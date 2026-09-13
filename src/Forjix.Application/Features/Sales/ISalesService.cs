namespace Forjix.Application.Features.Sales;

public interface ISalesService
{
    Task<SaleView> CreateAsync(string idempotencyKey, CreateSaleRequest request, CancellationToken cancellationToken = default);
    Task<PagedSales> GetAsync(DateTimeOffset? from, DateTimeOffset? through, string? status, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<SaleView> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<SaleView> CancelAsync(Guid id, CancelSaleRequest request, CancellationToken cancellationToken = default);
}
