using Forjix.Domain.Entities.Customers;
using Forjix.Domain.Entities.Identity;
using Forjix.Domain.Entities.Sales;
using Forjix.Domain.Enums;
namespace Forjix.Domain.Entities.Quotes;
public sealed class Quote
{
    public Guid Id { get; set; }
    public required string Number { get; set; }
    public Guid CustomerId { get; set; }
    public DateOnly IssueDate { get; set; }
    public DateOnly ValidUntil { get; set; }
    public QuoteStatus Status { get; private set; } = QuoteStatus.Draft;
    public string? Notes { get; set; }
    public decimal Subtotal { get; private set; }
    public decimal Discount { get; set; }
    public decimal Total { get; private set; }
    public Guid UserId { get; set; }
    public Guid UpdatedByUserId { get; set; }
    public Guid? SaleId { get; private set; }
    public DateTimeOffset? ConvertedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public Customer Customer { get; set; } = null!;
    public User User { get; set; } = null!;
    public Sale? Sale { get; set; }
    public ICollection<QuoteItem> Items { get; set; } = [];
    public QuoteStatus StatusOn(DateOnly today) => Status is QuoteStatus.Draft or QuoteStatus.Sent or QuoteStatus.Approved && ValidUntil < today ? QuoteStatus.Expired : Status;
    public void EnsureEditable()
    {
        if (Status != QuoteStatus.Draft) throw new InvalidOperationException("Somente rascunhos podem ser editados.");
    }
    public void Calculate()
    {
        EnsureEditable();
        if (CustomerId == Guid.Empty || Items.Count == 0 || ValidUntil < IssueDate) throw new ArgumentException("Informe cliente, itens e validade.");
        Money(Discount);
        foreach (var item in Items) item.Calculate();
        Subtotal = Items.Sum(x => x.Total + x.Discount);
        Money(Subtotal);
        if (Discount > Items.Sum(x => x.Total)) throw new ArgumentException("Desconto supera o subtotal líquido dos itens.");
        Total = Items.Sum(x => x.Total) - Discount;
    }
    public void Transition(QuoteStatus next, DateOnly today)
    {
        var current = StatusOn(today);
        var allowed = next switch
        {
            QuoteStatus.Sent => current == QuoteStatus.Draft,
            QuoteStatus.Approved or QuoteStatus.Rejected => current == QuoteStatus.Sent,
            QuoteStatus.Cancelled => current is QuoteStatus.Draft or QuoteStatus.Sent or QuoteStatus.Approved or QuoteStatus.Expired,
            _ => false
        };
        if (!allowed) throw new InvalidOperationException("Transição de status não permitida.");
        Status = next;
    }
    public void EnsureConvertible(DateOnly today)
    {
        if (StatusOn(today) != QuoteStatus.Approved || SaleId.HasValue) throw new InvalidOperationException("Somente orçamentos aprovados e válidos podem ser convertidos.");
    }
    public void ConvertToSale(Guid saleId, DateTimeOffset now)
    {
        EnsureConvertible(DateOnly.FromDateTime(now.UtcDateTime));
        if (saleId == Guid.Empty) throw new ArgumentException("Venda inválida.");
        SaleId = saleId; ConvertedAt = now; Status = QuoteStatus.Converted;
    }
    public static void Money(decimal value)
    {
        if (value < 0 || value > 9999999999999999.99m || decimal.Round(value, 2) != value)
            throw new ArgumentException("Valor deve ser não negativo com até duas casas decimais.");
    }
}
public sealed class QuoteSequence
{
    public int Id { get; set; } = 1;
    public long LastValue { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
