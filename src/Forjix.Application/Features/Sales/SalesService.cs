using Forjix.Application.Abstractions.Authorization;
using Forjix.Application.Abstractions.Identity;
using Forjix.Application.Abstractions.Sales;
using Forjix.Application.Abstractions.Tenancy;
using Forjix.Application.Common;
using Forjix.Domain.Enums;

namespace Forjix.Application.Features.Sales;

internal sealed class SalesService(ITenantDatabaseResolver tenantResolver, ISalesStoreFactory storeFactory, IPermissionChecker permissions, ICurrentUser currentUser, TimeProvider timeProvider) : ISalesService
{
    public async Task<SaleView> CreateAsync(string idempotencyKey, CreateSaleRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Trim().Length > 100) throw new RequestValidationException("Informe um Idempotency-Key válido.");
        if (!Enum.TryParse<PaymentMethod>(request.PaymentMethod, true, out var paymentMethod) || !Enum.IsDefined(paymentMethod)) throw new RequestValidationException("Forma de pagamento inválida.");
        if (request.Discount < 0) throw new RequestValidationException("O desconto não pode ser negativo.");
        if (request.Items.Count == 0) throw new RequestValidationException("Adicione ao menos um produto à venda.");
        if (request.Items.Any(x => x.ProductId == Guid.Empty || x.Quantity <= 0)) throw new RequestValidationException("Todos os itens devem possuir produto e quantidade maior que zero.");
        var (tenant, userId, store) = await StoreAsync(cancellationToken);
        await using (store)
        {
            if (request.Discount > 0 && !await permissions.HasPermissionAsync(tenant.TenantId, userId, Permissions.SalesDiscount, cancellationToken)) throw new PermissionDeniedException("Seu grupo não permite conceder descontos.");
            return await store.CreateAsync(idempotencyKey.Trim(), userId, paymentMethod, request.Discount, request.CustomerId, request.Items, AllowNegative(tenant), Audit(), timeProvider.GetUtcNow(), cancellationToken);
        }
    }

    public async Task<PagedSales> GetAsync(DateTimeOffset? from, DateTimeOffset? through, string? status, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        if (from > through) throw new RequestValidationException("O período inicial deve ser anterior ao final.");
        SaleStatus? parsed = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<SaleStatus>(status, true, out var value) || !Enum.IsDefined(value)) throw new RequestValidationException("Status de venda inválido.");
            parsed = value;
        }
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
        var (_, _, store) = await StoreAsync(cancellationToken);
        await using (store) { var result = await store.GetAsync(from, through, parsed, page, pageSize, cancellationToken); return new(result.Items, page, pageSize, result.Total); }
    }

    public async Task<SaleView> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var (_, _, store) = await StoreAsync(cancellationToken);
        await using (store) return await store.GetByIdAsync(id, cancellationToken) ?? throw new ResourceNotFoundException("Venda não encontrada.");
    }

    public async Task<SaleView> CancelAsync(Guid id, CancelSaleRequest request, CancellationToken cancellationToken = default)
    {
        var reason = request.Reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 500) throw new RequestValidationException("Informe um motivo de cancelamento com até 500 caracteres.");
        byte[] version;
        try { version = Convert.FromBase64String(request.RowVersion ?? string.Empty); if (version.Length == 0) throw new FormatException(); }
        catch (FormatException) { throw new RequestValidationException("A versão atual da venda é obrigatória."); }
        var (tenant, userId, store) = await StoreAsync(cancellationToken);
        await using (store) return await store.CancelAsync(id, userId, reason, version, AllowNegative(tenant), Audit(), timeProvider.GetUtcNow(), cancellationToken);
    }

    private async Task<(ResolvedTenantDatabase Tenant, Guid UserId, ISalesStore Store)> StoreAsync(CancellationToken cancellationToken)
    {
        if (currentUser.TenantId is not { } tenantId || currentUser.UserId is not { } userId) throw new ResourceNotFoundException("Contexto autenticado não encontrado.");
        var tenant = await tenantResolver.ResolveByTenantIdAsync(tenantId, cancellationToken) ?? throw new ResourceNotFoundException("Tenant autenticado não encontrado.");
        return (tenant, userId, storeFactory.Create(tenant));
    }
    private static bool AllowNegative(ResolvedTenantDatabase tenant) => tenant.Settings.TryGetValue(TenantSettingKeys.AllowNegativeStock, out var value) && bool.TryParse(value, out var enabled) ? enabled : TenantSettingDefaults.AllowNegativeStock;
    private SalesAuditContext Audit() => new(currentUser.CorrelationId, currentUser.IpAddress);
}
