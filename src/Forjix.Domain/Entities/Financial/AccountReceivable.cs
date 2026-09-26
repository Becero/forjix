using Forjix.Domain.Entities.Customers;
using Forjix.Domain.Entities.Sales;

namespace Forjix.Domain.Entities.Financial;

public sealed class AccountReceivable : FinancialAccount
{
    public Guid? CustomerId { get; set; }
    public Guid? SaleId { get; set; }
    public Customer? Customer { get; set; }
    public Sale? Sale { get; set; }
}
