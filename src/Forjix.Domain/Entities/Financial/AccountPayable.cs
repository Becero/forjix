using Forjix.Domain.Entities.Purchases;

namespace Forjix.Domain.Entities.Financial;

public sealed class AccountPayable : FinancialAccount
{
    public Guid? SupplierId { get; set; }
    public Guid? PurchaseId { get; set; }
    public Supplier? Supplier { get; set; }
    public Purchase? Purchase { get; set; }
}
