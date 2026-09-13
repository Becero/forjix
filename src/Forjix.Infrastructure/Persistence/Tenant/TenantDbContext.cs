using Forjix.Domain.Entities.Audit;
using Forjix.Domain.Entities.Catalog;
using Forjix.Domain.Entities.Cash;
using Forjix.Domain.Entities.Customers;
using Forjix.Domain.Entities.Identity;
using Forjix.Domain.Entities.Inventory;
using Forjix.Domain.Entities.Purchases;
using Forjix.Domain.Entities.Sales;
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
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleItem> SaleItems => Set<SaleItem>();
    public DbSet<SaleSequence> SaleSequences => Set<SaleSequence>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Supplier> Suppliers => Set<Supplier>(); public DbSet<Purchase> Purchases => Set<Purchase>(); public DbSet<PurchaseItem> PurchaseItems => Set<PurchaseItem>(); public DbSet<PurchaseSequence> PurchaseSequences => Set<PurchaseSequence>();
    public DbSet<CashRegister> CashRegisters=>Set<CashRegister>(); public DbSet<CashSession> CashSessions=>Set<CashSession>(); public DbSet<CashMovement> CashMovements=>Set<CashMovement>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        TenantModelConfiguration.Configure(modelBuilder);
    }
}
