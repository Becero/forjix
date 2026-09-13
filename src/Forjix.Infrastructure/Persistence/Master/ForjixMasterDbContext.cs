using Forjix.Domain.Entities.Master;
using Microsoft.EntityFrameworkCore;
using MasterTenant = Forjix.Domain.Entities.Master.Tenant;

namespace Forjix.Infrastructure.Persistence.Master;

public sealed class ForjixMasterDbContext(DbContextOptions<ForjixMasterDbContext> options) : DbContext(options)
{
    public DbSet<MasterTenant> Tenants => Set<MasterTenant>();
    public DbSet<Plan> Plans => Set<Plan>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<TenantDatabase> TenantDatabases => Set<TenantDatabase>();
    public DbSet<TenantSetting> TenantSettings => Set<TenantSetting>();
    public DbSet<TenantFeature> TenantFeatures => Set<TenantFeature>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        MasterModelConfiguration.Configure(modelBuilder);
    }
}
