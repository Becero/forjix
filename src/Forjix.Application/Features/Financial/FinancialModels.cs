namespace Forjix.Application.Features.Financial;

public sealed record FinancialCategoryView(Guid Id, string Name, string Type, bool IsActive);
public sealed record SaveFinancialCategoryRequest(string Name, string Type, bool IsActive);
public sealed record SaveFinancialAccountRequest(Guid? PartyId, Guid FinancialCategoryId, string Description, string? Document, decimal Amount, DateOnly IssueDate, DateOnly DueDate, int Installments, string? Notes, string? RowVersion);
public sealed record FinancialAccountFilter(string? Search, DateOnly? From, DateOnly? Through, string? Status, Guid? PartyId, Guid? CategoryId, bool OverdueOnly = false, int Page = 1, int PageSize = 20);
public sealed record FinancialAccountView(Guid Id, Guid GroupId, Guid? PartyId, string? PartyName, Guid? SourceId, Guid FinancialCategoryId, string CategoryName, string Description, string? Document, decimal OriginalAmount, decimal OpenAmount, DateOnly IssueDate, DateOnly DueDate, DateOnly? PaymentDate, string Status, int InstallmentNumber, int TotalInstallments, string? Notes, string RowVersion);
public sealed record PagedFinancialAccounts(IReadOnlyList<FinancialAccountView> Items, int Page, int PageSize, int Total);
public sealed record CreateFinancialPaymentRequest(decimal Amount, DateOnly PaymentDate, string PaymentMethod, decimal Discount, decimal Interest, decimal Penalty, string? Notes, string RowVersion);
public sealed record FinancialActionRequest(string Reason, string RowVersion);
public sealed record FinancialPaymentView(Guid Id, decimal Amount, decimal PrincipalAmount, DateOnly PaymentDate, string PaymentMethod, decimal Discount, decimal Interest, decimal Penalty, string? Notes, long? CashMovementId, DateTimeOffset? ReversedAt, string? ReversalReason, string RowVersion);
public sealed record FinancialDashboard(decimal TotalReceivable, decimal TotalPayable, int OverdueCount, decimal OverdueAmount, decimal Received, decimal Paid, decimal Balance, IReadOnlyList<FinancialAccountView> Upcoming);
public sealed record FinancialTerms(Guid FinancialCategoryId, DateOnly FirstDueDate, int Installments = 1);
