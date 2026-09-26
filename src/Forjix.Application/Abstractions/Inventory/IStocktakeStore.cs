using Forjix.Application.Abstractions.Tenancy;
using Forjix.Application.Abstractions.Sales;
using Forjix.Application.Features.Inventory;
namespace Forjix.Application.Abstractions.Inventory;
public interface IStocktakeStoreFactory { IStocktakeStore Create(ResolvedTenantDatabase tenant); }
public interface IStocktakeStore : IAsyncDisposable
{
    Task<PagedResult<StocktakeView>> ListAsync(StocktakeFilter filter, CancellationToken ct);
    Task<StocktakeView?> GetAsync(Guid id, CancellationToken ct);
    Task<StocktakeView> SaveAsync(Guid? id, SaveStocktakeRequest request, Guid user, SalesAuditContext audit, DateTimeOffset now, CancellationToken ct);
    Task<StocktakeView> ActionAsync(Guid id, string action, StocktakeActionRequest request, Guid user, SalesAuditContext audit, DateTimeOffset now, CancellationToken ct);
    Task<StocktakeView> CountAsync(Guid id, CountStocktakeRequest request, Guid user, SalesAuditContext audit, DateTimeOffset now, CancellationToken ct);
    Task<StockOverview> OverviewAsync(CancellationToken ct);
    Task<IReadOnlyList<StockReportItem>> ReportAsync(bool lowStock, DateTimeOffset from, DateTimeOffset toDate, CancellationToken ct);
    Task<PagedResult<StockMovementView>> MovementsAsync(StockMovementFilter filter, CancellationToken ct);
}
