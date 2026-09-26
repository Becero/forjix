using Forjix.Application.Abstractions.Sales;
using Forjix.Application.Abstractions.Tenancy;
using Forjix.Application.Features.Quotes;
using Forjix.Application.Features.Sales;
using Forjix.Domain.Enums;
namespace Forjix.Application.Abstractions.Quotes;
public interface IQuoteStoreFactory { IQuoteStore Create(ResolvedTenantDatabase tenant); }
public interface IQuoteStore : IAsyncDisposable
{
    Task<PagedQuotes> ListAsync(QuoteFilter filter, DateOnly today, CancellationToken ct);
    Task<QuoteView?> GetAsync(Guid id, DateOnly today, CancellationToken ct);
    Task<QuoteView> SaveAsync(Guid? id, SaveQuoteRequest request, Guid user, SalesAuditContext audit, DateTimeOffset now, CancellationToken ct);
    Task<QuoteView> TransitionAsync(Guid id, QuoteStatus targetStatus, byte[] version, string? reason, Guid user, SalesAuditContext audit, DateTimeOffset now, CancellationToken ct);
    Task<SaleView> ConvertAsync(Guid id, ConvertQuoteRequest request, PaymentMethod method, byte[] version, bool allowNegativeStock, Guid user, SalesAuditContext audit, DateTimeOffset now, CancellationToken ct);
    Task<QuoteSummary> SummaryAsync(DateOnly today, CancellationToken ct);
}
