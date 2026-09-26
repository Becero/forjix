using Forjix.Application.Abstractions.Financial;
using Forjix.Application.Abstractions.Identity;
using Forjix.Application.Abstractions.Sales;
using Forjix.Application.Abstractions.Tenancy;
using Forjix.Application.Common;
using Forjix.Domain.Entities.Financial;
using Forjix.Domain.Enums;

namespace Forjix.Application.Features.Financial;

internal sealed class FinancialService(IFinancialStoreFactory stores, ITenantDatabaseResolver tenants, ICurrentUser current, TimeProvider clock) : IFinancialService
{
    public async Task<IReadOnlyList<FinancialCategoryView>> CategoriesAsync(string? type, CancellationToken ct = default)
    {
        var parsed = string.IsNullOrWhiteSpace(type) ? (FinancialCategoryType?)null : Parse<FinancialCategoryType>(type);
        var (_, store) = await Store(ct); await using (store) return await store.CategoriesAsync(parsed, ct);
    }

    public async Task<FinancialCategoryView> SaveCategoryAsync(Guid? id, SaveFinancialCategoryRequest request, CancellationToken ct = default)
    {
        var name = Text(request.Name, 120, true)!;
        var type = Parse<FinancialCategoryType>(request.Type);
        var (user, store) = await Store(ct);
        await using (store) return await store.SaveCategoryAsync(id, request with { Name = name }, type, user, Audit(), clock.GetUtcNow(), ct);
    }

    public async Task<PagedFinancialAccounts> ListAsync(FinancialOperationType type, FinancialAccountFilter filter, CancellationToken ct = default)
    {
        if (filter.From > filter.Through) throw new RequestValidationException("Período inválido.");
        if (!string.IsNullOrWhiteSpace(filter.Status)) filter = filter with { Status = Parse<FinancialAccountStatus>(filter.Status).ToString() };
        var (_, store) = await Store(ct);
        await using (store) return await store.ListAsync(type, filter with { Page = Math.Max(1, filter.Page), PageSize = Math.Clamp(filter.PageSize, 1, 100) }, Today(), ct);
    }

    public async Task<FinancialAccountView> GetAsync(FinancialOperationType type, Guid id, CancellationToken ct = default)
    {
        var (_, store) = await Store(ct); await using (store) return await store.GetAsync(type, id, Today(), ct) ?? throw new ResourceNotFoundException("Conta não encontrada.");
    }

    public async Task<IReadOnlyList<FinancialAccountView>> CreateAsync(FinancialOperationType type, SaveFinancialAccountRequest request, CancellationToken ct = default)
    {
        var clean = Validate(request);
        var (user, store) = await Store(ct); await using (store) return await store.CreateAsync(type, clean, user, Audit(), clock.GetUtcNow(), ct);
    }

    public async Task<FinancialAccountView> UpdateAsync(FinancialOperationType type, Guid id, SaveFinancialAccountRequest request, CancellationToken ct = default)
    {
        var clean = Validate(request);
        var version = Version(request.RowVersion);
        var (user, store) = await Store(ct); await using (store) return await store.UpdateAsync(type, id, clean, version, user, Audit(), clock.GetUtcNow(), ct);
    }

    public async Task<FinancialAccountView> CancelAsync(FinancialOperationType type, Guid id, FinancialActionRequest request, CancellationToken ct = default)
    {
        var reason = Text(request.Reason, 500, true)!; var version = Version(request.RowVersion);
        var (user, store) = await Store(ct); await using (store) return await store.CancelAsync(type, id, reason, version, user, Audit(), clock.GetUtcNow(), ct);
    }

    public async Task<IReadOnlyList<FinancialPaymentView>> PaymentsAsync(FinancialOperationType type, Guid id, CancellationToken ct = default)
    {
        var (_, store) = await Store(ct); await using (store) return await store.PaymentsAsync(type, id, ct);
    }

