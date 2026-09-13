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
if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddUserSecrets<Program>(optional: true);
}

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
    var password = RequirePassword(builder.Configuration, "Forjix:DevelopmentSeed:AdminPassword");
    await ProvisionTenantAsync(master, builder.Configuration, new TenantSeedDefinition(
        "Empresa Demo", "empresa-demo", "Forjix_EmpresaDemo", "development",
        "TenantDatabases:empresa-demo", "admin@demo.com", "Administrador Forjix",
        password, null, false));
}

if (builder.Environment.IsEnvironment("CI") &&
    builder.Configuration.GetValue("Forjix:IntegrationSeed:Enabled", false))
{
    var password = RequirePassword(builder.Configuration, "Forjix:IntegrationSeed:AdminPassword");
    await ProvisionTenantAsync(master, builder.Configuration, new TenantSeedDefinition(
        "Empresa A CI", "empresa-a-ci", "Forjix_EmpresaA_CI", "ci",
        "TenantDatabases:EmpresaA_CI", "admin@teste.local", "Administrador Empresa A",
        password, "tenant.a.marker", true));
    await ProvisionTenantAsync(master, builder.Configuration, new TenantSeedDefinition(
        "Empresa B CI", "empresa-b-ci", "Forjix_EmpresaB_CI", "ci",
        "TenantDatabases:EmpresaB_CI", "admin@teste.local", "Administrador Empresa B",
        password, "tenant.b.marker", true));
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

static string RequirePassword(IConfiguration configuration, string key)
{
    var password = configuration[key];
    return !string.IsNullOrWhiteSpace(password) && password.Length >= 12
        ? password
        : throw new InvalidOperationException($"'{key}' must be externally configured with at least 12 characters.");
}

static TenantDbContext CreateTenantDbContext(string connectionString) => new(
    new DbContextOptionsBuilder<TenantDbContext>().UseSqlServer(connectionString).Options);

static async Task ProvisionTenantAsync(
    ForjixMasterDbContext master,
    IConfiguration configuration,
    TenantSeedDefinition definition)
{
    var tenantConnection = configuration[definition.SecretReference];
    if (string.IsNullOrWhiteSpace(tenantConnection))
    {
        throw new InvalidOperationException($"Configure '{definition.SecretReference}' outside source control.");
    }

    var now = DateTimeOffset.UtcNow;
    var plan = await master.Plans.SingleOrDefaultAsync(x => x.Code == definition.PlanCode);
    if (plan is null)
    {
        plan = new Plan
        {
            Id = Guid.NewGuid(),
            Code = definition.PlanCode,
            Name = definition.PlanCode.ToUpperInvariant(),
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };
        master.Plans.Add(plan);
    }

    var tenant = await master.Tenants
        .Include(x => x.Database).Include(x => x.Subscriptions).Include(x => x.Settings)
        .SingleOrDefaultAsync(x => x.Slug == definition.Slug);
    if (tenant is null)
    {
        tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = definition.Name,
            Slug = definition.Slug,
            Status = TenantStatus.Active,
            Plan = plan,
            CreatedAt = now,
            UpdatedAt = now
        };
        master.Tenants.Add(tenant);
    }

    tenant.Name = definition.Name;
    tenant.Status = TenantStatus.Active;
    tenant.Plan = plan;
    tenant.UpdatedAt = now;
    tenant.Database ??= new TenantDatabase
    {
        Id = Guid.NewGuid(),
        Tenant = tenant,
        DatabaseName = definition.DatabaseName,
        ServerReference = definition.PlanCode,
        SecretReference = definition.SecretReference,
        SchemaVersion = "pending",
        CreatedAt = now,
        UpdatedAt = now
    };
    tenant.Database.DatabaseName = definition.DatabaseName;
    tenant.Database.SecretReference = definition.SecretReference;
    tenant.Database.UpdatedAt = now;

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
    await SeedTenantIdentityAsync(tenantDb, definition, now);
}

static async Task SeedTenantIdentityAsync(
    TenantDbContext db,
    TenantSeedDefinition definition,
    DateTimeOffset now)
{
    var permissionCodes = Permissions.All
        .Concat(definition.MarkerPermission is null ? [] : [definition.MarkerPermission])
        .ToArray();
    var existingPermissions = await db.Permissions.ToDictionaryAsync(x => x.Code);
    foreach (var code in permissionCodes)
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

    var permissionIds = await db.Permissions
        .Where(x => permissionCodes.Contains(x.Code)).Select(x => x.Id).ToListAsync();
    var assignedPermissionIds = await db.RolePermissions
        .Where(x => x.RoleId == role.Id).Select(x => x.PermissionId).ToListAsync();
    foreach (var permissionId in permissionIds.Except(assignedPermissionIds))
    {
        db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permissionId, GrantedAt = now });
    }

    var normalizedEmail = definition.AdminEmail.ToUpperInvariant();
    var user = await db.Users.SingleOrDefaultAsync(x => x.NormalizedEmail == normalizedEmail);
    if (user is null)
    {
        user = new User
        {
            Id = Guid.NewGuid(),
            Name = definition.AdminName,
            Email = definition.AdminEmail,
            NormalizedEmail = normalizedEmail,
            PasswordHash = string.Empty,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.Users.Add(user);
    }

    user.Name = definition.AdminName;
    user.Email = definition.AdminEmail;
    user.IsActive = true;
    user.LockedUntil = null;
    user.PasswordHash = new PasswordHasher<User>().HashPassword(user, definition.Password);
    user.SecurityStamp = Guid.NewGuid();
    user.UpdatedAt = now;

    if (!await db.UserRoles.AnyAsync(x => x.UserId == user.Id && x.RoleId == role.Id))
    {
        db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id, AssignedAt = now });
    }

    if (definition.AddUnprivilegedUser)
    {
        const string viewerEmail = "viewer@teste.local";
        var viewer = await db.Users.SingleOrDefaultAsync(x => x.NormalizedEmail == "VIEWER@TESTE.LOCAL");
        if (viewer is null)
        {
            viewer = new User
            {
                Id = Guid.NewGuid(),
                Name = "Usuário sem permissão",
                Email = viewerEmail,
                NormalizedEmail = "VIEWER@TESTE.LOCAL",
                PasswordHash = string.Empty,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            };
            db.Users.Add(viewer);
        }

        viewer.PasswordHash = new PasswordHasher<User>().HashPassword(viewer, definition.Password);
        viewer.SecurityStamp = Guid.NewGuid();
        viewer.UpdatedAt = now;
    }

    await db.SaveChangesAsync();
}

internal sealed record TenantSeedDefinition(
    string Name,
    string Slug,
    string DatabaseName,
    string PlanCode,
    string SecretReference,
    string AdminEmail,
    string AdminName,
    string Password,
    string? MarkerPermission,
    bool AddUnprivilegedUser);
