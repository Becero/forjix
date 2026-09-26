using Forjix.Domain.Entities.Catalog;
namespace Forjix.Domain.Entities.Quotes;
public sealed class QuoteItem
{
    public Guid Id { get; set; }
    public Guid QuoteId { get; set; }
    public Guid ProductId { get; set; }
    public required string ProductName { get; set; }
    public required string Sku { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Discount { get; set; }
    public decimal Total { get; private set; }
    public Quote Quote { get; set; } = null!;
    public Product Product { get; set; } = null!;
    public void Calculate()
    {
        if (Quantity <= 0 || Quantity > 999999999999999.999m || decimal.Round(Quantity, 3) != Quantity)
            throw new ArgumentException("Quantidade deve ser positiva com até três casas decimais.");
        Quote.Money(UnitPrice); Quote.Money(Discount);
        if (UnitPrice > 0 && Quantity > 9999999999999999.99m / UnitPrice) throw new ArgumentException("Valor do item supera o limite monetário.");
        var gross = decimal.Round(Quantity * UnitPrice, 2, MidpointRounding.AwayFromZero);
        Quote.Money(gross);
        if (Discount > gross) throw new ArgumentException("Desconto do item supera seu valor.");
        Total = gross - Discount;
    }
}
