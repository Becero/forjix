using Forjix.Application.Common;
using Forjix.Domain.Entities.Identity;
using Forjix.Domain.Entities.Master;
using Forjix.Domain.Enums;
using Forjix.Infrastructure.Persistence.Master;
using Forjix.Infrastructure.Persistence.Tenant;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.AddUserSecrets<Program>(optional: true);

var masterConnection = builder.Configuration.GetConnectionString("ForjixMaster");
if (string.IsNullOrWhiteSpace(masterConnection))
{
    throw new InvalidOperationException("Configure ConnectionStrings:ForjixMaster outside source control.");
}

await using var master = new ForjixMasterDbContext(
    new DbContextOptionsBuilder<ForjixMasterDbContext>().UseSqlServer(masterConnection).Options);
await master.Database.MigrateAsync();

if (builder.Environment.IsDevelopment() &&
    builder.Configuration.GetValue("Forjix:DevelopmentSeed:Enabled", false))
{
    await ProvisionDemoAsync(master, builder.Configuration);
}

var activeTenantDatabases = await master.Tenants
    .AsNoTracking()
    .Where(x => x.Status == TenantStatus.Active && x.Database != null)
    .Select(x => new { x.Id, x.Database!.DatabaseName, x.Database.SecretReference })
    .ToListAsync();

var failures = 0;
foreach (var tenant in activeTenantDatabases)
{
    var startedAt = DateTimeOffset.UtcNow;
    var execution = new MigrationExecution
    {
        Id = Guid.NewGuid(),
        TenantId = tenant.Id,
        DatabaseName = tenant.DatabaseName,
        MigrationType = "Tenant",
        Status = "Started",
        StartedAt = startedAt,
        CompletedAt = startedAt
    };

    try
    {
        var connection = builder.Configuration[tenant.SecretReference];
        if (string.IsNullOrWhiteSpace(connection))
        {
            throw new InvalidOperationException($"Secret reference '{tenant.SecretReference}' is not configured.");
        }

        await using var tenantDb = CreateTenantDbContext(connection);
        await tenantDb.Database.MigrateAsync();
        execution.Status = "Succeeded";
        execution.AppliedMigration = (await tenantDb.Database.GetAppliedMigrationsAsync()).LastOrDefault();

        var database = await master.TenantDatabases.SingleAsync(x => x.TenantId == tenant.Id);
        database.SchemaVersion = execution.AppliedMigration ?? "none";
        database.LastMigratedAt = DateTimeOffset.UtcNow;
    }
    catch (Exception exception)
    {
        failures++;
        execution.Status = "Failed";
        execution.ErrorSummary = exception.Message.Length <= 1000 ? exception.Message : exception.Message[..1000];
    }

    execution.CompletedAt = DateTimeOffset.UtcNow;
    master.MigrationExecutions.Add(execution);
    await master.SaveChangesAsync();
}

if (failures > 0)
{
    Environment.ExitCode = 1;
}

static TenantDbContext CreateTenantDbContext(string connectionString) => new(
    new DbContextOptionsBuilder<TenantDbContext>().UseSqlServer(connectionString).Options);

