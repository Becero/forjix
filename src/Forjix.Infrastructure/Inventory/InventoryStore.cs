using Forjix.Application.Abstractions.Inventory;
using Forjix.Application.Abstractions.Tenancy;
using Forjix.Application.Common;
using Forjix.Application.Features.Inventory;
using Forjix.Domain.Entities.Audit;
using Forjix.Domain.Entities.Inventory;
using Forjix.Domain.Enums;
using Forjix.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;
using InventoryEntity = Forjix.Domain.Entities.Inventory.Inventory;

namespace Forjix.Infrastructure.Inventory;

internal sealed class InventoryStoreFactory(ITenantDbContextFactory contextFactory) : IInventoryStoreFactory
{
    public IInventoryStore Create(ResolvedTenantDatabase tenant) => new InventoryStore(contextFactory.Create(tenant));
}

internal sealed class InventoryStore(TenantDbContext db) : IInventoryStore
{
    public Task<List<InventoryItem>> GetAsync(string? search, Guid? categoryId, string? status, CancellationToken cancellationToken)
    {
        var query = db.Inventories.AsNoTracking().Where(x => x.Product.IsActive);
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(x => x.Product.Name.Contains(search) || x.Product.Sku.Contains(search) || (x.Product.Barcode != null && x.Product.Barcode.Contains(search)));
        if (categoryId.HasValue) query = query.Where(x => x.Product.CategoryId == categoryId);
        query = status switch
        {
            "Negative" => query.Where(x => x.Quantity < 0),
            "OutOfStock" => query.Where(x => x.Quantity == 0),
            "Low" => query.Where(x => x.Quantity > 0 && x.Quantity <= x.Product.MinimumStock),
            "Normal" => query.Where(x => x.Quantity > x.Product.MinimumStock),
            _ => query
        };
        return query.OrderBy(x => x.Product.Name).Select(x => new InventoryItem(
            x.ProductId, x.Product.Name, x.Product.Sku, x.Product.Barcode, x.Product.CategoryId,
            x.Product.Category.Name, x.Quantity, x.Product.MinimumStock, x.Product.CostPrice, x.Product.SalePrice,
            x.Quantity < 0 ? "Negative" : x.Quantity == 0 ? "OutOfStock" : x.Quantity <= x.Product.MinimumStock ? "Low" : "Normal",
            x.Movements.OrderByDescending(m => m.CreatedAt).Select(m => (DateTimeOffset?)m.CreatedAt).FirstOrDefault(),
            Convert.ToBase64String(x.RowVersion))).ToListAsync(cancellationToken);
    }

    public Task<InventoryEntity?> GetByProductAsync(Guid productId, CancellationToken cancellationToken) =>
        db.Inventories.Include(x => x.Product).ThenInclude(x => x.Category).Include(x => x.Movements).SingleOrDefaultAsync(x => x.ProductId == productId, cancellationToken);

    public async Task<(List<InventoryMovementItem> Items, int Total)> GetMovementsAsync(Guid productId, DateTimeOffset? from, DateTimeOffset? toDate, string? type, string? user, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = db.InventoryMovements.AsNoTracking().Where(x => x.ProductId == productId);
        if (from.HasValue) query = query.Where(x => x.CreatedAt >= from);
        if (toDate.HasValue) query = query.Where(x => x.CreatedAt <= toDate);
        if (Enum.TryParse<InventoryMovementType>(type, true, out var parsed)) query = query.Where(x => x.Type == parsed);
        if (!string.IsNullOrWhiteSpace(user)) query = query.Where(x => x.User.Name.Contains(user));
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new InventoryMovementItem(x.Id, x.ProductId, x.Type.ToString(), x.Quantity, x.PreviousQuantity, x.NewQuantity, x.Reason, x.ReferenceType, x.ReferenceId, x.UserId, x.User.Name, x.CreatedAt))
            .ToListAsync(cancellationToken);
        return (items, total);
    }

    public async Task<string> SaveMovementAsync(InventoryEntity inventory, byte[] expectedRowVersion, InventoryMovement movement, AuditLog audit, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        db.Entry(inventory).Property(x => x.RowVersion).OriginalValue = expectedRowVersion;
        db.InventoryMovements.Add(movement);
        db.AuditLogs.Add(audit);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return await db.Users.Where(x => x.Id == movement.UserId).Select(x => x.Name).SingleAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new ResourceConflictException("O estoque foi movimentado por outro usuário. Atualize a página e tente novamente.");
        }
    }

    public ValueTask DisposeAsync() => db.DisposeAsync();
}
