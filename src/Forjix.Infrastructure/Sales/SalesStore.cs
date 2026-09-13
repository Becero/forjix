using System.Data;
using System.Text.Json;
using Forjix.Application.Abstractions.Sales;
using Forjix.Application.Abstractions.Tenancy;
using Forjix.Application.Common;
using Forjix.Application.Features.Sales;
using Forjix.Domain.Entities.Audit;
using Forjix.Domain.Entities.Cash;
using Forjix.Domain.Entities.Inventory;
using Forjix.Domain.Entities.Sales;
using Forjix.Domain.Enums;
using Forjix.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;

namespace Forjix.Infrastructure.Sales;

internal sealed class SalesStoreFactory(ITenantDbContextFactory contextFactory) : ISalesStoreFactory
{
    public ISalesStore Create(ResolvedTenantDatabase tenant) => new SalesStore(contextFactory.Create(tenant));
}

internal sealed class SalesStore(TenantDbContext db) : ISalesStore
{
    public async Task<SaleView> CreateAsync(string idempotencyKey, Guid userId, PaymentMethod paymentMethod, decimal discount, Guid? customerId, IReadOnlyList<CreateSaleItemRequest> requestedItems, bool allowNegativeStock, SalesAuditContext auditContext, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var existing = await SaleQuery().SingleOrDefaultAsync(x => x.IdempotencyKey == idempotencyKey, cancellationToken);
            if (existing is not null) { await transaction.CommitAsync(cancellationToken); return Map(existing); }
            var cashSession = await db.CashSessions.Include(x => x.Movements).SingleOrDefaultAsync(x => x.Status == CashSessionStatus.Open, cancellationToken)
                ?? throw new ResourceConflictException("Abra o caixa antes de realizar uma venda.");

            var items = requestedItems.GroupBy(x => x.ProductId).Select(x => new CreateSaleItemRequest(x.Key, x.Sum(y => y.Quantity))).ToList();
            var productIds = items.Select(x => x.ProductId).ToList();
            var inventories = await db.Inventories.Include(x => x.Product).Where(x => productIds.Contains(x.ProductId)).ToListAsync(cancellationToken);
            if (inventories.Count != productIds.Count) throw new RequestValidationException("Um ou mais produtos não foram encontrados.");
            if (inventories.Any(x => !x.Product.IsActive)) throw new RequestValidationException("Produto inativo não pode ser vendido.");

            var subtotal = items.Sum(item => inventories.Single(x => x.ProductId == item.ProductId).Product.SalePrice * item.Quantity);
            if (discount > subtotal) throw new RequestValidationException("O desconto não pode ser maior que o subtotal.");
            var date = DateOnly.FromDateTime(now.UtcDateTime);
            var sequence = await db.SaleSequences.SingleOrDefaultAsync(x => x.Date == date, cancellationToken);
            if (sequence is null) { sequence = new SaleSequence { Date = date }; db.SaleSequences.Add(sequence); }
            sequence.LastValue++;

            var sale = new Sale
            {
                Id = Guid.NewGuid(), Number = $"VD-{date:yyyyMMdd}-{sequence.LastValue:00000}", IdempotencyKey = idempotencyKey,
                Subtotal = subtotal, Discount = discount, Total = subtotal - discount, PaymentMethod = paymentMethod,
                UserId = userId, CustomerId = customerId, CreatedAt = now
            };
            foreach (var request in items)
            {
                var inventory = inventories.Single(x => x.ProductId == request.ProductId);
                InventoryChange change;
                try { change = inventory.ApplyMovement(InventoryMovementType.Sale, request.Quantity, allowNegativeStock, now); }
                catch (InvalidOperationException) { throw new RequestValidationException($"Saldo insuficiente para {inventory.Product.Name}."); }
                var item = new SaleItem { Id = Guid.NewGuid(), ProductId = inventory.ProductId, ProductName = inventory.Product.Name, Sku = inventory.Product.Sku, Quantity = request.Quantity, UnitPrice = inventory.Product.SalePrice, UnitCost = inventory.Product.CostPrice, Discount = 0, Total = inventory.Product.SalePrice * request.Quantity };
                sale.Items.Add(item);
                db.InventoryMovements.Add(new InventoryMovement { InventoryId = inventory.Id, ProductId = inventory.ProductId, Type = InventoryMovementType.Sale, Quantity = request.Quantity, PreviousQuantity = change.PreviousQuantity, NewQuantity = change.NewQuantity, ReferenceType = nameof(Sale), ReferenceId = sale.Id.ToString(), UserId = userId, CreatedAt = now });
            }
            db.Sales.Add(sale);
            if (paymentMethod == PaymentMethod.Cash) cashSession.Movements.Add(new CashMovement { Type = CashMovementType.Sale, Amount = sale.Total, Reason = sale.Number, SaleId = sale.Id, UserId = userId, CreatedAt = now });
            db.AuditLogs.Add(Audit(AuditAction.SaleCreated, sale.Id, userId, auditContext, now, new { sale.Number, sale.Total, ItemCount = items.Count }));
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Map(await SaleQuery().SingleAsync(x => x.Id == sale.Id, cancellationToken));
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new ResourceConflictException("O estoque foi alterado durante a venda. Atualize os produtos e tente novamente.");
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken); throw;
        }
    }

    public async Task<(List<SaleListItem> Items, int Total)> GetAsync(DateTimeOffset? from, DateTimeOffset? through, SaleStatus? status, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = db.Sales.AsNoTracking().AsQueryable();
        if (from.HasValue) query = query.Where(x => x.CreatedAt >= from);
        if (through.HasValue) query = query.Where(x => x.CreatedAt <= through);
        if (status.HasValue) query = query.Where(x => x.Status == status);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Number).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new SaleListItem(x.Id, x.Number, x.Status.ToString(), x.Total, x.PaymentMethod.ToString(), x.User.Name, x.CustomerId, x.Items.Count, x.CreatedAt, Convert.ToBase64String(x.RowVersion))).ToListAsync(cancellationToken);
        return (items, total);
    }

    public async Task<SaleView?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => MapNullable(await SaleQuery().SingleOrDefaultAsync(x => x.Id == id, cancellationToken));

    public async Task<SaleView> CancelAsync(Guid id, Guid userId, string reason, byte[] rowVersion, bool allowNegativeStock, SalesAuditContext auditContext, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var sale = await db.Sales.Include(x => x.User).Include(x => x.Items).SingleOrDefaultAsync(x => x.Id == id, cancellationToken) ?? throw new ResourceNotFoundException("Venda não encontrada.");
            if (sale.Status == SaleStatus.Cancelled) throw new ResourceConflictException("Esta venda já foi cancelada.");
            db.Entry(sale).Property(x => x.RowVersion).OriginalValue = rowVersion;
            var productIds = sale.Items.Select(x => x.ProductId).ToList();
            var inventories = await db.Inventories.Where(x => productIds.Contains(x.ProductId)).ToListAsync(cancellationToken);
            sale.Cancel(userId, reason, now);
            foreach (var item in sale.Items)
            {
                var inventory = inventories.Single(x => x.ProductId == item.ProductId);
                var change = inventory.ApplyMovement(InventoryMovementType.SaleCancellation, item.Quantity, allowNegativeStock, now);
                db.InventoryMovements.Add(new InventoryMovement { InventoryId = inventory.Id, ProductId = item.ProductId, Type = InventoryMovementType.SaleCancellation, Quantity = item.Quantity, PreviousQuantity = change.PreviousQuantity, NewQuantity = change.NewQuantity, Reason = reason, ReferenceType = nameof(Sale), ReferenceId = sale.Id.ToString(), UserId = userId, CreatedAt = now });
            }
            db.AuditLogs.Add(Audit(AuditAction.SaleCancelled, sale.Id, userId, auditContext, now, new { sale.Number, Reason = reason }));
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Map(sale);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken); throw new ResourceConflictException("A venda ou o estoque foi alterado por outro usuário.");
        }
        catch { await transaction.RollbackAsync(cancellationToken); throw; }
    }

    private IQueryable<Sale> SaleQuery() => db.Sales.AsNoTracking().Include(x => x.User).Include(x => x.Items).OrderBy(x => x.Id);
    private static SaleView? MapNullable(Sale? sale) => sale is null ? null : Map(sale);
    private static SaleView Map(Sale sale) => new(sale.Id, sale.Number, sale.Status.ToString(), sale.Subtotal, sale.Discount, sale.Total, sale.PaymentMethod.ToString(), sale.UserId, sale.User.Name, sale.CustomerId, sale.CreatedAt, sale.CancelledAt, sale.CancellationReason, Convert.ToBase64String(sale.RowVersion), sale.Items.OrderBy(x => x.ProductName).Select(x => new SaleItemView(x.Id, x.ProductId, x.ProductName, x.Sku, x.Quantity, x.UnitPrice, x.UnitCost, x.Discount, x.Total)).ToList());
    private static AuditLog Audit(AuditAction action, Guid id, Guid userId, SalesAuditContext context, DateTimeOffset now, object data) => new() { UserId = userId, Action = action, EntityName = nameof(Sale), EntityId = id.ToString(), AfterData = JsonSerializer.Serialize(data), IpAddress = context.IpAddress, CorrelationId = context.CorrelationId, OccurredAt = now };
    public ValueTask DisposeAsync() => db.DisposeAsync();
}
