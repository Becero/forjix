namespace Forjix.Application.Features.Inventory;
public interface IStocktakeService
{
    Task<PagedResult<StocktakeView>> ListAsync(StocktakeFilter filter, CancellationToken ct = default);
    Task<StocktakeView> GetAsync(Guid id, CancellationToken ct = default);
    Task<StocktakeView> SaveAsync(Guid? id, SaveStocktakeRequest request, CancellationToken ct = default);
    Task<StocktakeView> ActionAsync(Guid id, string action, StocktakeActionRequest request, CancellationToken ct = default);
    Task<StocktakeView> CountAsync(Guid id, CountStocktakeRequest request, CancellationToken ct = default);
    Task<StockOverview> OverviewAsync(CancellationToken ct = default);
    Task<IReadOnlyList<StockReportItem>> ReportAsync(bool lowStock, int days, DateTimeOffset? from, DateTimeOffset? toDate, CancellationToken ct = default);
    Task<PagedResult<StockMovementView>> MovementsAsync(StockMovementFilter filter, CancellationToken ct = default);
    Task<InventoryMovementResult> AdjustAsync(StockAdjustmentRequest request, CancellationToken ct = default);
}
