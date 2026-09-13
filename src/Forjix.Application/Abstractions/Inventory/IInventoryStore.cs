using Forjix.Application.Abstractions.Tenancy;
using Forjix.Application.Features.Inventory;
using Forjix.Domain.Entities.Audit;
using Forjix.Domain.Entities.Inventory;
using InventoryEntity = Forjix.Domain.Entities.Inventory.Inventory;

namespace Forjix.Application.Abstractions.Inventory;

public interface IInventoryStoreFactory
{
    IInventoryStore Create(ResolvedTenantDatabase tenant);
}

public interface IInventoryStore : IAsyncDisposable
{
    Task<List<InventoryItem>> GetAsync(string? search, Guid? categoryId, string? status, CancellationToken cancellationToken);
    Task<InventoryEntity?> GetByProductAsync(Guid productId, CancellationToken cancellationToken);
    Task<(List<InventoryMovementItem> Items, int Total)> GetMovementsAsync(Guid productId, DateTimeOffset? from, DateTimeOffset? toDate, string? type, string? user, int page, int pageSize, CancellationToken cancellationToken);
    Task<string> SaveMovementAsync(InventoryEntity inventory, byte[] expectedRowVersion, InventoryMovement movement, AuditLog audit, CancellationToken cancellationToken);
}
