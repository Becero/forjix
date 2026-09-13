namespace Forjix.Domain.Entities.Sales;

public sealed class SaleSequence
{
    public DateOnly Date { get; set; }
    public int LastValue { get; set; }
}
