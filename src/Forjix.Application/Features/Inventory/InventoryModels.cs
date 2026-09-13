namespace Forjix.Application.Features.Inventory;

public sealed record InventoryItem(
    Guid ProductId, string ProductName, string Sku, string? Barcode, Guid CategoryId,
    string CategoryName, decimal Quantity, decimal MinimumStock, decimal CostPrice, decimal SalePrice,
    string Status, DateTimeOffset? LastMovementAt, string RowVersion);

public sealed record InventoryMovementItem(
    long Id, Guid ProductId, string Type, decimal Quantity, decimal PreviousQuantity,
    decimal NewQuantity, string? Reason, string? ReferenceType, string? ReferenceId,
    Guid UserId, string UserName, DateTimeOffset CreatedAt);

public sealed record CreateInventoryMovementRequest(string Type, decimal Quantity, string? Reason, string? RowVersion);
public sealed record InventoryMovementResult(InventoryItem Inventory, InventoryMovementItem Movement);
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total);
