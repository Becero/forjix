namespace Forjix.Application.Features.Inventory;

public interface IInventoryService
{
    Task<IReadOnlyList<InventoryItem>> GetAsync(string? search, Guid? categoryId, string? status, CancellationToken cancellationToken = default);
    Task<InventoryItem> GetByProductAsync(Guid productId, CancellationToken cancellationToken = default);
    Task<PagedResult<InventoryMovementItem>> GetMovementsAsync(Guid productId, DateTimeOffset? from, DateTimeOffset? toDate, string? type, string? user, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<InventoryMovementResult> CreateMovementAsync(Guid productId, CreateInventoryMovementRequest request, CancellationToken cancellationToken = default);
}
