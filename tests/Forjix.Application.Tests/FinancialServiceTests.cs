using Forjix.Application.Abstractions.Financial;
using Forjix.Application.Abstractions.Identity;
using Forjix.Application.Abstractions.Sales;
using Forjix.Application.Abstractions.Tenancy;
using Forjix.Application.Common;
using Forjix.Application.Features.Financial;
using Forjix.Domain.Enums;

namespace Forjix.Application.Tests;

public sealed class FinancialServiceTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(10, 121)]
    [InlineData(0.01, 2)]
    public async Task InvalidAccountsAreRejectedBeforeOpeningDatabase(decimal amount, int count)
    {
        var factory = new RecordingFactory();
        var service = new FinancialService(factory, new Resolver(), new Current(), TimeProvider.System);
        await Assert.ThrowsAsync<RequestValidationException>(() => service.CreateAsync(FinancialOperationType.Receivable, Request(amount, count)));
        Assert.Null(factory.Tenant);
    }
    [Fact]
    public async Task CreationUsesOnlyAuthenticatedTenantAndPreservesInstallmentContract()
    {
        var current = new Current(); var factory = new RecordingFactory();
        var service = new FinancialService(factory, new Resolver(), current, TimeProvider.System);
        await service.CreateAsync(FinancialOperationType.Payable, Request(1200, 3));
        Assert.Equal(current.TenantId, factory.Tenant?.TenantId);
        Assert.Equal(1200, factory.Store.Request?.Amount);
        Assert.Equal(3, factory.Store.Request?.Installments);
    }
    [Fact]
    public async Task PaymentRejectsFutureDateAndDeferredMethod()
    {
        var factory = new RecordingFactory();
        var service = new FinancialService(factory, new Resolver(), new Current(), TimeProvider.System);
        var request = new CreateFinancialPaymentRequest(10, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1), "Cash", 0, 0, 0, null, Convert.ToBase64String(new byte[8]));
        await Assert.ThrowsAsync<RequestValidationException>(() => service.PayAsync(FinancialOperationType.Receivable, Guid.NewGuid(), "key", request));
        await Assert.ThrowsAsync<RequestValidationException>(() => service.PayAsync(FinancialOperationType.Receivable, Guid.NewGuid(), "key", request with { PaymentMethod = "Deferred" }));
        Assert.Null(factory.Tenant);
    }
    [Fact]
    public async Task MissingTenantCannotCreateFinancialData()
    {
        var service = new FinancialService(new RecordingFactory(), new Resolver(), new Current { TenantId = null }, TimeProvider.System);
        await Assert.ThrowsAsync<ResourceNotFoundException>(() => service.CreateAsync(FinancialOperationType.Receivable, Request(100, 1)));
    }
    [Fact]
    public async Task ValidPaymentPreservesMoneyKeyVersionAndAuthenticatedTenant()
    {
        var current = new Current(); var factory = new RecordingFactory();
        var service = new FinancialService(factory, new Resolver(), current, TimeProvider.System);
        var id = Guid.NewGuid(); var version = Convert.ToBase64String(new byte[8]);
        var request = new CreateFinancialPaymentRequest(105, DateOnly.FromDateTime(DateTime.UtcNow), "Pix", 5, 8, 2, " Nota ", version);
        await service.PayAsync(FinancialOperationType.Receivable, id, " key ", request);
        Assert.Equal(current.TenantId, factory.Tenant?.TenantId);
        Assert.Equal("key", factory.Store.PaymentKey);
        Assert.Equal(105, factory.Store.Payment?.Amount);
        Assert.Equal("Nota", factory.Store.Payment?.Notes);
        Assert.Equal(version, factory.Store.Payment?.RowVersion);
        Assert.Equal(current.UserId, factory.Store.PaymentUser);
    }
    [Fact]
    public async Task InstallmentsCannotOverflowTheSupportedCalendar()
    {
        var factory = new RecordingFactory();
        var service = new FinancialService(factory, new Resolver(), new Current(), TimeProvider.System);
        await Assert.ThrowsAsync<RequestValidationException>(() => service.CreateAsync(FinancialOperationType.Receivable,
            Request(100, 2) with { DueDate = DateOnly.MaxValue }));
        Assert.Null(factory.Tenant);
    }
    private static SaveFinancialAccountRequest Request(decimal amount, int count) => new(null, Guid.NewGuid(), "Conta", null, amount, new(2026, 9, 26), new(2026, 10, 26), count, null, null);
    private sealed class Current : ICurrentUser
    {
        public Guid? UserId { get; } = Guid.NewGuid(); public Guid? TenantId { get; init; } = Guid.NewGuid();
        public string? TenantSlug => "test"; public bool IsAuthenticated => true; public string CorrelationId => "test"; public string? IpAddress => null;
    }
    private sealed class Resolver : ITenantDatabaseResolver
    {
        public Task<ResolvedTenantDatabase?> ResolveBySlugAsync(string slug, CancellationToken cancellationToken = default) => throw new NotSupportedException("Authenticated finance must not resolve a client slug.");
        public Task<ResolvedTenantDatabase?> ResolveByTenantIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<ResolvedTenantDatabase?>(new(id, "Test", "test", "isolated", "", "1", [], new Dictionary<string, string>()));
    }
    private sealed class RecordingFactory : IFinancialStoreFactory
    {
        public ResolvedTenantDatabase? Tenant { get; private set; } public RecordingStore Store { get; } = new();
        public IFinancialStore Create(ResolvedTenantDatabase tenant) { Tenant = tenant; return Store; }
    }
    private sealed class RecordingStore : IFinancialStore
    {
        public SaveFinancialAccountRequest? Request { get; private set; }
        public CreateFinancialPaymentRequest? Payment { get; private set; }
        public string? PaymentKey { get; private set; }
        public Guid? PaymentUser { get; private set; }
        public Task<IReadOnlyList<FinancialAccountView>> CreateAsync(FinancialOperationType type, SaveFinancialAccountRequest request, Guid user, SalesAuditContext audit, DateTimeOffset now, CancellationToken ct) { Request = request; return Task.FromResult<IReadOnlyList<FinancialAccountView>>([]); }
        public Task<IReadOnlyList<FinancialCategoryView>> CategoriesAsync(FinancialCategoryType? type, CancellationToken ct) => throw new NotSupportedException();
        public Task<FinancialCategoryView> SaveCategoryAsync(Guid? id, SaveFinancialCategoryRequest request, FinancialCategoryType type, Guid user, SalesAuditContext audit, DateTimeOffset now, CancellationToken ct) => throw new NotSupportedException();
        public Task<PagedFinancialAccounts> ListAsync(FinancialOperationType type, FinancialAccountFilter filter, DateOnly today, CancellationToken ct) => throw new NotSupportedException();
        public Task<FinancialAccountView?> GetAsync(FinancialOperationType type, Guid id, DateOnly today, CancellationToken ct) => throw new NotSupportedException();
        public Task<FinancialAccountView> UpdateAsync(FinancialOperationType type, Guid id, SaveFinancialAccountRequest request, byte[] version, Guid user, SalesAuditContext audit, DateTimeOffset now, CancellationToken ct) => throw new NotSupportedException();
        public Task<FinancialAccountView> CancelAsync(FinancialOperationType type, Guid id, string reason, byte[] version, Guid user, SalesAuditContext audit, DateTimeOffset now, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<FinancialPaymentView>> PaymentsAsync(FinancialOperationType type, Guid id, CancellationToken ct) => throw new NotSupportedException();
        public Task<FinancialAccountView> PayAsync(FinancialOperationType type, Guid id, string key, CreateFinancialPaymentRequest request, PaymentMethod method, byte[] version, Guid user, SalesAuditContext audit, DateTimeOffset now, CancellationToken ct)
        {
            Payment = request; PaymentKey = key; PaymentUser = user;
            return Task.FromResult(new FinancialAccountView(id, Guid.Empty, null, null, null, Guid.Empty, "Categoria", "Conta", null,
                100, 0, request.PaymentDate, request.PaymentDate, request.PaymentDate, "Paid", 1, 1, null, request.RowVersion));
        }
        public Task<FinancialAccountView> ReverseAsync(FinancialOperationType type, Guid id, Guid paymentId, string reason, byte[] version, Guid user, SalesAuditContext audit, DateTimeOffset now, CancellationToken ct) => throw new NotSupportedException();
        public Task<FinancialDashboard> DashboardAsync(DateOnly from, DateOnly through, DateOnly today, CancellationToken ct) => throw new NotSupportedException();
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
