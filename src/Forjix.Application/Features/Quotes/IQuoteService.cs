using Forjix.Application.Features.Analytics;
using Forjix.Application.Features.Sales;
namespace Forjix.Application.Features.Quotes;
public interface IQuoteService
{
    Task<PagedQuotes> ListAsync(QuoteFilter filter, CancellationToken ct = default);
    Task<QuoteView> GetAsync(Guid id, CancellationToken ct = default);
    Task<QuoteView> SaveAsync(Guid? id, SaveQuoteRequest request, CancellationToken ct = default);
    Task<QuoteView> TransitionAsync(Guid id, string action, QuoteActionRequest request, CancellationToken ct = default);
    Task<SaleView> ConvertAsync(Guid id, ConvertQuoteRequest request, CancellationToken ct = default);
    Task<ExportedReport> PdfAsync(Guid id, CancellationToken ct = default);
    Task<QuoteSummary> SummaryAsync(CancellationToken ct = default);
}
