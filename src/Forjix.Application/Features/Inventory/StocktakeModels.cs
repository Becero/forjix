namespace Forjix.Application.Features.Inventory;

public sealed record SaveStocktakeRequest(IReadOnlyList<Guid> ProductIds, string? Notes, string? RowVersion = null);
public sealed record StocktakeActionRequest(string RowVersion, string? Reason = null);
public sealed record CountStocktakeItemRequest(Guid ProductId, decimal Quantity, string? Notes);
public sealed record CountStocktakeRequest(string RowVersion, IReadOnlyList<CountStocktakeItemRequest> Items);
public sealed record StocktakeFilter(string? Search = null, string? Status = null, DateTimeOffset? From = null, DateTimeOffset? To = null, int Page = 1, int PageSize = 20);
public sealed record StocktakeItemView(Guid ProductId, string ProductName, string Sku, decimal ExpectedQuantity, decimal? CountedQuantity, decimal? Difference, string? Notes, DateTimeOffset? CountedAt, Guid? CountedByUserId, string? CountedByName);
public sealed record StocktakeView(Guid Id, string Number, string Status, string? Notes, Guid UserId, string UserName, DateTimeOffset OpenedAt, DateTimeOffset? StartedAt, DateTimeOffset? ClosedAt, string RowVersion, IReadOnlyList<StocktakeItemView> Items);
public sealed record StockOverview(int ControlledProducts, int OutOfStock, int LowStock, int NearMinimum, int OngoingInventories, decimal EstimatedValue);
public sealed record StockReportItem(Guid ProductId, string ProductName, string Sku, decimal Quantity, decimal MinimumStock, decimal SuggestedReplacement, DateTimeOffset? LastMovementAt, decimal EstimatedValue);
public sealed record StockMovementFilter(Guid? ProductId = null, DateTimeOffset? From = null, DateTimeOffset? To = null, string? Type = null, string? Origin = null, string? User = null, int Page = 1, int PageSize = 50);
public sealed record StockMovementView(long Id, Guid ProductId, string ProductName, string Sku, string Type, decimal Entry, decimal Exit, decimal PreviousQuantity, decimal NewQuantity, string Origin, string? ReferenceId, string? ReferenceNumber, Guid UserId, string UserName, DateTimeOffset CreatedAt, string? Reason, string? ReasonCode, string? Observation);
public sealed record StockAdjustmentRequest(Guid ProductId, string Type, decimal Quantity, string Reason, string? Observation, string RowVersion);
