using Forjix.Application.Abstractions.Authorization;
using Forjix.Application.Abstractions.Identity;
using Forjix.Application.Abstractions.Quotes;
using Forjix.Application.Abstractions.Sales;
using Forjix.Application.Abstractions.Tenancy;
using Forjix.Application.Common;
using Forjix.Application.Features.Quotes;
using Forjix.Application.Features.Sales;
using Forjix.Application.Features.Settings;
using Forjix.Domain.Enums;
namespace Forjix.Application.Tests;
public sealed class QuoteServiceTests
{
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);
    private static SaveQuoteRequest Request() => new(Guid.NewGuid(), Today.AddDays(10), 0, " Nota ", [new(Guid.NewGuid(), 2)]);
    private static QuoteView View() => new(Guid.NewGuid(), "ORC-000001", Guid.NewGuid(), "Cliente", Today, Today.AddDays(10), "Approved", null, 20, 0, 20, Guid.NewGuid(), "Vendedor", null, null, Convert.ToBase64String(new byte[8]), []);
    [Fact] public async Task CreationUsesAuthenticatedTenantAndNormalizedNotes()
    {
        var factory = new Factory(); var current = new Current();
        await Service(factory, current).SaveAsync(null, Request());
        Assert.Equal(current.TenantId, factory.Tenant?.TenantId); Assert.Equal("Nota", factory.Store.Request?.Notes);
    }
    [Fact] public async Task InvalidItemsAndDatesAreRejectedBeforeOpeningDatabase()
    {
        var factory = new Factory(); var service = Service(factory); var r = Request();
        await Assert.ThrowsAsync<RequestValidationException>(() => service.SaveAsync(null, r with { Items = [] }));
        await Assert.ThrowsAsync<RequestValidationException>(() => service.SaveAsync(null, r with { ValidUntil = Today.AddDays(-1) }));
        await Assert.ThrowsAsync<RequestValidationException>(() => service.SaveAsync(null, r with { Items = [new(Guid.NewGuid(), 0)] }));
        Assert.Null(factory.Tenant);
    }
    [Fact] public async Task EditRequiresCurrentRowVersion()
    {
        await Assert.ThrowsAsync<RequestValidationException>(() => Service(new Factory()).SaveAsync(Guid.NewGuid(), Request()));
    }
    [Fact] public async Task RejectionRequiresReasonAndSendForwardsTransition()
    {
        var factory = new Factory(); var service = Service(factory); var version = Convert.ToBase64String(new byte[8]);
        await Assert.ThrowsAsync<RequestValidationException>(() => service.TransitionAsync(Guid.NewGuid(), "reject", new(version)));
        await service.TransitionAsync(Guid.NewGuid(), "send", new(version));
        Assert.Equal(QuoteStatus.Sent, factory.Store.Transition);
    }
    [Fact] public async Task ConversionRequiresSalesPermissionAndDeferredTerms()
    {
        var factory = new Factory(); var service = Service(factory, allowed: false); var r = new ConvertQuoteRequest(Convert.ToBase64String(new byte[8]), "Pix");
        await Assert.ThrowsAsync<PermissionDeniedException>(() => service.ConvertAsync(Guid.NewGuid(), r));
        await Assert.ThrowsAsync<RequestValidationException>(() => service.ConvertAsync(Guid.NewGuid(), r with { PaymentMethod = "Deferred" }));
        Assert.False(factory.Store.Converted);
    }
    [Fact] public async Task ConversionCallsExistingStoreAndPropagatesTransactionalFailure()
    {
        var factory = new Factory(); var service = Service(factory); var r = new ConvertQuoteRequest(Convert.ToBase64String(new byte[8]), "Pix");
        await service.ConvertAsync(Guid.NewGuid(), r); Assert.True(factory.Store.Converted);
        factory.Store.Fail = true;
        await Assert.ThrowsAsync<ResourceConflictException>(() => service.ConvertAsync(Guid.NewGuid(), r));
    }
    [Fact] public async Task DiscountRequiresTheExistingSalesDiscountPermission()
    {
        await Assert.ThrowsAsync<PermissionDeniedException>(() => Service(new Factory(), allowed: false).SaveAsync(null, Request() with { Discount = 1 }));
    }
    [Fact] public void PdfSupportsMultiplePagesPortugueseAndPngLogo()
    {
        var q = View() with { Notes = "Observação final: preço válido.", Items = Enumerable.Range(1, 100).Select(i => new QuoteItemView(Guid.NewGuid(), Guid.NewGuid(), $"Produto {i}", "SKU", 2, 10, 0, 20)).ToArray() };
        var company = new TenantSettingsView("Empresa Exemplo", "Razão social", "12345678000199", "11999999999", null, "Rua Exemplo", false, "BRL", "America/Sao_Paulo", true);
        var logo = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");
        var text = System.Text.Encoding.Latin1.GetString(QuotePdf.Create(q, company, logo));
        Assert.StartsWith("%PDF-1.4", text); Assert.Contains("/Subtype /Image", text); Assert.Contains("Produto 100", text);
        Assert.Contains("Observação final", text);
        Assert.True(System.Text.RegularExpressions.Regex.Count(text, "/Type /Page ") > 1);
    }
    private static QuoteService Service(Factory factory, Current? current = null, bool allowed = true) => new(factory, new Resolver(), current ?? new Current(), new Checker(allowed), null!, null!, TimeProvider.System);
    private sealed class Current : ICurrentUser
    {
        public Guid? UserId { get; } = Guid.NewGuid(); public Guid? TenantId { get; } = Guid.NewGuid(); public string? TenantSlug => "test"; public bool IsAuthenticated => true; public string CorrelationId => "test"; public string? IpAddress => null;
    }
    private sealed class Resolver : ITenantDatabaseResolver
    {
        public Task<ResolvedTenantDatabase?> ResolveBySlugAsync(string slug, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ResolvedTenantDatabase?> ResolveByTenantIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<ResolvedTenantDatabase?>(new(id, "Empresa", "test", "isolated", "", "1", [], new Dictionary<string, string>()));
    }
    private sealed class Checker(bool allowed) : IPermissionChecker
    {
        public Task<bool> HasPermissionAsync(Guid tenantId, Guid userId, string permission, CancellationToken cancellationToken = default) => Task.FromResult(allowed);
    }
    private sealed class Factory : IQuoteStoreFactory
    {
        public ResolvedTenantDatabase? Tenant { get; private set; } public Store Store { get; } = new();
        public IQuoteStore Create(ResolvedTenantDatabase tenant) { Tenant = tenant; return Store; }
    }
    private sealed class Store : IQuoteStore
    {
        public SaveQuoteRequest? Request { get; private set; } public QuoteStatus? Transition { get; private set; } public bool Converted { get; private set; } public bool Fail { get; set; }
        public Task<QuoteView> SaveAsync(Guid? id, SaveQuoteRequest request, Guid user, SalesAuditContext audit, DateTimeOffset now, CancellationToken ct) { Request = request; return Task.FromResult(View()); }
        public Task<QuoteView?> GetAsync(Guid id, DateOnly today, CancellationToken ct) => Task.FromResult<QuoteView?>(View());
        public Task<QuoteView> TransitionAsync(Guid id, QuoteStatus targetStatus, byte[] version, string? reason, Guid user, SalesAuditContext audit, DateTimeOffset now, CancellationToken ct) { Transition = targetStatus; return Task.FromResult(View()); }
        public Task<SaleView> ConvertAsync(Guid id, ConvertQuoteRequest request, PaymentMethod method, byte[] version, bool allowNegativeStock, Guid user, SalesAuditContext audit, DateTimeOffset now, CancellationToken ct)
        {
            if (Fail) throw new ResourceConflictException("Rollback");
            Converted = true; return Task.FromResult(new SaleView(Guid.NewGuid(), "VD-EXEMPLO", "Completed", 20, 0, 20, method.ToString(), user, "Vendedor", Guid.NewGuid(), now, null, null, Convert.ToBase64String(version), []));
        }
        public Task<PagedQuotes> ListAsync(QuoteFilter filter, DateOnly today, CancellationToken ct) => throw new NotSupportedException();
        public Task<QuoteSummary> SummaryAsync(DateOnly today, CancellationToken ct) => throw new NotSupportedException();
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
