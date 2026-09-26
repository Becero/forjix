using Forjix.Domain.Enums;

namespace Forjix.Application.Features.Financial;

public interface IFinancialService
{
    Task<IReadOnlyList<FinancialCategoryView>> CategoriesAsync(string? type, CancellationToken ct = default);
    Task<FinancialCategoryView> SaveCategoryAsync(Guid? id, SaveFinancialCategoryRequest request, CancellationToken ct = default);
    Task<PagedFinancialAccounts> ListAsync(FinancialOperationType type, FinancialAccountFilter filter, CancellationToken ct = default);
    Task<FinancialAccountView> GetAsync(FinancialOperationType type, Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<FinancialAccountView>> CreateAsync(FinancialOperationType type, SaveFinancialAccountRequest request, CancellationToken ct = default);
    Task<FinancialAccountView> UpdateAsync(FinancialOperationType type, Guid id, SaveFinancialAccountRequest request, CancellationToken ct = default);
    Task<FinancialAccountView> CancelAsync(FinancialOperationType type, Guid id, FinancialActionRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<FinancialPaymentView>> PaymentsAsync(FinancialOperationType type, Guid id, CancellationToken ct = default);
    Task<FinancialAccountView> PayAsync(FinancialOperationType type, Guid id, string key, CreateFinancialPaymentRequest request, CancellationToken ct = default);
    Task<FinancialAccountView> ReverseAsync(FinancialOperationType type, Guid id, Guid paymentId, FinancialActionRequest request, CancellationToken ct = default);
    Task<FinancialDashboard> DashboardAsync(DateOnly? from, DateOnly? through, CancellationToken ct = default);
}
