using Forjix.Domain.Entities.Master;
using Microsoft.EntityFrameworkCore;
using MasterTenant = Forjix.Domain.Entities.Master.Tenant;

namespace Forjix.Infrastructure.Persistence.Master;

internal static class MasterModelConfiguration
{
    public static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MasterTenant>(entity =>
        {
            entity.ToTable("Tenants");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Slug).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Cnpj).HasMaxLength(14);
            entity.HasIndex(x => x.Slug).IsUnique();
            entity.HasIndex(x => x.Cnpj).IsUnique().HasFilter("[Cnpj] IS NOT NULL");
            entity.HasOne(x => x.Plan).WithMany(x => x.Tenants).HasForeignKey(x => x.PlanId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Plan>(entity =>
        {
            entity.ToTable("Plans");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Code).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(120).IsRequired();
            entity.HasIndex(x => x.Code).IsUnique();
        });

        modelBuilder.Entity<Subscription>(entity =>
        {
            entity.ToTable("Subscriptions");
            entity.HasKey(x => x.Id);
            entity.HasOne(x => x.Tenant).WithMany(x => x.Subscriptions).HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Plan).WithMany(x => x.Subscriptions).HasForeignKey(x => x.PlanId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.TenantId, x.Status });
        });

        modelBuilder.Entity<TenantDatabase>(entity =>
        {
            entity.ToTable("TenantDatabases");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.DatabaseName).HasMaxLength(128).IsRequired();
            entity.Property(x => x.ServerReference).HasMaxLength(200).IsRequired();
            entity.Property(x => x.SecretReference).HasMaxLength(300).IsRequired();
            entity.Property(x => x.SchemaVersion).HasMaxLength(40).IsRequired();
            entity.HasIndex(x => x.TenantId).IsUnique();
            entity.HasOne(x => x.Tenant).WithOne(x => x.Database).HasForeignKey<TenantDatabase>(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TenantSetting>(entity =>
        {
            entity.ToTable("TenantSettings");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Key).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Value).HasMaxLength(2000).IsRequired();
            entity.HasIndex(x => new { x.TenantId, x.Key }).IsUnique();
            entity.HasOne(x => x.Tenant).WithMany(x => x.Settings).HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TenantFeature>(entity =>
        {
            entity.ToTable("TenantFeatures");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.FeatureCode).HasMaxLength(120).IsRequired();
            entity.HasIndex(x => new { x.TenantId, x.FeatureCode }).IsUnique();
            entity.HasOne(x => x.Tenant).WithMany(x => x.Features).HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
