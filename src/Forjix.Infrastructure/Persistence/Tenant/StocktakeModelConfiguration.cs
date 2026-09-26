using Forjix.Domain.Entities.Inventory;
using Forjix.Domain.Entities.Identity;
using Microsoft.EntityFrameworkCore;
namespace Forjix.Infrastructure.Persistence.Tenant;
internal static class StocktakeModelConfiguration
{
    public static void Configure(ModelBuilder model)
    {
        model.Entity<Stocktake>(e =>
        {
            e.ToTable("Stocktakes", t =>
            {
                t.HasCheckConstraint("CK_Stocktake_Status", "[Status] IN (0,1,2,3)");
                t.HasCheckConstraint("CK_Stocktake_Closed", "([Status] IN (0,1) AND [ClosedAt] IS NULL) OR ([Status] IN (2,3) AND [ClosedAt] IS NOT NULL)");
            });
            e.HasKey(x => x.Id); e.Property(x => x.Number).HasMaxLength(40).IsRequired(); e.HasIndex(x => x.Number).IsUnique();
            e.Property(x => x.Notes).HasMaxLength(2000); e.Property(x => x.RowVersion).IsRowVersion();
            e.HasIndex(x => new { x.Status, x.OpenedAt }); e.HasIndex(x => x.OpenedAt);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UpdatedByUserId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<StocktakeItem>(e =>
        {
            e.ToTable("StocktakeItems", t => t.HasCheckConstraint("CK_StocktakeItem_Count", "[CountedQuantity] IS NULL OR ([CountedQuantity] >= 0 AND [CountedAt] IS NOT NULL AND [CountedByUserId] IS NOT NULL)"));
            e.HasKey(x => x.Id); e.Property(x => x.ExpectedQuantity).HasPrecision(18,3); e.Property(x => x.CountedQuantity).HasPrecision(18,3);
            e.Property(x => x.StockRowVersion).HasMaxLength(8).IsRequired(); e.Property(x => x.Notes).HasMaxLength(500); e.Ignore(x => x.Difference);
            e.HasIndex(x => new { x.StocktakeId, x.ProductId }).IsUnique();
            e.HasOne(x => x.Stocktake).WithMany(x => x.Items).HasForeignKey(x => x.StocktakeId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.CountedByUserId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<StocktakeSequence>(e =>
        {
            e.ToTable("StocktakeSequences", t => t.HasCheckConstraint("CK_StocktakeSequence", "[Id] = 1 AND [LastValue] >= 0"));
            e.HasKey(x => x.Id); e.Property(x => x.Id).ValueGeneratedNever(); e.Property(x => x.RowVersion).IsRowVersion();
        });
        model.Entity<InventoryMovement>(e =>
        {
            e.Property(x => x.ReasonCode).HasMaxLength(30); e.Property(x => x.Observation).HasMaxLength(500);
            e.HasIndex(x => new { x.ProductId, x.CreatedAt });
        });
    }
}
