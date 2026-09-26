using Forjix.Application.Abstractions.Authorization;
using Forjix.Application.Abstractions.Identity;
using Forjix.Application.Abstractions.Quotes;
using Forjix.Application.Abstractions.Sales;
using Forjix.Application.Abstractions.Settings;
using Forjix.Application.Abstractions.Tenancy;
using Forjix.Application.Common;
using Forjix.Application.Features.Analytics;
using Forjix.Application.Features.Sales;
using Forjix.Domain.Entities.Quotes;
using Forjix.Domain.Enums;
namespace Forjix.Application.Features.Quotes;
internal sealed class QuoteService(IQuoteStoreFactory stores, ITenantDatabaseResolver tenants, ICurrentUser current, IPermissionChecker permissions, ISettingsStore settings, IFileStorage files, TimeProvider clock) : IQuoteService
{
    public async Task<PagedQuotes> ListAsync(QuoteFilter filter, CancellationToken ct = default)
    {
        if (filter.From > filter.Through) throw new RequestValidationException("Período inválido.");
        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            if (!Enum.TryParse<QuoteStatus>(filter.Status, true, out var status) || !Enum.IsDefined(status)) throw new RequestValidationException("Status inválido.");
            filter = filter with { Status = status.ToString() };
        }
        var (_, _, store) = await Store(ct); await using (store) return await store.ListAsync(filter with { Page = Math.Max(1, filter.Page), PageSize = Math.Clamp(filter.PageSize, 1, 100) }, Today(), ct);
    }
    public async Task<QuoteView> GetAsync(Guid id, CancellationToken ct = default)
    {
        var (_, _, store) = await Store(ct); await using (store) return await store.GetAsync(id, Today(), ct) ?? throw new ResourceNotFoundException("Orçamento não encontrado.");
    }
    public async Task<QuoteView> SaveAsync(Guid? id, SaveQuoteRequest request, CancellationToken ct = default)
    {
        if (request.CustomerId == Guid.Empty || request.Items is null || request.Items.Count is < 1 or > 200) throw new RequestValidationException("Informe cliente e de 1 a 200 itens.");
        if (request.ValidUntil == default || request.ValidUntil < Today()) throw new RequestValidationException("A validade não pode estar vencida.");
        if (request.Notes?.Length > 2000) throw new RequestValidationException("Observação deve ter até 2000 caracteres.");
        if (request.Items.Select(x => x.ProductId).Distinct().Count() != request.Items.Count || request.Items.Any(x => x.ProductId == Guid.Empty)) throw new RequestValidationException("Não repita produtos nem use referência inválida.");
        try
        {
            Quote.Money(request.Discount);
            foreach (var item in request.Items)
            {
                Quote.Money(item.Discount);
                if (item.Quantity <= 0 || item.Quantity > 999999999999999.999m || decimal.Round(item.Quantity, 3) != item.Quantity) throw new ArgumentException("Quantidade inválida.");
            }
        } catch (ArgumentException e) { throw new RequestValidationException(e.Message); }
        if (id.HasValue) _ = Version(request.RowVersion);
        var (tenant, user, store) = await Store(ct);
        await using (store)
        {
            if ((request.Discount > 0 || request.Items.Any(x => x.Discount > 0)) && !await permissions.HasPermissionAsync(tenant.TenantId, user, Permissions.SalesDiscount, ct))
                throw new PermissionDeniedException("Seu grupo não permite conceder descontos.");
            return await store.SaveAsync(id, request with { Notes = request.Notes?.Trim() }, user, Audit(), clock.GetUtcNow(), ct);
        }
    }
    public async Task<QuoteView> TransitionAsync(Guid id, string action, QuoteActionRequest request, CancellationToken ct = default)
    {
        var next = action switch { "send" => QuoteStatus.Sent, "approve" => QuoteStatus.Approved, "reject" => QuoteStatus.Rejected, "cancel" => QuoteStatus.Cancelled, _ => throw new RequestValidationException("Ação inválida.") };
        var reason = request.Reason?.Trim();
        if (reason?.Length > 500 || (next is QuoteStatus.Cancelled or QuoteStatus.Rejected && string.IsNullOrWhiteSpace(reason))) throw new RequestValidationException("Informe motivo com até 500 caracteres.");
        var version = Version(request.RowVersion);
        var (_, user, store) = await Store(ct); await using (store) return await store.TransitionAsync(id, next, version, reason, user, Audit(), clock.GetUtcNow(), ct);
    }
    public async Task<SaleView> ConvertAsync(Guid id, ConvertQuoteRequest request, CancellationToken ct = default)
    {
        var version = Version(request.RowVersion);
        if (!Enum.TryParse<PaymentMethod>(request.PaymentMethod, true, out var method) || !Enum.IsDefined(method)) throw new RequestValidationException("Pagamento inválido.");
        if (method == PaymentMethod.Deferred && request.FinancialTerms is null || method != PaymentMethod.Deferred && request.FinancialTerms is not null) throw new RequestValidationException("Informe condições financeiras somente para venda a prazo.");
        var (tenant, user, store) = await Store(ct);
        await using (store)
        {
            if (!await permissions.HasPermissionAsync(tenant.TenantId, user, Permissions.SalesCreate, ct)) throw new PermissionDeniedException("Conversão exige permissão para realizar vendas.");
            var quote = await store.GetAsync(id, Today(), ct) ?? throw new ResourceNotFoundException("Orçamento não encontrado.");
            if ((quote.Discount > 0 || quote.Items.Any(x => x.Discount > 0)) && !await permissions.HasPermissionAsync(tenant.TenantId, user, Permissions.SalesDiscount, ct))
                throw new PermissionDeniedException("A venda com desconto exige permissão para conceder descontos.");
            if (method == PaymentMethod.Deferred && !await permissions.HasPermissionAsync(tenant.TenantId, user, Permissions.FinancialReceivableManage, ct)) throw new PermissionDeniedException("Venda a prazo exige permissão financeira.");
            return await store.ConvertAsync(id, request, method, version, tenant.Settings.TryGetValue(TenantSettingKeys.AllowNegativeStock, out var flag) && bool.TryParse(flag, out var enabled) && enabled, user, Audit(), clock.GetUtcNow(), ct);
        }
    }
    public async Task<QuoteSummary> SummaryAsync(CancellationToken ct = default)
    {
        var (_, _, store) = await Store(ct); await using (store) return await store.SummaryAsync(Today(), ct);
    }
    public async Task<ExportedReport> PdfAsync(Guid id, CancellationToken ct = default)
    {
        var quote = await GetAsync(id, ct);
        var tenantId = current.TenantId!.Value;
        var company = await settings.GetAsync(tenantId, ct) ?? throw new ResourceNotFoundException("Empresa não encontrada.");
        byte[]? logo = null;
        var key = await settings.GetLogoKeyAsync(tenantId, ct);
        if (key is not null && await files.OpenAsync(key, ct) is { } file)
        {
            await using var stream = file.Content;
            using var buffer = new MemoryStream(); await stream.CopyToAsync(buffer, ct); logo = buffer.ToArray();
        }
        return new(QuotePdf.Create(quote, company, logo), "application/pdf", quote.Number + ".pdf");
    }
    private DateOnly Today() => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
    private SalesAuditContext Audit() => new(current.CorrelationId, current.IpAddress);
    private async Task<(ResolvedTenantDatabase Tenant, Guid User, IQuoteStore Store)> Store(CancellationToken ct)
    {
        if (current.UserId is not { } user || current.TenantId is not { } tenant) throw new ResourceNotFoundException("Contexto autenticado não encontrado.");
        var resolved = await tenants.ResolveByTenantIdAsync(tenant, ct) ?? throw new ResourceNotFoundException("Tenant não encontrado.");
        return (resolved, user, stores.Create(resolved));
    }
    private static byte[] Version(string? version)
    {
        try { var bytes = Convert.FromBase64String(version ?? ""); if (bytes.Length != 8) throw new FormatException(); return bytes; }
        catch (FormatException) { throw new RequestValidationException("Atualize o orçamento e informe sua versão."); }
    }
}
