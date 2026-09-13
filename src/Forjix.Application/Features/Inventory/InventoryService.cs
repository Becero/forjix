using System.Text.Json;
using Forjix.Application.Abstractions.Identity;
using Forjix.Application.Abstractions.Inventory;
using Forjix.Application.Abstractions.Tenancy;
using Forjix.Application.Common;
using Forjix.Domain.Entities.Audit;
using Forjix.Domain.Entities.Inventory;
using Forjix.Domain.Enums;

namespace Forjix.Application.Features.Inventory;

internal sealed class InventoryService(
    ITenantDatabaseResolver tenantResolver,
    IInventoryStoreFactory storeFactory,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : IInventoryService
{
    public async Task<IReadOnlyList<InventoryItem>> GetAsync(string? search, Guid? categoryId, string? status, CancellationToken cancellationToken = default)
    {
        ValidateStatus(status);
        var (_, store) = await StoreAsync(cancellationToken);
        await using (store) return await store.GetAsync(Clean(search), categoryId, Clean(status), cancellationToken);
    }

    public async Task<InventoryItem> GetByProductAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        var (_, store) = await StoreAsync(cancellationToken);
        await using (store)
        {
            var inventory = await store.GetByProductAsync(productId, cancellationToken) ?? throw new ResourceNotFoundException("Estoque do produto não encontrado.");
            return Map(inventory);
        }
    }

    public async Task<PagedResult<InventoryMovementItem>> GetMovementsAsync(Guid productId, DateTimeOffset? from, DateTimeOffset? toDate, string? type, string? user, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        if (from > toDate) throw new RequestValidationException("O período inicial deve ser anterior ao período final.");
        if (!string.IsNullOrWhiteSpace(type) && !Enum.TryParse<InventoryMovementType>(type, true, out _)) throw new RequestValidationException("Tipo de movimentação inválido.");
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var (_, store) = await StoreAsync(cancellationToken);
        await using (store)
        {
            if (await store.GetByProductAsync(productId, cancellationToken) is null) throw new ResourceNotFoundException("Estoque do produto não encontrado.");
            var result = await store.GetMovementsAsync(productId, from, toDate, Clean(type), Clean(user), page, pageSize, cancellationToken);
            return new PagedResult<InventoryMovementItem>(result.Items, page, pageSize, result.Total);
        }
    }

    public async Task<InventoryMovementResult> CreateMovementAsync(Guid productId, CreateInventoryMovementRequest request, CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<InventoryMovementType>(request.Type, true, out var type) || !Enum.IsDefined(type)) throw new RequestValidationException("Tipo de movimentação inválido.");
        if (request.Quantity <= 0) throw new RequestValidationException("A quantidade deve ser maior que zero.");
        var reason = Clean(request.Reason);
        if (type is InventoryMovementType.PositiveAdjustment or InventoryMovementType.NegativeAdjustment && reason is null) throw new RequestValidationException("Informe o motivo do ajuste.");
        if (reason?.Length > 500) throw new RequestValidationException("O motivo deve ter no máximo 500 caracteres.");
        byte[] expectedRowVersion;
        try
        {
            expectedRowVersion = Convert.FromBase64String(request.RowVersion ?? string.Empty);
            if (expectedRowVersion.Length == 0) throw new FormatException();
        }
        catch (FormatException)
        {
            throw new RequestValidationException("A versão atual do estoque é obrigatória. Atualize a página e tente novamente.");
        }

        var (tenant, store) = await StoreAsync(cancellationToken);
        await using (store)
        {
            var inventory = await store.GetByProductAsync(productId, cancellationToken) ?? throw new ResourceNotFoundException("Estoque do produto não encontrado.");
            if (!inventory.Product.IsActive) throw new RequestValidationException("Produto inativo não pode receber movimentações.");
            if (!inventory.RowVersion.SequenceEqual(expectedRowVersion)) throw new ResourceConflictException("O estoque foi movimentado por outro usuário. Atualize a página e tente novamente.");
            var allowNegative = tenant.Settings.TryGetValue(TenantSettingKeys.AllowNegativeStock, out var setting) && bool.TryParse(setting, out var enabled) ? enabled : TenantSettingDefaults.AllowNegativeStock;
            var now = timeProvider.GetUtcNow();
            InventoryChange change;
            try { change = inventory.ApplyMovement(type, request.Quantity, allowNegative, now); }
            catch (InvalidOperationException) { throw new RequestValidationException("Saldo insuficiente. O estoque negativo não está permitido para esta empresa."); }

            if (currentUser.UserId is not { } userId) throw new ResourceNotFoundException("Usuário autenticado não encontrado.");
            var movement = new InventoryMovement
            {
                InventoryId = inventory.Id,
                ProductId = productId,
                Type = type,
                Quantity = request.Quantity,
                PreviousQuantity = change.PreviousQuantity,
                NewQuantity = change.NewQuantity,
                Reason = reason,
                UserId = userId,
                CreatedAt = now
            };
            var audit = new AuditLog
            {
                UserId = userId,
                Action = AuditAction.StockMovementCreated,
                EntityName = nameof(InventoryMovement),
                EntityId = productId.ToString(),
                AfterData = JsonSerializer.Serialize(new { ProductId = productId, Type = type.ToString() }),
                IpAddress = currentUser.IpAddress,
                CorrelationId = currentUser.CorrelationId,
                OccurredAt = now
            };
            var userName = await store.SaveMovementAsync(inventory, expectedRowVersion, movement, audit, cancellationToken);
            return new InventoryMovementResult(Map(inventory), Map(movement, userName));
        }
    }

    private async Task<(ResolvedTenantDatabase Tenant, IInventoryStore Store)> StoreAsync(CancellationToken cancellationToken)
    {
        if (currentUser.TenantId is not { } tenantId) throw new ResourceNotFoundException("Tenant autenticado não encontrado.");
        var tenant = await tenantResolver.ResolveByTenantIdAsync(tenantId, cancellationToken) ?? throw new ResourceNotFoundException("Tenant autenticado não encontrado.");
        return (tenant, storeFactory.Create(tenant));
    }

    private static InventoryItem Map(Forjix.Domain.Entities.Inventory.Inventory value) => new(value.ProductId, value.Product.Name, value.Product.Sku, value.Product.Barcode, value.Product.CategoryId, value.Product.Category.Name, value.Quantity, value.Product.MinimumStock, value.Product.CostPrice, value.Product.SalePrice, Status(value.Quantity, value.Product.MinimumStock), value.Movements.OrderByDescending(x => x.CreatedAt).Select(x => (DateTimeOffset?)x.CreatedAt).FirstOrDefault(), Convert.ToBase64String(value.RowVersion));
    private static InventoryMovementItem Map(InventoryMovement value, string userName) => new(value.Id, value.ProductId, value.Type.ToString(), value.Quantity, value.PreviousQuantity, value.NewQuantity, value.Reason, value.ReferenceType, value.ReferenceId, value.UserId, userName, value.CreatedAt);
    private static string Status(decimal quantity, decimal minimum) => quantity < 0 ? "Negative" : quantity == 0 ? "OutOfStock" : quantity <= minimum ? "Low" : "Normal";
    private static void ValidateStatus(string? status) { if (!string.IsNullOrWhiteSpace(status) && status is not ("Normal" or "Low" or "OutOfStock" or "Negative")) throw new RequestValidationException("Situação de estoque inválida."); }
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
