namespace Forjix.Application.Features.Sales;

public sealed record CreateSaleItemRequest(Guid ProductId, decimal Quantity);
public sealed record CreateSaleRequest(string PaymentMethod, decimal Discount, Guid? CustomerId, IReadOnlyList<CreateSaleItemRequest> Items);
public sealed record CancelSaleRequest(string Reason, string RowVersion);
public sealed record SaleItemView(Guid Id, Guid ProductId, string ProductName, string Sku, decimal Quantity, decimal UnitPrice, decimal UnitCost, decimal Discount, decimal Total);
public sealed record SaleView(Guid Id, string Number, string Status, decimal Subtotal, decimal Discount, decimal Total, string PaymentMethod, Guid UserId, string UserName, Guid? CustomerId, DateTimeOffset CreatedAt, DateTimeOffset? CancelledAt, string? CancellationReason, string RowVersion, IReadOnlyList<SaleItemView> Items);
public sealed record SaleListItem(Guid Id, string Number, string Status, decimal Total, string PaymentMethod, string UserName, Guid? CustomerId, int ItemCount, DateTimeOffset CreatedAt, string RowVersion);
public sealed record PagedSales(IReadOnlyList<SaleListItem> Items, int Page, int PageSize, int Total);
