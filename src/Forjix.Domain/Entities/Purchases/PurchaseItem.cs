using Forjix.Domain.Entities.Catalog;
namespace Forjix.Domain.Entities.Purchases;
public sealed class PurchaseItem { public Guid Id { get; set; } public Guid PurchaseId { get; set; } public Guid ProductId { get; set; } public required string ProductName { get; set; } public required string Sku { get; set; } public decimal Quantity { get; set; } public decimal UnitCost { get; set; } public decimal Total { get; set; } public Purchase Purchase { get; set; }=null!; public Product Product { get; set; }=null!; }
