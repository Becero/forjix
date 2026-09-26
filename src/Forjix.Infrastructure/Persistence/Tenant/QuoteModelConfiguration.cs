using Forjix.Domain.Entities.Identity;
using Forjix.Domain.Entities.Quotes;
using Microsoft.EntityFrameworkCore;
namespace Forjix.Infrastructure.Persistence.Tenant;
internal static class QuoteModelConfiguration
{
    public static void Configure(ModelBuilder model)
    {
        model.Entity<Quote>(e =>
        {
            e.ToTable("Quotes", t =>
            {
                t.HasCheckConstraint("CK_Quote_Amounts", "[Subtotal] >= 0 AND [Discount] >= 0 AND [Total] >= 0 AND [Total] <= [Subtotal]");
                t.HasCheckConstraint("CK_Quote_Dates", "[ValidUntil] >= [IssueDate]");
                t.HasCheckConstraint("CK_Quote_Status", "[Status] IN (0,1,2,3,5,6)");
                t.HasCheckConstraint("CK_Quote_Conversion", "([Status] = 5 AND [SaleId] IS NOT NULL AND [ConvertedAt] IS NOT NULL) OR ([Status] <> 5 AND [SaleId] IS NULL AND [ConvertedAt] IS NULL)");
            });
            e.HasKey(x => x.Id); e.Property(x => x.Number).HasMaxLength(40).IsRequired(); e.HasIndex(x => x.Number).IsUnique();
            e.Property(x => x.Notes).HasMaxLength(2000); e.Property(x => x.RowVersion).IsRowVersion();
            e.Property(x => x.Subtotal).HasPrecision(18, 2); e.Property(x => x.Discount).HasPrecision(18, 2); e.Property(x => x.Total).HasPrecision(18, 2);
            e.HasIndex(x => x.IssueDate); e.HasIndex(x => x.ValidUntil); e.HasIndex(x => new { x.Status, x.ValidUntil });
            e.HasIndex(x => x.SaleId).IsUnique().HasFilter("[SaleId] IS NOT NULL");
            e.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UpdatedByUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Sale).WithMany().HasForeignKey(x => x.SaleId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<QuoteItem>(e =>
        {
            e.ToTable("QuoteItems", t => t.HasCheckConstraint("CK_QuoteItem_Amounts", "[Quantity] > 0 AND [UnitPrice] >= 0 AND [Discount] >= 0 AND [Total] >= 0 AND [Total] = ROUND([Quantity] * [UnitPrice], 2) - [Discount]"));
            e.HasKey(x => x.Id); e.Property(x => x.ProductName).HasMaxLength(180).IsRequired(); e.Property(x => x.Sku).HasMaxLength(60).IsRequired();
            e.Property(x => x.Quantity).HasPrecision(18, 3); e.Property(x => x.UnitPrice).HasPrecision(18, 2); e.Property(x => x.Discount).HasPrecision(18, 2); e.Property(x => x.Total).HasPrecision(18, 2);
            e.HasIndex(x => new { x.QuoteId, x.ProductId }).IsUnique();
            e.HasOne(x => x.Quote).WithMany(x => x.Items).HasForeignKey(x => x.QuoteId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<QuoteSequence>(e =>
        {
            e.ToTable("QuoteSequences", t => t.HasCheckConstraint("CK_QuoteSequence", "[Id] = 1 AND [LastValue] >= 0"));
            e.HasKey(x => x.Id); e.Property(x => x.Id).ValueGeneratedNever(); e.Property(x => x.RowVersion).IsRowVersion();
        });
    }
}
