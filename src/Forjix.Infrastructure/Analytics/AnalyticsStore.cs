using System.Globalization;
using Forjix.Application.Abstractions.Analytics;
using Forjix.Application.Abstractions.Tenancy;
using Forjix.Application.Features.Analytics;
using Forjix.Domain.Enums;
using Forjix.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;

namespace Forjix.Infrastructure.Analytics;

internal sealed class AnalyticsStoreFactory(ITenantDbContextFactory contexts) : IAnalyticsStoreFactory
{
    public IAnalyticsStore Create(ResolvedTenantDatabase tenant) => new AnalyticsStore(contexts.Create(tenant));
}

internal sealed class AnalyticsStore(TenantDbContext db) : IAnalyticsStore
{
    public async Task<DashboardView> DashboardAsync(DateTimeOffset now, CancellationToken ct)
    {
        var today = new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, TimeSpan.Zero);
        var week = today.AddDays(-6);
        var sales = await db.Sales.AsNoTracking().Where(x => x.Status == SaleStatus.Completed && x.CreatedAt >= week)
            .Select(x => new { x.Total, x.CreatedAt }).ToListAsync(ct);
        var todaySales = sales.Where(x => x.CreatedAt >= today).ToList();
        var active = await db.Products.CountAsync(x => x.IsActive, ct);
        var cost = await db.Inventories.Where(x => x.Product.IsActive).SumAsync(x => (decimal?)(x.Quantity * x.Product.CostPrice), ct) ?? 0;
        var lowQuery = db.Inventories.AsNoTracking().Where(x => x.Product.IsActive && x.Quantity <= x.Product.MinimumStock);
        var lowCount = await lowQuery.CountAsync(ct);
        var low = await lowQuery.OrderBy(x => x.Quantity - x.Product.MinimumStock).Take(8)
            .Select(x => new LowStockItem(x.ProductId, x.Product.Name, x.Product.Sku, x.Quantity, x.Product.MinimumStock)).ToListAsync(ct);
        var recentRows = await db.Sales.AsNoTracking().OrderByDescending(x => x.CreatedAt).Take(8)
            .Select(x => new { x.Id, x.Number, x.Total, x.PaymentMethod, x.Status, x.CreatedAt }).ToListAsync(ct);
        var recent = recentRows.Select(x => new RecentSale(x.Id, x.Number, x.Total, x.PaymentMethod.ToString(), x.Status.ToString(), x.CreatedAt)).ToList();
        var topRows = await db.SaleItems.AsNoTracking().Where(x => x.Sale.Status == SaleStatus.Completed && x.Sale.CreatedAt >= week)
            .Select(x => new { x.ProductId, x.ProductName, x.Quantity, x.Total }).ToListAsync(ct);
        var top = topRows.GroupBy(x => new { x.ProductId, x.ProductName }).Select(g => new TopProduct(g.Key.ProductId, g.Key.ProductName, g.Sum(x => x.Quantity), g.Sum(x => x.Total)))
            .OrderByDescending(x => x.Quantity).Take(8).ToList();
        var openCashId = await db.CashSessions.AsNoTracking().Where(x => x.Status == CashSessionStatus.Open).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);
        decimal? cash = null;
        if (openCashId.HasValue)
        {
            var movements = await db.CashMovements.AsNoTracking().Where(x => x.CashSessionId == openCashId.Value).Select(x => new { x.Type, x.Amount }).ToListAsync(ct);
            cash = movements.Sum(x => x.Type == CashMovementType.Withdrawal ? -x.Amount : x.Amount);
        }
        var points = Enumerable.Range(0, 7).Select(i => week.AddDays(i)).Select(day => new MetricPoint(
            day.ToString("dd/MM", CultureInfo.InvariantCulture), sales.Where(x => x.CreatedAt >= day && x.CreatedAt < day.AddDays(1)).Sum(x => x.Total))).ToList();
        return new(todaySales.Sum(x => x.Total), todaySales.Count, todaySales.Count == 0 ? 0 : todaySales.Average(x => x.Total), active, cost, lowCount, cash, points, low, recent, top);
    }

    public async Task<ReportView> ReportAsync(DateTimeOffset from, DateTimeOffset through, CancellationToken ct)
    {
        var saleRows = await db.Sales.AsNoTracking().Where(x => x.Status == SaleStatus.Completed && x.CreatedAt >= from && x.CreatedAt <= through)
            .Select(x => new { x.Total, x.PaymentMethod }).ToListAsync(ct);
        var revenue = saleRows.Sum(x => x.Total);
        var count = saleRows.Count;
        var payments = saleRows.GroupBy(x => x.PaymentMethod).Select(g => new PaymentSummary(g.Key.ToString(), g.Count(), g.Sum(x => x.Total))).ToList();
        var soldRows = await db.SaleItems.AsNoTracking().Where(x => x.Sale.Status == SaleStatus.Completed && x.Sale.CreatedAt >= from && x.Sale.CreatedAt <= through)
            .Select(x => new { x.ProductId, x.ProductName, x.Quantity, x.Total }).ToListAsync(ct);
        var top = soldRows.GroupBy(x => new { x.ProductId, x.ProductName }).Select(g => new TopProduct(g.Key.ProductId, g.Key.ProductName, g.Sum(x => x.Quantity), g.Sum(x => x.Total)))
            .OrderByDescending(x => x.Quantity).Take(25).ToList();
        var stockRows = await db.Inventories.AsNoTracking().Where(x => x.Product.IsActive).OrderBy(x => x.Product.Name)
            .Select(x => new { x.ProductId, Product = x.Product.Name, x.Product.Sku, x.Quantity, x.Product.MinimumStock, x.Product.CostPrice }).ToListAsync(ct);
        var inventory = stockRows.Select(x => new InventoryReportItem(x.Product, x.Sku, x.Quantity, x.MinimumStock, x.Quantity * x.CostPrice,
            x.Quantity < 0 ? "Negative" : x.Quantity == 0 ? "OutOfStock" : x.Quantity <= x.MinimumStock ? "Low" : "Normal")).ToList();
        var low = stockRows.Where(x => x.Quantity <= x.MinimumStock).Select(x => new LowStockItem(x.ProductId, x.Product, x.Sku, x.Quantity, x.MinimumStock)).ToList();
        var purchaseQuery = db.Purchases.AsNoTracking().Where(x => x.CreatedAt >= from && x.CreatedAt <= through);
        var receivedPurchaseTotals = await db.Purchases.AsNoTracking().Where(x => x.Status == PurchaseStatus.Received && x.ReceivedAt >= from && x.ReceivedAt <= through).Select(x => x.Total).ToListAsync(ct);
        var purchaseRows = await purchaseQuery
            .OrderByDescending(x => x.CreatedAt).Select(x => new { x.Number, Supplier = x.Supplier.Name, x.Total, x.Status, x.CreatedAt, x.ReceivedAt }).Take(100).ToListAsync(ct);
        var purchases = purchaseRows.Select(x => new PurchaseReportItem(x.Number, x.Supplier, x.Total, x.Status.ToString(), x.CreatedAt, x.ReceivedAt)).ToList();
        var movementQuery = db.InventoryMovements.AsNoTracking().Where(x => x.CreatedAt >= from && x.CreatedAt <= through);
        var movementCount = await movementQuery.CountAsync(ct);
        var movementRows = await movementQuery
            .OrderByDescending(x => x.CreatedAt).Select(x => new { x.CreatedAt, Product = x.Product.Name, x.Product.Sku, x.Type, x.Quantity, x.PreviousQuantity, x.NewQuantity, UserName = x.User.Name, x.Reason }).Take(100).ToListAsync(ct);
        var movements = movementRows.Select(x => new StockMovementReportItem(x.CreatedAt, x.Product, x.Sku, x.Type.ToString(), x.Quantity, x.PreviousQuantity, x.NewQuantity, x.UserName, x.Reason)).ToList();
        return new(from, through, revenue, count, count == 0 ? 0 : revenue / count, receivedPurchaseTotals.Sum(), receivedPurchaseTotals.Count, movementCount, payments, top, inventory, low, purchases, movements);
    }

    public ValueTask DisposeAsync() => db.DisposeAsync();
}