    public async Task<FinancialAccountView> PayAsync(FinancialOperationType type, Guid id, string key, CreateFinancialPaymentRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length > 100) throw new RequestValidationException("Informe Idempotency-Key de até 100 caracteres.");
        try { _ = FinancialRules.Principal(request.Amount, request.Discount, request.Interest, request.Penalty); }
        catch (ArgumentException e) { throw new RequestValidationException(e.Message); }
        var method = Parse<PaymentMethod>(request.PaymentMethod);
        if (method == PaymentMethod.Deferred) throw new RequestValidationException("Uma baixa não pode ser a prazo.");
        if (request.PaymentDate == default || request.PaymentDate > Today()) throw new RequestValidationException("Informe uma data de pagamento válida, sem data futura.");
        var clean = request with { Notes = Text(request.Notes, 1000) };
        var version = Version(request.RowVersion);
        var (user, store) = await Store(ct); await using (store) return await store.PayAsync(type, id, key.Trim(), clean, method, version, user, Audit(), clock.GetUtcNow(), ct);
    }

    public async Task<FinancialAccountView> ReverseAsync(FinancialOperationType type, Guid id, Guid paymentId, FinancialActionRequest request, CancellationToken ct = default)
    {
        var reason = Text(request.Reason, 500, true)!; var version = Version(request.RowVersion);
        var (user, store) = await Store(ct); await using (store) return await store.ReverseAsync(type, id, paymentId, reason, version, user, Audit(), clock.GetUtcNow(), ct);
    }

    public async Task<FinancialDashboard> DashboardAsync(DateOnly? from, DateOnly? through, CancellationToken ct = default)
    {
        var today = Today(); var start = from ?? new DateOnly(today.Year, today.Month, 1); var end = through ?? today;
        if (start > end) throw new RequestValidationException("Período inválido.");
        var (_, store) = await Store(ct); await using (store) return await store.DashboardAsync(start, end, today, ct);
    }

    private DateOnly Today() => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
    private SalesAuditContext Audit() => new(current.CorrelationId, current.IpAddress);
    private async Task<(Guid User, IFinancialStore Store)> Store(CancellationToken ct)
    {
        if (current.TenantId is not { } tenantId || current.UserId is not { } user) throw new ResourceNotFoundException("Contexto autenticado não encontrado.");
        var tenant = await tenants.ResolveByTenantIdAsync(tenantId, ct) ?? throw new ResourceNotFoundException("Tenant não encontrado.");
        return (user, stores.Create(tenant));
    }
    private static SaveFinancialAccountRequest Validate(SaveFinancialAccountRequest r)
    {
        try { _ = FinancialRules.Split(r.Amount, r.Installments); } catch (ArgumentException e) { throw new RequestValidationException(e.Message); }
        if (r.DueDate == default || r.IssueDate == default || r.DueDate < r.IssueDate) throw new RequestValidationException("Informe emissão e vencimento válidos.");
        try { _ = r.DueDate.AddMonths(r.Installments - 1); }
        catch (ArgumentOutOfRangeException) { throw new RequestValidationException("Os vencimentos ultrapassam o intervalo de datas suportado."); }
        if (r.FinancialCategoryId == Guid.Empty || r.PartyId == Guid.Empty) throw new RequestValidationException("Referência inválida.");
        return r with { Description = Text(r.Description, 200, true)!, Document = Text(r.Document, 100), Notes = Text(r.Notes, 1000) };
    }
    private static string? Text(string? value, int max, bool required = false)
    {
        var clean = value?.Trim();
        if (string.IsNullOrEmpty(clean)) { if (required) throw new RequestValidationException("Preencha os campos obrigatórios."); return null; }
        if (clean.Length > max) throw new RequestValidationException($"O texto deve ter até {max} caracteres.");
        return clean;
    }
    private static T Parse<T>(string value) where T : struct, Enum => Enum.TryParse<T>(value, true, out var parsed) && Enum.IsDefined(parsed) ? parsed : throw new RequestValidationException("Tipo ou status inválido.");
    private static byte[] Version(string? value)
    {
        try { var bytes = Convert.FromBase64String(value ?? ""); if (bytes.Length != 8) throw new FormatException(); return bytes; }
        catch (FormatException) { throw new RequestValidationException("A versão atual é obrigatória. Atualize a conta."); }
    }
}
