using Forjix.Domain.Entities.Catalog;

namespace Forjix.Domain.Entities.Sales;

public sealed class SaleItem
{
    public Guid Id { get; set; }
    public Guid SaleId { get; set; }
    public Guid ProductId { get; set; }
    public required string ProductName { get; set; }
    public required string Sku { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal UnitCost { get; set; }
    public decimal Discount { get; set; }
    public decimal Total { get; set; }
    public Sale Sale { get; set; } = null!;
    public Product Product { get; set; } = null!;
}
