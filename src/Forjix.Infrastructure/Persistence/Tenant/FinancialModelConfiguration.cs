using Forjix.Domain.Entities.Cash;
using Forjix.Domain.Entities.Financial;
using Forjix.Domain.Entities.Identity;
using Microsoft.EntityFrameworkCore;

namespace Forjix.Infrastructure.Persistence.Tenant;

internal static class FinancialModelConfiguration
{
    public static void Configure(ModelBuilder model)
    {
        model.Entity<FinancialCategory>(e =>
        {
            e.ToTable("FinancialCategories", t => t.HasCheckConstraint("CK_FinancialCategory_Type", "[Type] IN (0,1)"));
            e.HasKey(x => x.Id); e.Property(x => x.Name).HasMaxLength(120).IsRequired();
            e.HasIndex(x => new { x.Type, x.Name }).IsUnique();
        });
        model.Entity<FinancialAccount>(e =>
        {
            e.UseTpcMappingStrategy(); e.HasKey(x => x.Id);
            e.Property(x => x.Description).HasMaxLength(200).IsRequired();
            e.Property(x => x.Document).HasMaxLength(100); e.Property(x => x.Notes).HasMaxLength(1000);
            e.Property(x => x.OriginalAmount).HasPrecision(18, 2); e.Property(x => x.OpenAmount).HasPrecision(18, 2);
            e.Property(x => x.RowVersion).IsRowVersion();
            e.HasIndex(x => new { x.Status, x.DueDate }); e.HasIndex(x => x.DueDate);
            e.HasIndex(x => new { x.GroupId, x.InstallmentNumber }).IsUnique();
            e.HasOne(x => x.FinancialCategory).WithMany().HasForeignKey(x => x.FinancialCategoryId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UpdatedByUserId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<AccountReceivable>(e =>
        {
            e.ToTable("AccountsReceivable", t =>
            {
                t.HasCheckConstraint("CK_Receivable_Amounts", "[OriginalAmount] > 0 AND [OpenAmount] >= 0 AND [OpenAmount] <= [OriginalAmount]");
                t.HasCheckConstraint("CK_Receivable_Installments", "[InstallmentNumber] >= 1 AND [InstallmentNumber] <= [TotalInstallments] AND [TotalInstallments] <= 120");
                t.HasCheckConstraint("CK_Receivable_Status", "[Status] IN (0,1,2,4)");
                t.HasCheckConstraint("CK_Receivable_Dates", "[DueDate] >= [IssueDate]");
            });
            e.HasIndex(x => new { x.SaleId, x.InstallmentNumber }).IsUnique().HasFilter("[SaleId] IS NOT NULL");
            e.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Sale).WithMany().HasForeignKey(x => x.SaleId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<AccountPayable>(e =>
        {
            e.ToTable("AccountsPayable", t =>
            {
                t.HasCheckConstraint("CK_Payable_Amounts", "[OriginalAmount] > 0 AND [OpenAmount] >= 0 AND [OpenAmount] <= [OriginalAmount]");
                t.HasCheckConstraint("CK_Payable_Installments", "[InstallmentNumber] >= 1 AND [InstallmentNumber] <= [TotalInstallments] AND [TotalInstallments] <= 120");
                t.HasCheckConstraint("CK_Payable_Status", "[Status] IN (0,1,2,4)");
                t.HasCheckConstraint("CK_Payable_Dates", "[DueDate] >= [IssueDate]");
            });
            e.HasIndex(x => new { x.PurchaseId, x.InstallmentNumber }).IsUnique().HasFilter("[PurchaseId] IS NOT NULL");
            e.HasOne(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Purchase).WithMany().HasForeignKey(x => x.PurchaseId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<FinancialPayment>(e =>
        {
            e.ToTable("FinancialPayments", t =>
            {
                t.HasCheckConstraint("CK_Payment_Account", "([Type] = 0 AND [AccountReceivableId] IS NOT NULL AND [AccountPayableId] IS NULL) OR ([Type] = 1 AND [AccountPayableId] IS NOT NULL AND [AccountReceivableId] IS NULL)");
                t.HasCheckConstraint("CK_Payment_Amounts", "[Amount] > 0 AND [PrincipalAmount] > 0 AND [Discount] >= 0 AND [Interest] >= 0 AND [Penalty] >= 0 AND [PrincipalAmount] = [Amount] + [Discount] - [Interest] - [Penalty]");
                t.HasCheckConstraint("CK_Payment_Method", "[PaymentMethod] IN (0,1,2,3)");
            });
            e.HasKey(x => x.Id); e.Property(x => x.IdempotencyKey).HasMaxLength(100).IsRequired(); e.HasIndex(x => x.IdempotencyKey).IsUnique();
            e.Property(x => x.Amount).HasPrecision(18, 2); e.Property(x => x.PrincipalAmount).HasPrecision(18, 2);
            e.Property(x => x.Discount).HasPrecision(18, 2); e.Property(x => x.Interest).HasPrecision(18, 2); e.Property(x => x.Penalty).HasPrecision(18, 2);
            e.Property(x => x.Notes).HasMaxLength(1000); e.Property(x => x.ReversalReason).HasMaxLength(500); e.Property(x => x.RowVersion).IsRowVersion();
            e.HasIndex(x => new { x.PaymentDate, x.Type }); e.HasIndex(x => x.CashMovementId).IsUnique().HasFilter("[CashMovementId] IS NOT NULL");
            e.HasIndex(x => x.ReversalCashMovementId).IsUnique().HasFilter("[ReversalCashMovementId] IS NOT NULL");
            e.HasOne(x => x.AccountReceivable).WithMany().HasForeignKey(x => x.AccountReceivableId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.AccountPayable).WithMany().HasForeignKey(x => x.AccountPayableId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.CashMovement).WithMany().HasForeignKey(x => x.CashMovementId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.ReversalCashMovement).WithMany().HasForeignKey(x => x.ReversalCashMovementId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.ReversedByUserId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