static async Task ProvisionDemoAsync(ForjixMasterDbContext master, IConfiguration configuration)
{
    const string slug = "empresa-demo";
    const string tenantSecretReference = "TenantDatabases:empresa-demo";
    var now = DateTimeOffset.UtcNow;
    var password = configuration["Forjix:DevelopmentSeed:AdminPassword"];
    var tenantConnection = configuration[tenantSecretReference];

    if (string.IsNullOrWhiteSpace(password) || password.Length < 12)
    {
        throw new InvalidOperationException("Development seed password must be externally configured with at least 12 characters.");
    }

    if (string.IsNullOrWhiteSpace(tenantConnection))
    {
        throw new InvalidOperationException($"Configure '{tenantSecretReference}' outside source control.");
    }

    var plan = await master.Plans.SingleOrDefaultAsync(x => x.Code == "development");
    if (plan is null)
    {
        plan = new Plan { Id = Guid.NewGuid(), Code = "development", Name = "Development", IsActive = true, CreatedAt = now, UpdatedAt = now };
        master.Plans.Add(plan);
    }

    var tenant = await master.Tenants
        .Include(x => x.Database).Include(x => x.Subscriptions).Include(x => x.Settings)
        .SingleOrDefaultAsync(x => x.Slug == slug);
    if (tenant is null)
    {
        tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Empresa Demo",
            Slug = slug,
            Status = TenantStatus.Active,
            Plan = plan,
            CreatedAt = now,
            UpdatedAt = now
        };
        master.Tenants.Add(tenant);
    }

    tenant.Name = "Empresa Demo";
    tenant.Status = TenantStatus.Active;
    tenant.Plan = plan;
    tenant.UpdatedAt = now;
    tenant.Database ??= new TenantDatabase
    {
        Id = Guid.NewGuid(),
        Tenant = tenant,
        DatabaseName = "Forjix_EmpresaDemo",
        ServerReference = "development",
        SecretReference = tenantSecretReference,
        SchemaVersion = "pending",
        CreatedAt = now,
        UpdatedAt = now
    };
    if (!tenant.Subscriptions.Any(x => x.Status == SubscriptionStatus.Active && x.EndsAt is null))
    {
        tenant.Subscriptions.Add(new Subscription
        {
            Id = Guid.NewGuid(),
            Tenant = tenant,
            Plan = plan,
            Status = SubscriptionStatus.Active,
            StartsAt = now,
            CreatedAt = now,
            UpdatedAt = now
        });
    }

    var negativeStock = tenant.Settings.SingleOrDefault(x => x.Key == TenantSettingKeys.AllowNegativeStock);
    if (negativeStock is null)
    {
        tenant.Settings.Add(new TenantSetting
        {
            Id = Guid.NewGuid(),
            Tenant = tenant,
            Key = TenantSettingKeys.AllowNegativeStock,
            Value = bool.FalseString,
            UpdatedAt = now
        });
    }
    else
    {
        negativeStock.Value = bool.FalseString;
        negativeStock.UpdatedAt = now;
    }

    await master.SaveChangesAsync();

    await using var tenantDb = CreateTenantDbContext(tenantConnection);
    await tenantDb.Database.MigrateAsync();
    await SeedTenantIdentityAsync(tenantDb, password, now);
}

static async Task SeedTenantIdentityAsync(TenantDbContext db, string password, DateTimeOffset now)
{
    var existingPermissions = await db.Permissions.ToDictionaryAsync(x => x.Code);
    foreach (var code in Permissions.All)
    {
        if (!existingPermissions.ContainsKey(code))
        {
            db.Permissions.Add(new Permission
            {
                Id = Guid.NewGuid(),
                Code = code,
                Name = code,
                Module = code.Split('.')[0]
            });
        }
    }

    await db.SaveChangesAsync();
    var role = await db.Roles.SingleOrDefaultAsync(x => x.NormalizedName == "ADMINISTRADOR");
    if (role is null)
    {
        role = new Role
        {
            Id = Guid.NewGuid(),
            Name = "Administrador",
            NormalizedName = "ADMINISTRADOR",
            Description = "Administração do tenant",
            IsSystem = true,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.Roles.Add(role);
        await db.SaveChangesAsync();
    }

    var permissionIds = await db.Permissions.Where(x => Permissions.All.Contains(x.Code)).Select(x => x.Id).ToListAsync();
    var assignedPermissionIds = await db.RolePermissions.Where(x => x.RoleId == role.Id).Select(x => x.PermissionId).ToListAsync();
    foreach (var permissionId in permissionIds.Except(assignedPermissionIds))
    {
        db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permissionId, GrantedAt = now });
    }

    var user = await db.Users.SingleOrDefaultAsync(x => x.NormalizedEmail == "ADMIN@DEMO.COM");
    if (user is null)
    {
        user = new User
        {
            Id = Guid.NewGuid(),
            Name = "Administrador Forjix",
            Email = "admin@demo.com",
            NormalizedEmail = "ADMIN@DEMO.COM",
            PasswordHash = string.Empty,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.Users.Add(user);
    }

    user.Name = "Administrador Forjix";
    user.Email = "admin@demo.com";
    user.IsActive = true;
    user.LockedUntil = null;
    user.PasswordHash = new PasswordHasher<User>().HashPassword(user, password);
    user.SecurityStamp = Guid.NewGuid();
    user.UpdatedAt = now;

    if (!await db.UserRoles.AnyAsync(x => x.UserId == user.Id && x.RoleId == role.Id))
    {
        db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id, AssignedAt = now });
    }

    await db.SaveChangesAsync();
}
