using Forjix.Application.Abstractions.Sales;
using Forjix.Application.Abstractions.Tenancy;
using Forjix.Application.Features.Financial;
using Forjix.Domain.Enums;

namespace Forjix.Application.Abstractions.Financial;

public interface IFinancialStoreFactory { IFinancialStore Create(ResolvedTenantDatabase tenant); }
public interface IFinancialStore : IAsyncDisposable
{
    Task<IReadOnlyList<FinancialCategoryView>> CategoriesAsync(FinancialCategoryType? type, CancellationToken ct);
    Task<FinancialCategoryView> SaveCategoryAsync(Guid? id, SaveFinancialCategoryRequest request, FinancialCategoryType type, Guid user, SalesAuditContext audit, DateTimeOffset now, CancellationToken ct);
    Task<PagedFinancialAccounts> ListAsync(FinancialOperationType type, FinancialAccountFilter filter, DateOnly today, CancellationToken ct);
    Task<FinancialAccountView?> GetAsync(FinancialOperationType type, Guid id, DateOnly today, CancellationToken ct);
    Task<IReadOnlyList<FinancialAccountView>> CreateAsync(FinancialOperationType type, SaveFinancialAccountRequest request, Guid user, SalesAuditContext audit, DateTimeOffset now, CancellationToken ct);
    Task<FinancialAccountView> UpdateAsync(FinancialOperationType type, Guid id, SaveFinancialAccountRequest request, byte[] version, Guid user, SalesAuditContext audit, DateTimeOffset now, CancellationToken ct);
    Task<FinancialAccountView> CancelAsync(FinancialOperationType type, Guid id, string reason, byte[] version, Guid user, SalesAuditContext audit, DateTimeOffset now, CancellationToken ct);
    Task<IReadOnlyList<FinancialPaymentView>> PaymentsAsync(FinancialOperationType type, Guid id, CancellationToken ct);
    Task<FinancialAccountView> PayAsync(FinancialOperationType type, Guid id, string key, CreateFinancialPaymentRequest request, PaymentMethod method, byte[] version, Guid user, SalesAuditContext audit, DateTimeOffset now, CancellationToken ct);
    Task<FinancialAccountView> ReverseAsync(FinancialOperationType type, Guid id, Guid paymentId, string reason, byte[] version, Guid user, SalesAuditContext audit, DateTimeOffset now, CancellationToken ct);
    Task<FinancialDashboard> DashboardAsync(DateOnly from, DateOnly through, DateOnly today, CancellationToken ct);
}
