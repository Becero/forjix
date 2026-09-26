using System.Text.Json;
using Forjix.Application.Abstractions.Sales;
using Forjix.Application.Common;
using Forjix.Application.Features.Financial;
using Forjix.Domain.Entities.Audit;
using Forjix.Domain.Entities.Financial;
using Forjix.Domain.Entities.Purchases;
using Forjix.Domain.Entities.Sales;
using Forjix.Domain.Enums;
using Forjix.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;

namespace Forjix.Infrastructure.Financial;

// This helper only stages entities in the caller's DbContext. The sale/purchase owns the transaction.
internal static class FinancialOriginWriter
{
    public static async Task FromSaleAsync(TenantDbContext db, Sale sale, FinancialTerms terms, Guid user, SalesAuditContext audit, DateTimeOffset now, CancellationToken ct)
    {
        if (await db.AccountsReceivable.AnyAsync(x => x.SaleId == sale.Id, ct)) return;
        if (!sale.CustomerId.HasValue || !await db.Customers.AnyAsync(x => x.Id == sale.CustomerId && x.IsActive, ct)) throw new RequestValidationException("Venda a prazo exige cliente ativo.");
        await Category(db, terms.FinancialCategoryId, FinancialCategoryType.Income, ct);
        var values = Amounts(sale.Total, terms, now);
        var group = Guid.NewGuid();
        for (var i = 0; i < values.Count; i++)
        {
            var account = new AccountReceivable { Id = Guid.NewGuid(), GroupId = group, CustomerId = sale.CustomerId, SaleId = sale.Id, FinancialCategoryId = terms.FinancialCategoryId, Description = $"Venda {sale.Number}", Document = sale.Number, IssueDate = DateOnly.FromDateTime(now.UtcDateTime), DueDate = terms.FirstDueDate.AddMonths(i), InstallmentNumber = i + 1, TotalInstallments = values.Count, CreatedByUserId = user, UpdatedByUserId = user, CreatedAt = now, UpdatedAt = now };
            account.SetAmount(values[i]); db.AccountsReceivable.Add(account);
            Log(db, account.Id, user, audit, now, new { sale.Id, account.OriginalAmount, account.InstallmentNumber });
        }
    }

    public static async Task FromPurchaseAsync(TenantDbContext db, Purchase purchase, FinancialTerms? terms, Guid user, SalesAuditContext audit, DateTimeOffset now, CancellationToken ct)
    {
        if (purchase.Total == 0 || await db.AccountsPayable.AnyAsync(x => x.PurchaseId == purchase.Id, ct)) return;
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        if (terms is null)
        {
            var category = await db.FinancialCategories.SingleOrDefaultAsync(x => x.Name == "Compras" && x.Type == FinancialCategoryType.Expense, ct);
            if (category is null)
            {
                category = new FinancialCategory { Id = Guid.NewGuid(), Name = "Compras", Type = FinancialCategoryType.Expense, CreatedAt = now, UpdatedAt = now };
                db.FinancialCategories.Add(category);
                db.AuditLogs.Add(new AuditLog { UserId = user, Action = AuditAction.FinancialCategoryCreated, EntityName = nameof(FinancialCategory), EntityId = category.Id.ToString(), AfterData = JsonSerializer.Serialize(new { category.Name }), CorrelationId = audit.CorrelationId, IpAddress = audit.IpAddress, OccurredAt = now });
            }
            if (!category.IsActive) throw new RequestValidationException("Ative a categoria Compras ou informe outra categoria financeira.");
            terms = new(category.Id, today);
        }
        else await Category(db, terms.FinancialCategoryId, FinancialCategoryType.Expense, ct);
        var values = Amounts(purchase.Total, terms, now);
        var group = Guid.NewGuid();
        for (var i = 0; i < values.Count; i++)
        {
            var account = new AccountPayable { Id = Guid.NewGuid(), GroupId = group, SupplierId = purchase.SupplierId, PurchaseId = purchase.Id, FinancialCategoryId = terms.FinancialCategoryId, Description = $"Compra {purchase.Number}", Document = purchase.Number, IssueDate = today, DueDate = terms.FirstDueDate.AddMonths(i), InstallmentNumber = i + 1, TotalInstallments = values.Count, CreatedByUserId = user, UpdatedByUserId = user, CreatedAt = now, UpdatedAt = now };
            account.SetAmount(values[i]); db.AccountsPayable.Add(account);
            Log(db, account.Id, user, audit, now, new { purchase.Id, account.OriginalAmount, account.InstallmentNumber });
        }
    }

    public static async Task CancelSaleAsync(TenantDbContext db, Guid saleId, Guid user, SalesAuditContext audit, DateTimeOffset now, CancellationToken ct)
    {
        var accounts = await db.AccountsReceivable.Where(x => x.SaleId == saleId).ToListAsync(ct);
        var ids = accounts.Select(x => x.Id).ToArray();
        if (await db.FinancialPayments.AnyAsync(x => ids.Contains(x.AccountReceivableId ?? Guid.Empty) && x.ReversedAt == null, ct)) throw new ResourceConflictException("Estorne as baixas financeiras antes de cancelar a venda.");
        foreach (var account in accounts)
        {
            account.Cancel(); account.UpdatedAt = now; account.UpdatedByUserId = user;
            db.AuditLogs.Add(new AuditLog { UserId = user, Action = AuditAction.FinancialAccountCancelled, EntityName = nameof(AccountReceivable), EntityId = account.Id.ToString(), AfterData = JsonSerializer.Serialize(new { saleId }), CorrelationId = audit.CorrelationId, IpAddress = audit.IpAddress, OccurredAt = now });
        }
    }

    private static async Task Category(TenantDbContext db, Guid id, FinancialCategoryType type, CancellationToken ct)
    {
        if (!await db.FinancialCategories.AnyAsync(x => x.Id == id && x.Type == type && x.IsActive, ct)) throw new RequestValidationException("Categoria financeira incompatível ou inativa.");
    }
    private static IReadOnlyList<decimal> Amounts(decimal total, FinancialTerms terms, DateTimeOffset now)
    {
        if (terms.FirstDueDate == default || terms.FirstDueDate < DateOnly.FromDateTime(now.UtcDateTime)) throw new RequestValidationException("Vencimento inválido.");
        try
        {
            var amounts = FinancialRules.Split(decimal.Round(total, 2, MidpointRounding.AwayFromZero), terms.Installments);
            _ = terms.FirstDueDate.AddMonths(terms.Installments - 1);
            return amounts;
        }
        catch (ArgumentException e) { throw new RequestValidationException(e.Message); }
    }
    private static void Log(TenantDbContext db, Guid id, Guid user, SalesAuditContext audit, DateTimeOffset now, object data) =>
        db.AuditLogs.Add(new AuditLog { UserId = user, Action = AuditAction.FinancialAccountCreated, EntityName = "Financial", EntityId = id.ToString(), AfterData = JsonSerializer.Serialize(data), CorrelationId = audit.CorrelationId, IpAddress = audit.IpAddress, OccurredAt = now });
}
