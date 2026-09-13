using Forjix.Application.Common;
using Forjix.Domain.Entities.Cash;
using Forjix.Domain.Entities.Catalog;
using Forjix.Domain.Entities.Identity;
using Forjix.Domain.Entities.Inventory;
using Forjix.Domain.Entities.Master;
using Forjix.Domain.Entities.Sales;
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
    if (definition.PlanCode == "development") await SeedCommercialDemoAsync(tenantDb, now);
}

static async Task SeedTenantIdentityAsync(
    TenantDbContext db,
    TenantSeedDefinition definition,
    DateTimeOffset now)
{
    var permissionDefinitions = Permissions.Catalog
        .Concat(definition.MarkerPermission is null
            ? []
            : [new Permissions.Definition(definition.MarkerPermission, definition.MarkerPermission, "CI")])
        .ToArray();
    var permissionCodes = permissionDefinitions.Select(x => x.Code).ToArray();
    var existingPermissions = await db.Permissions.ToDictionaryAsync(x => x.Code);
    foreach (var permissionDefinition in permissionDefinitions)
    {
        if (!existingPermissions.TryGetValue(permissionDefinition.Code, out var permission))
        {
            db.Permissions.Add(new Permission
            {
                Id = Guid.NewGuid(),
                Code = permissionDefinition.Code,
                Name = permissionDefinition.Name,
                Module = permissionDefinition.Module
            });
        }
        else
        {
            permission.Name = permissionDefinition.Name;
            permission.Module = permissionDefinition.Module;
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

static async Task SeedCommercialDemoAsync(TenantDbContext db, DateTimeOffset now)
{
    var admin = await db.Users.SingleAsync(x => x.NormalizedEmail == "ADMIN@DEMO.COM");
    var categoryDefinitions = new[]
    {
        (Name: "Alimentos", Description: "Mercearia e alimentos básicos"),
        (Name: "Bebidas", Description: "Bebidas e itens refrigerados"),
        (Name: "Higiene e limpeza", Description: "Cuidados pessoais e limpeza")
    };
    var categories = await db.Categories.ToDictionaryAsync(x => x.Name);
    foreach (var definition in categoryDefinitions)
    {
        if (categories.ContainsKey(definition.Name)) continue;
        var category = new Category { Id = Guid.NewGuid(), Name = definition.Name, Description = definition.Description, IsActive = true, CreatedAt = now, UpdatedAt = now };
        db.Categories.Add(category);
        categories.Add(category.Name, category);
    }
    await db.SaveChangesAsync();

    var productDefinitions = new[]
    {
        ("Arroz Tipo 1 - 5 kg", "ALI-001", "7891000000011", "Alimentos", 27.90m, 19.90m, 8m, 24m),
        ("Feijão Carioca - 1 kg", "ALI-002", "7891000000028", "Alimentos", 9.50m, 6.50m, 10m, 7m),
        ("Água Mineral - 500 ml", "BEB-001", "7891000000035", "Bebidas", 3.00m, 1.20m, 12m, 48m),
        ("Suco Integral - 1L", "BEB-002", "7891000000042", "Bebidas", 13.90m, 8.90m, 5m, 16m),
        ("Detergente Neutro", "HIG-001", "7891000000059", "Higiene e limpeza", 3.50m, 1.80m, 10m, 32m),
        ("Papel Higiênico - 12 rolos", "HIG-002", "7891000000066", "Higiene e limpeza", 19.90m, 12.50m, 6m, 5m)
    };
    var existing = await db.Products.Include(x => x.Inventory).ToDictionaryAsync(x => x.Sku);
    foreach (var definition in productDefinitions)
    {
        if (!existing.TryGetValue(definition.Item2, out var product))
        {
            product = new Product { Id = Guid.NewGuid(), CategoryId = categories[definition.Item4].Id, Name = definition.Item1, Sku = definition.Item2, Barcode = definition.Item3, SalePrice = definition.Item5, CostPrice = definition.Item6, MinimumStock = definition.Item7, IsActive = true, CreatedAt = now, UpdatedAt = now };
            product.Inventory = Forjix.Domain.Entities.Inventory.Inventory.Create(product.Id, now);
            db.Products.Add(product);
            existing.Add(product.Sku, product);
        }
    }
    await db.SaveChangesAsync();

    foreach (var definition in productDefinitions)
    {
        var product = existing[definition.Item2];
        if (await db.InventoryMovements.AnyAsync(x => x.ProductId == product.Id)) continue;
        var change = product.Inventory.ApplyMovement(InventoryMovementType.StockEntry, definition.Item8, false, now.AddDays(-7));
        db.InventoryMovements.Add(new InventoryMovement { InventoryId = product.Inventory.Id, ProductId = product.Id, Type = InventoryMovementType.StockEntry, Quantity = definition.Item8, PreviousQuantity = change.PreviousQuantity, NewQuantity = change.NewQuantity, Reason = "Estoque inicial demonstrativo", ReferenceType = "DevelopmentSeed", UserId = admin.Id, CreatedAt = now.AddDays(-7) });
    }
    await db.SaveChangesAsync();

    if (await db.Sales.AnyAsync()) return;
    var register = new CashRegister { Id = Guid.NewGuid(), Name = "Caixa principal" };
    var session = new CashSession { Id = Guid.NewGuid(), CashRegisterId = register.Id, OpenedByUserId = admin.Id, OpenedAt = now.AddHours(-4), OpeningAmount = 100m };
    session.Movements.Add(new CashMovement { Type = CashMovementType.Opening, Amount = 100m, Reason = "Abertura demonstrativa", UserId = admin.Id, CreatedAt = session.OpenedAt });
    register.Sessions.Add(session);
    db.CashRegisters.Add(register);

    var demoSales = new[]
    {
        (Product: existing["ALI-001"], Quantity: 2m, Payment: PaymentMethod.Pix, Minutes: 150),
        (Product: existing["BEB-001"], Quantity: 4m, Payment: PaymentMethod.Cash, Minutes: 85),
        (Product: existing["HIG-001"], Quantity: 3m, Payment: PaymentMethod.DebitCard, Minutes: 25)
    };
    var sequence = 0;
    foreach (var demo in demoSales)
    {
        sequence++;
        var createdAt = now.AddMinutes(-demo.Minutes);
        var total = demo.Product.SalePrice * demo.Quantity;
        var sale = new Sale { Id = Guid.NewGuid(), Number = $"VD-{createdAt:yyyyMMdd}-{sequence:00000}", IdempotencyKey = $"development-seed-{sequence}", Subtotal = total, Total = total, PaymentMethod = demo.Payment, UserId = admin.Id, CreatedAt = createdAt };
        sale.Items.Add(new SaleItem { Id = Guid.NewGuid(), ProductId = demo.Product.Id, ProductName = demo.Product.Name, Sku = demo.Product.Sku, Quantity = demo.Quantity, UnitPrice = demo.Product.SalePrice, UnitCost = demo.Product.CostPrice, Total = total });
        var change = demo.Product.Inventory.ApplyMovement(InventoryMovementType.Sale, demo.Quantity, false, createdAt);
        db.InventoryMovements.Add(new InventoryMovement { InventoryId = demo.Product.Inventory.Id, ProductId = demo.Product.Id, Type = InventoryMovementType.Sale, Quantity = demo.Quantity, PreviousQuantity = change.PreviousQuantity, NewQuantity = change.NewQuantity, Reason = "Venda demonstrativa", ReferenceType = nameof(Sale), ReferenceId = sale.Id.ToString(), UserId = admin.Id, CreatedAt = createdAt });
        if (demo.Payment == PaymentMethod.Cash) session.Movements.Add(new CashMovement { Type = CashMovementType.Sale, Amount = total, Reason = sale.Number, UserId = admin.Id, SaleId = sale.Id, CreatedAt = createdAt });
        db.Sales.Add(sale);
    }
    db.SaleSequences.Add(new SaleSequence { Date = DateOnly.FromDateTime(now.UtcDateTime), LastValue = sequence });
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
