using System.Data;
using System.Text.Json;
using Forjix.Application.Abstractions.Inventory;
using Forjix.Application.Abstractions.Tenancy;
using Forjix.Application.Abstractions.Sales;
using Forjix.Application.Common;
using Forjix.Application.Features.Inventory;
using Forjix.Domain.Entities.Inventory;
using Forjix.Domain.Entities.Audit;
using Forjix.Domain.Enums;
using Forjix.Infrastructure.Persistence.Tenant;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
namespace Forjix.Infrastructure.Inventory;

internal sealed class StocktakeStoreFactory(ITenantDbContextFactory contexts) : IStocktakeStoreFactory
{
    public IStocktakeStore Create(ResolvedTenantDatabase tenant) => new StocktakeStore(contexts.Create(tenant));
}
internal sealed class StocktakeStore(TenantDbContext db) : IStocktakeStore
{
    private IQueryable<Stocktake> Query() => db.Stocktakes.Include(x => x.Items).ThenInclude(x => x.Product);
    public async Task<StocktakeView?> GetAsync(Guid id, CancellationToken ct)
    {
        var item = await Query().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        return item is null ? null : await Map(item,ct);
    }
    public async Task<PagedResult<StocktakeView>> ListAsync(StocktakeFilter filter, CancellationToken ct)
    {
        var q = Query().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(filter.Search)) q = q.Where(x => x.Number.Contains(filter.Search));
        if (Enum.TryParse<StocktakeStatus>(filter.Status,out var status)) q = q.Where(x => x.Status == status);
        if (filter.From.HasValue) q = q.Where(x => x.OpenedAt >= filter.From);
        if (filter.To.HasValue) q = q.Where(x => x.OpenedAt <= filter.To);
        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(x => x.OpenedAt).ThenByDescending(x => x.Number).Skip((filter.Page-1)*filter.PageSize).Take(filter.PageSize).ToListAsync(ct);
        var result = new List<StocktakeView>();
        foreach (var item in items) result.Add(await Map(item,ct));
        return new(result,filter.Page,filter.PageSize,total);
    }
    public Task<StocktakeView> SaveAsync(Guid? id, SaveStocktakeRequest request, Guid user, SalesAuditContext audit, DateTimeOffset now, CancellationToken ct) => Write(async () =>
    {
        Stocktake item;
        if (id.HasValue) { item = await Load(id.Value,request.RowVersion!,ct); item.EnsureDraft(); }
        else
        {
            var sequence = await db.StocktakeSequences.SingleOrDefaultAsync(x => x.Id == 1,ct);
            if (sequence is null) { sequence = new(); db.StocktakeSequences.Add(sequence); }
            sequence.LastValue++;
            item = new() { Id = Guid.NewGuid(), Number = $"INV-{sequence.LastValue:000000}", OpenedAt = now, UserId = user };
            db.Stocktakes.Add(item);
        }
        var stocks = await db.Inventories.Include(x => x.Product).Where(x => request.ProductIds.Contains(x.ProductId) && x.Product.IsActive).ToListAsync(ct);
        if (stocks.Count != request.ProductIds.Count) throw new RequestValidationException("Produto inexistente, inativo ou sem estoque cadastrado.");
        db.StocktakeItems.RemoveRange(item.Items); item.Items.Clear();
        foreach (var stock in stocks)
        {
            var detail = new StocktakeItem { Id = Guid.NewGuid(), ProductId = stock.ProductId, ExpectedQuantity = stock.Quantity, StockRowVersion = stock.RowVersion };
            db.StocktakeItems.Add(detail); item.Items.Add(detail);
        }
        item.Notes = request.Notes; Touch(item,user,now);
        Audit(id.HasValue ? AuditAction.StocktakeUpdated : AuditAction.StocktakeCreated,item,user,audit,now,new { item.Number, Items = item.Items.Count });
        await db.SaveChangesAsync(ct); return await Map(await Query().SingleAsync(x => x.Id == item.Id,ct),ct);
    },ct);
    public Task<StocktakeView> ActionAsync(Guid id, string action, StocktakeActionRequest request, Guid user, SalesAuditContext audit, DateTimeOffset now, CancellationToken ct) => Write(async () =>
    {
        var item = await Load(id,request.RowVersion,ct);
        if (action == "start")
        {
            item.Start(now);
            var ids = item.Items.Select(x => x.ProductId).ToArray();
            var stocks = await db.Inventories.Include(x => x.Product).Where(x => ids.Contains(x.ProductId)).ToDictionaryAsync(x => x.ProductId,ct);
            foreach (var detail in item.Items)
            {
                var stock = stocks[detail.ProductId];
                if (!stock.Product.IsActive) throw new RequestValidationException("Produto inativo.");
                detail.ExpectedQuantity = stock.Quantity; detail.StockRowVersion = stock.RowVersion;
            }
        }
        else if (action == "complete")
        {
            item.EnsureCounting();
            if (item.Items.Any(x => x.CountedQuantity is null)) throw new ResourceConflictException("Conte todos os produtos antes de finalizar.");
            var ids = item.Items.Select(x => x.ProductId).ToArray();
            var stocks = await db.Inventories.Include(x => x.Product).Where(x => ids.Contains(x.ProductId)).ToDictionaryAsync(x => x.ProductId,ct);
            foreach (var detail in item.Items)
            {
                if (!stocks.TryGetValue(detail.ProductId,out var stock) || !stock.Product.IsActive) throw new ResourceConflictException("Produto inválido ou inativo.");
                if (!stock.RowVersion.SequenceEqual(detail.StockRowVersion)) throw new ResourceConflictException("Houve movimentação após a contagem. Reconte os produtos antes de finalizar.");
            }
            // Validate all versions before generating any incremental adjustment.
            foreach (var detail in item.Items)
            {
                var delta = detail.Difference!.Value;
                if (delta == 0) continue;
                var stock = stocks[detail.ProductId];
                var type = delta > 0 ? InventoryMovementType.PositiveAdjustment : InventoryMovementType.NegativeAdjustment;
                var change = stock.ApplyMovement(type,Math.Abs(delta),false,now);
                db.InventoryMovements.Add(new InventoryMovement { InventoryId = stock.Id, ProductId = stock.ProductId, Type = type, Quantity = Math.Abs(delta), PreviousQuantity = change.PreviousQuantity, NewQuantity = change.NewQuantity, Reason = "Finalização de inventário " + item.Number, ReasonCode = StockAdjustmentReason.Inventory.ToString(), Observation = detail.Notes, ReferenceType = "Stocktake", ReferenceId = item.Id.ToString(), UserId = user, CreatedAt = now });
            }
            item.Complete(now);
        }
        else item.Cancel(now);
        Touch(item,user,now);
        Audit(action == "start" ? AuditAction.StocktakeStarted : action == "complete" ? AuditAction.StocktakeCompleted : AuditAction.StocktakeCancelled,item,user,audit,now,new { item.Number, Reason = request.Reason, Differences = item.Items.Select(x => new { x.ProductId,x.Difference }) });
        await db.SaveChangesAsync(ct); return await Map(item,ct);
    },ct);
    public Task<StocktakeView> CountAsync(Guid id, CountStocktakeRequest request, Guid user, SalesAuditContext audit, DateTimeOffset now, CancellationToken ct) => Write(async () =>
    {
        var item = await Load(id,request.RowVersion,ct); item.EnsureCounting();
        var ids = request.Items.Select(x => x.ProductId).ToArray();
        if (ids.Any(x => !item.Items.Any(i => i.ProductId == x))) throw new RequestValidationException("Produto não pertence a este inventário.");
        var stocks = await db.Inventories.Include(x => x.Product).Where(x => ids.Contains(x.ProductId)).ToDictionaryAsync(x => x.ProductId,ct);
        foreach (var count in request.Items)
        {
            var stock = stocks[count.ProductId];
            if (!stock.Product.IsActive) throw new RequestValidationException("Produto inativo.");
            var detail = item.Items.Single(x => x.ProductId == count.ProductId);
            // Each submitted count explicitly establishes its current stock baseline.
            detail.ExpectedQuantity = stock.Quantity; detail.StockRowVersion = stock.RowVersion;
            detail.Count(count.Quantity,count.Notes,user,now);
        }
        Touch(item,user,now); Audit(AuditAction.StocktakeCounted,item,user,audit,now,new { Items = request.Items });
        await db.SaveChangesAsync(ct); return await Map(item,ct);
    },ct);
    public async Task<StockOverview> OverviewAsync(CancellationToken ct)
    {
        var q = db.Inventories.AsNoTracking().Where(x => x.Product.IsActive);
        return new(await q.CountAsync(ct),await q.CountAsync(x => x.Quantity <= 0,ct),
            await q.CountAsync(x => x.Quantity > 0 && x.Quantity <= x.Product.MinimumStock,ct),
            await q.CountAsync(x => x.Product.MinimumStock > 0 && x.Quantity > x.Product.MinimumStock && x.Quantity <= x.Product.MinimumStock * 1.2m,ct),
            await db.Stocktakes.CountAsync(x => x.Status == StocktakeStatus.Counting,ct),
            await q.SumAsync(x => x.Quantity * x.Product.CostPrice,ct));
    }
    public async Task<IReadOnlyList<StockReportItem>> ReportAsync(bool lowStock, DateTimeOffset from, DateTimeOffset toDate, CancellationToken ct)
    {
        var q = db.Inventories.AsNoTracking().Where(x => x.Product.IsActive);
        if (lowStock) q = q.Where(x => x.Quantity <= x.Product.MinimumStock);
        else q = q.Where(x => x.Product.CreatedAt <= from && !x.Movements.Any(m => m.CreatedAt >= from && m.CreatedAt <= toDate));
        return await q.OrderBy(x => x.Product.Name).Select(x => new StockReportItem(x.ProductId,x.Product.Name,x.Product.Sku,x.Quantity,x.Product.MinimumStock,x.Quantity < x.Product.MinimumStock ? x.Product.MinimumStock-x.Quantity : 0,x.Movements.OrderByDescending(m => m.CreatedAt).Select(m => (DateTimeOffset?)m.CreatedAt).FirstOrDefault(),x.Quantity*x.Product.CostPrice)).ToListAsync(ct);
    }
    public async Task<PagedResult<StockMovementView>> MovementsAsync(StockMovementFilter filter, CancellationToken ct)
    {
        var q = db.InventoryMovements.AsNoTracking();
        if (filter.ProductId.HasValue) q = q.Where(x => x.ProductId == filter.ProductId);
        if (filter.From.HasValue) q = q.Where(x => x.CreatedAt >= filter.From);
        if (filter.To.HasValue) q = q.Where(x => x.CreatedAt <= filter.To);
        if (Enum.TryParse<InventoryMovementType>(filter.Type,out var type)) q = q.Where(x => x.Type == type);
        if (!string.IsNullOrWhiteSpace(filter.Origin)) q = q.Where(x => x.ReferenceType == filter.Origin);
        if (!string.IsNullOrWhiteSpace(filter.User)) q = q.Where(x => x.User.Name.Contains(filter.User));
        var total = await q.CountAsync(ct);
        var data = await q.Include(x => x.Product).Include(x => x.User).OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).Skip((filter.Page-1)*filter.PageSize).Take(filter.PageSize).ToListAsync(ct);
        var ids = data.Where(x => x.ReferenceId != null).Select(x => x.ReferenceId!).Distinct().ToArray();
        var sales = await db.Sales.Where(x => ids.Contains(x.Id.ToString())).ToDictionaryAsync(x => x.Id.ToString(),x => x.Number,ct);
        var purchases = await db.Purchases.Where(x => ids.Contains(x.Id.ToString())).ToDictionaryAsync(x => x.Id.ToString(),x => x.Number,ct);
        var counts = await db.Stocktakes.Where(x => ids.Contains(x.Id.ToString())).ToDictionaryAsync(x => x.Id.ToString(),x => x.Number,ct);
        var results = data.Select(x =>
        {
            var origin = x.ReferenceType ?? "Other";
            var dictionary = origin == "Sale" ? sales : origin == "Purchase" ? purchases : counts;
            var number = x.ReferenceId is not null && dictionary.TryGetValue(x.ReferenceId,out var found) ? found : null;
            return new StockMovementView(x.Id,x.ProductId,x.Product.Name,x.Product.Sku,x.Type.ToString(),x.NewQuantity > x.PreviousQuantity ? x.Quantity : 0,x.NewQuantity < x.PreviousQuantity ? x.Quantity : 0,x.PreviousQuantity,x.NewQuantity,origin,x.ReferenceId,number,x.UserId,x.User.Name,x.CreatedAt,x.Reason,x.ReasonCode,x.Observation);
        }).ToArray();
        return new(results,filter.Page,filter.PageSize,total);
    }
    private async Task<Stocktake> Load(Guid id,string version,CancellationToken ct)
    {
        var item = await Query().SingleOrDefaultAsync(x => x.Id == id,ct) ?? throw new ResourceNotFoundException("Inventário não encontrado.");
        var expected = Convert.FromBase64String(version);
        if (!item.RowVersion.SequenceEqual(expected)) throw new ResourceConflictException("Inventário alterado. Atualize a página.");
        db.Entry(item).Property(x => x.RowVersion).OriginalValue = expected; return item;
    }
    private async Task<StocktakeView> Map(Stocktake item,CancellationToken ct)
    {
        var ids = item.Items.Where(x => x.CountedByUserId.HasValue).Select(x => x.CountedByUserId!.Value).Append(item.UserId).Distinct().ToArray();
        var names = await db.Users.Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id,x => x.Name,ct);
        return new(item.Id,item.Number,item.Status.ToString(),item.Notes,item.UserId,names[item.UserId],item.OpenedAt,item.StartedAt,item.ClosedAt,Convert.ToBase64String(item.RowVersion),item.Items.OrderBy(x => x.Product.Name).Select(x => new StocktakeItemView(x.ProductId,x.Product.Name,x.Product.Sku,x.ExpectedQuantity,x.CountedQuantity,x.Difference,x.Notes,x.CountedAt,x.CountedByUserId,x.CountedByUserId.HasValue ? names[x.CountedByUserId.Value] : null)).ToArray());
    }
    private static void Touch(Stocktake item,Guid user,DateTimeOffset now) { item.UpdatedAt = now; item.UpdatedByUserId = user; }
    private void Audit(AuditAction action,Stocktake item,Guid user,SalesAuditContext ctx,DateTimeOffset now,object data) =>
        db.AuditLogs.Add(new AuditLog { Action = action, UserId = user, EntityName = nameof(Stocktake), EntityId = item.Id.ToString(), AfterData = JsonSerializer.Serialize(data), CorrelationId = ctx.CorrelationId, IpAddress = ctx.IpAddress, OccurredAt = now });
    private async Task<T> Write<T>(Func<Task<T>> action,CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        try { var value = await action(); await tx.CommitAsync(ct); return value; }
        catch (DbUpdateConcurrencyException) { throw new ResourceConflictException("Inventário ou estoque alterado. Reconte e tente novamente."); }
        catch (DbUpdateException) { throw new ResourceConflictException("Não foi possível gravar o inventário. Atualize e confira as referências."); }
        catch (SqlException e) when (e.Number == 1205) { throw new ResourceConflictException("Operação concorrente. Atualize e tente novamente."); }
        catch (InvalidOperationException e) { throw new ResourceConflictException(e.Message); }
        catch (ArgumentException e) { throw new RequestValidationException(e.Message); }
    }
    public ValueTask DisposeAsync() => db.DisposeAsync();
}
