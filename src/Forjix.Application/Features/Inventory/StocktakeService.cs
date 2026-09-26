using Forjix.Application.Abstractions.Identity;
using Forjix.Application.Abstractions.Inventory;
using Forjix.Application.Abstractions.Tenancy;
using Forjix.Application.Abstractions.Sales;
using Forjix.Application.Common;
using Forjix.Domain.Entities.Inventory;
using Forjix.Domain.Enums;
namespace Forjix.Application.Features.Inventory;

internal sealed class StocktakeService(ITenantDatabaseResolver tenants, IStocktakeStoreFactory stores, ICurrentUser current, TimeProvider clock, IInventoryService inventory) : IStocktakeService
{
    public async Task<PagedResult<StocktakeView>> ListAsync(StocktakeFilter filter, CancellationToken ct = default)
    {
        if (filter.From > filter.To || filter.Status is not null && filter.Status != "" && (!Enum.TryParse<StocktakeStatus>(filter.Status, out var status) || !Enum.IsDefined(status))) throw new RequestValidationException("Filtros inválidos.");
        var (_, store) = await Store(ct); await using (store) return await store.ListAsync(filter with { Page = Math.Max(1, filter.Page), PageSize = Math.Clamp(filter.PageSize, 1, 100) }, ct);
    }
    public async Task<StocktakeView> GetAsync(Guid id, CancellationToken ct = default)
    {
        var (_, store) = await Store(ct); await using (store) return await store.GetAsync(id, ct) ?? throw new ResourceNotFoundException("Inventário não encontrado.");
    }
    public async Task<StocktakeView> SaveAsync(Guid? id, SaveStocktakeRequest request, CancellationToken ct = default)
    {
        if (request.ProductIds is null || request.ProductIds.Count is < 1 or > 1000 || request.ProductIds.Any(x => x == Guid.Empty) || request.ProductIds.Distinct().Count() != request.ProductIds.Count || request.Notes?.Length > 2000) throw new RequestValidationException("Selecione de 1 a 1000 produtos distintos e observação de até 2000 caracteres.");
        if (id.HasValue) ValidateVersion(request.RowVersion);
        var (user, store) = await Store(ct); await using (store) return await store.SaveAsync(id, request with { Notes = request.Notes?.Trim() }, user, Audit(), clock.GetUtcNow(), ct);
    }
    public async Task<StocktakeView> ActionAsync(Guid id, string action, StocktakeActionRequest request, CancellationToken ct = default)
    {
        ValidateVersion(request.RowVersion);
        if (action is not ("start" or "complete" or "cancel")) throw new RequestValidationException("Ação inválida.");
        if (action == "cancel" && (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 500)) throw new RequestValidationException("Informe motivo do cancelamento até 500 caracteres.");
        var (user, store) = await Store(ct); await using (store) return await store.ActionAsync(id, action, request, user, Audit(), clock.GetUtcNow(), ct);
    }
    public async Task<StocktakeView> CountAsync(Guid id, CountStocktakeRequest request, CancellationToken ct = default)
    {
        ValidateVersion(request.RowVersion);
        if (request.Items is null || request.Items.Count is < 1 or > 1000 || request.Items.Select(x => x.ProductId).Distinct().Count() != request.Items.Count) throw new RequestValidationException("Informe contagens distintas.");
        try { foreach (var item in request.Items) { StocktakeItem.ValidateQuantity(item.Quantity); if (item.ProductId == Guid.Empty || item.Notes?.Length > 500) throw new ArgumentException("Item inválido."); } }
        catch (ArgumentException e) { throw new RequestValidationException(e.Message); }
        var (user, store) = await Store(ct); await using (store) return await store.CountAsync(id, request, user, Audit(), clock.GetUtcNow(), ct);
    }
    public async Task<StockOverview> OverviewAsync(CancellationToken ct = default)
    {
        var (_, store) = await Store(ct); await using (store) return await store.OverviewAsync(ct);
    }
    public async Task<IReadOnlyList<StockReportItem>> ReportAsync(bool lowStock, int days, DateTimeOffset? from, DateTimeOffset? toDate, CancellationToken ct = default)
    {
        if (!lowStock && from is null && days is not (30 or 60 or 90)) throw new RequestValidationException("Escolha 30, 60, 90 dias ou período personalizado.");
        var now = clock.GetUtcNow();
        var end = toDate.HasValue && toDate.Value < now ? toDate.Value : now; var begin = from ?? end.AddDays(-days);
        if (begin > end) throw new RequestValidationException("Período inválido.");
        var (_, store) = await Store(ct); await using (store) return await store.ReportAsync(lowStock, begin, end, ct);
    }
    public async Task<PagedResult<StockMovementView>> MovementsAsync(StockMovementFilter filter, CancellationToken ct = default)
    {
        if (filter.From > filter.To || !string.IsNullOrEmpty(filter.Type) && (!Enum.TryParse<InventoryMovementType>(filter.Type, out var type) || !Enum.IsDefined(type))) throw new RequestValidationException("Filtros inválidos.");
        var (_, store) = await Store(ct); await using (store) return await store.MovementsAsync(filter with { Page = Math.Max(1,filter.Page), PageSize = Math.Clamp(filter.PageSize,1,100) }, ct);
    }
    public Task<InventoryMovementResult> AdjustAsync(StockAdjustmentRequest request, CancellationToken ct = default)
    {
        if (request.ProductId == Guid.Empty || request.Type is not ("PositiveAdjustment" or "NegativeAdjustment") || !Enum.TryParse<StockAdjustmentReason>(request.Reason, out var reason) || !Enum.IsDefined(reason) || reason == StockAdjustmentReason.Inventory || request.Observation?.Length > 500) throw new RequestValidationException("Informe produto, entrada/saída e motivo válido. Inventário é gerado somente pela finalização.");
        ValidateVersion(request.RowVersion);
        return inventory.CreateMovementAsync(request.ProductId, new(request.Type, request.Quantity, request.Reason, request.RowVersion, request.Reason, request.Observation), ct);
    }
    private async Task<(Guid User, IStocktakeStore Store)> Store(CancellationToken ct)
    {
        if (current.TenantId is not { } tenant || current.UserId is not { } user) throw new ResourceNotFoundException("Contexto autenticado não encontrado.");
        var resolved = await tenants.ResolveByTenantIdAsync(tenant,ct) ?? throw new ResourceNotFoundException("Tenant não encontrado.");
        return (user,stores.Create(resolved));
    }
    private SalesAuditContext Audit() => new(current.CorrelationId,current.IpAddress);
    private static void ValidateVersion(string? value)
    {
        try { if (Convert.FromBase64String(value ?? "").Length != 8) throw new FormatException(); }
        catch (FormatException) { throw new RequestValidationException("Atualize o registro e informe sua versão."); }
    }
}
