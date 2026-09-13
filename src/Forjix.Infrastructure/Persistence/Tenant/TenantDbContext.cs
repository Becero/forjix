using Forjix.Domain.Entities.Audit;
using Forjix.Domain.Entities.Catalog;
using Forjix.Domain.Entities.Identity;
using Forjix.Domain.Entities.Inventory;
using Microsoft.EntityFrameworkCore;
using InventoryEntity = Forjix.Domain.Entities.Inventory.Inventory;

namespace Forjix.Infrastructure.Persistence.Tenant;

public sealed class TenantDbContext(DbContextOptions<TenantDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<InventoryEntity> Inventories => Set<InventoryEntity>();
    public DbSet<InventoryMovement> InventoryMovements => Set<InventoryMovement>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        TenantModelConfiguration.Configure(modelBuilder);
    }
}
