using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Forjix.Application.Common;
using Forjix.Domain.Enums;
using Forjix.Infrastructure.Persistence.Master;
using Forjix.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Forjix.IntegrationTests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class SqlIntegrationCollection : ICollectionFixture<ForjixWebApplicationFactory>
{
    public const string Name = "SQL Server integration";
}

[Collection(SqlIntegrationCollection.Name)]
[Trait("Category", "SqlIntegration")]
public sealed class SqlServerAcceptanceTests(ForjixWebApplicationFactory factory)
{
    private const string TenantA = "empresa-a-ci";
    private const string TenantB = "empresa-b-ci";
    private const string AdminEmail = "admin@teste.local";
    private const string ViewerEmail = "viewer@teste.local";
    private static readonly string[] StockOnlyPermissions = ["stock.view", "stock.manage"];

    [SqlFact]
    public async Task MasterAndTenantMigrationsAreApplied()
    {
        await using var master = CreateMasterDb();
        Assert.NotEmpty(await master.Database.GetAppliedMigrationsAsync());
        Assert.Empty(await master.Database.GetPendingMigrationsAsync());

        await using var tenantA = CreateTenantDb(TenantA);
        await using var tenantB = CreateTenantDb(TenantB);
        Assert.NotEmpty(await tenantA.Database.GetAppliedMigrationsAsync());
        Assert.NotEmpty(await tenantB.Database.GetAppliedMigrationsAsync());
        Assert.Empty(await tenantA.Database.GetPendingMigrationsAsync());
        Assert.Empty(await tenantB.Database.GetPendingMigrationsAsync());
    }

    [SqlFact]
    public async Task RepeatedSeedIsIdempotentForBothTenants()
    {
        await using var master = CreateMasterDb();
        Assert.Equal(2, await master.Tenants.CountAsync(x => x.Slug == TenantA || x.Slug == TenantB));
        Assert.Equal(2, await master.Subscriptions.CountAsync(x => x.Status == SubscriptionStatus.Active));
        Assert.True(await master.MigrationExecutions.CountAsync(x => x.Status == "Succeeded") >= 4);

        await AssertTenantSeedCountsAsync(TenantA);
        await AssertTenantSeedCountsAsync(TenantB);
    }

    [SqlTheory]
    [InlineData("tenant-inexistente", AdminEmail, "configured", HttpStatusCode.Unauthorized)]
    [InlineData(TenantA, "nobody@teste.local", "configured", HttpStatusCode.Unauthorized)]
    [InlineData(TenantA, AdminEmail, "definitely-wrong", HttpStatusCode.Unauthorized)]
    public async Task InvalidCredentialsAreRejectedGenerically(
        string tenantSlug,
        string email,
        string passwordSource,
        HttpStatusCode expected)
    {
        var password = passwordSource == "configured" ? Password() : passwordSource;
        using var client = Client();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { tenantSlug, email, password });
        Assert.Equal(expected, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Credenciais inválidas.", problem.RootElement.GetProperty("detail").GetString());
    }

    [SqlFact]
    public async Task InactiveTenantCannotAuthenticate()
    {
        await using var master = CreateMasterDb();
        var tenant = await master.Tenants.SingleAsync(x => x.Slug == TenantA);
        tenant.Status = TenantStatus.Suspended;
        await master.SaveChangesAsync();

        try
        {
            using var client = Client();
            var response = await client.PostAsJsonAsync("/api/auth/login", new
            {
                tenantSlug = TenantA,
                email = AdminEmail,
                password = Password()
            });
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
        finally
        {
            tenant.Status = TenantStatus.Active;
            await master.SaveChangesAsync();
        }
    }

    [SqlFact]
    public async Task ValidLoginAndMeReturnTheCorrectTenantContext()
    {
        using var client = Client();
        var session = await LoginAsync(client, TenantA, AdminEmail);
        var me = await GetMeAsync(client, session.AccessToken);

        Assert.Equal(TenantA, me.RootElement.GetProperty("tenant").GetProperty("slug").GetString());
        Assert.Equal(AdminEmail, me.RootElement.GetProperty("user").GetProperty("email").GetString());
        Assert.Contains("tenant.a.marker", PermissionCodes(me));
        Assert.DoesNotContain("tenant.b.marker", PermissionCodes(me));
    }

    [SqlFact]
    public async Task RefreshRotatesTokenAndReplayRevokesTheFamily()
    {
        using var client = Client();
        var login = await LoginAsync(client, TenantA, AdminEmail);
        var refresh = await RefreshAsync(client, login.Cookie);

        Assert.NotEqual(login.AccessToken, refresh.AccessToken);
        var replay = await RefreshResponseAsync(client, login.Cookie);
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
        var replacementAfterReplay = await RefreshResponseAsync(client, refresh.Cookie);
        Assert.Equal(HttpStatusCode.Unauthorized, replacementAfterReplay.StatusCode);
    }

    [SqlFact]
    public async Task LogoutRevokesRefreshToken()
    {
        using var client = Client();
        var session = await LoginAsync(client, TenantA, AdminEmail);
        using var logout = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout")
        {
            Content = JsonContent.Create(new { })
        };
        logout.Headers.Add("Cookie", session.Cookie);
        Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(logout)).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshResponseAsync(client, session.Cookie)).StatusCode);
    }

    [SqlFact]
    public async Task PermissionPolicyReturnsForbiddenOrSuccessFromTenantDatabase()
    {
        using var client = Client();
        var viewer = await LoginAsync(client, TenantA, ViewerEmail);
        var administrator = await LoginAsync(client, TenantA, AdminEmail);

        Assert.Equal(HttpStatusCode.Forbidden,
            (await AuthorizedGetAsync(client, "/api/access-proof/administration-users", viewer.AccessToken)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await AuthorizedGetAsync(client, "/api/access-proof/administration-users", administrator.AccessToken)).StatusCode);
    }

    [SqlFact]
    public async Task SameEmailInTwoTenantsNeverCrossesIdentityOrPermissions()
    {
        using var client = Client();
        var sessionA = await LoginAsync(client, TenantA, AdminEmail);
        var sessionB = await LoginAsync(client, TenantB, AdminEmail);
        var meA = await GetMeAsync(client, sessionA.AccessToken);
        var meB = await GetMeAsync(client, sessionB.AccessToken);

        Assert.Equal("Empresa A CI", meA.RootElement.GetProperty("tenant").GetProperty("name").GetString());
        Assert.Equal("Empresa B CI", meB.RootElement.GetProperty("tenant").GetProperty("name").GetString());
        Assert.Contains("tenant.a.marker", PermissionCodes(meA));
        Assert.DoesNotContain("tenant.b.marker", PermissionCodes(meA));
        Assert.Contains("tenant.b.marker", PermissionCodes(meB));
        Assert.DoesNotContain("tenant.a.marker", PermissionCodes(meB));
    }

    [SqlFact]
    public async Task ConcurrentRequestsKeepTenantContextAndDatabaseIsolated()
    {
        using var client = Client();
        var sessionA = await LoginAsync(client, TenantA, AdminEmail);
        var sessionB = await LoginAsync(client, TenantB, AdminEmail);

        var requests = Enumerable.Range(0, 20).Select(async index =>
        {
            var expectedSlug = index % 2 == 0 ? TenantA : TenantB;
            var expectedMarker = index % 2 == 0 ? "tenant.a.marker" : "tenant.b.marker";
            var forbiddenMarker = index % 2 == 0 ? "tenant.b.marker" : "tenant.a.marker";
            var token = index % 2 == 0 ? sessionA.AccessToken : sessionB.AccessToken;
            var me = await GetMeAsync(client, token);
            Assert.Equal(expectedSlug, me.RootElement.GetProperty("tenant").GetProperty("slug").GetString());
            Assert.Contains(expectedMarker, PermissionCodes(me));
            Assert.DoesNotContain(forbiddenMarker, PermissionCodes(me));
        });

        await Task.WhenAll(requests);
    }

    [SqlFact]
    public async Task UserManagementEnforcesPermissionInactiveLoginAndTenantIsolation()
    {
        using var client = Client();
        var viewer = await LoginAsync(client, TenantA, ViewerEmail);
        Assert.Equal(HttpStatusCode.Forbidden, (await AuthorizedGetAsync(client, "/api/users", viewer.AccessToken)).StatusCode);

        var adminA = await LoginAsync(client, TenantA, AdminEmail);
        var uniqueEmail = $"inactive-{Guid.NewGuid():N}@teste.local";
        var create = await AuthorizedJsonAsync(client, HttpMethod.Post, "/api/users", adminA.AccessToken,
            new { name = "Usuário Inativo", email = uniqueEmail, password = Password(), isActive = false, roleIds = Array.Empty<Guid>() });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", new { tenantSlug = TenantA, email = uniqueEmail, password = Password() })).StatusCode);

        var adminB = await LoginAsync(client, TenantB, AdminEmail);
        using var usersB = await ReadJsonAsync(await AuthorizedGetAsync(client, "/api/users", adminB.AccessToken));
        Assert.DoesNotContain(usersB.RootElement.EnumerateArray(), x => x.GetProperty("email").GetString() == uniqueEmail);
    }

    [SqlFact]
    public async Task RolePermissionsCanBeAssignedAndRemoved()
    {
        using var client = Client();
        var admin = await LoginAsync(client, TenantA, AdminEmail);
        var name = $"Catálogo {Guid.NewGuid():N}";
        var create = await AuthorizedJsonAsync(client, HttpMethod.Post, "/api/roles", admin.AccessToken,
            new { name, description = "Teste", permissions = new[] { Permissions.ProductsView } });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        using var created = await ReadJsonAsync(create);
        var id = created.RootElement.GetProperty("id").GetGuid();
        Assert.Contains(Permissions.ProductsView, created.RootElement.GetProperty("permissions").EnumerateArray().Select(x => x.GetString()));

        var update = await AuthorizedJsonAsync(client, HttpMethod.Put, $"/api/roles/{id}", admin.AccessToken,
            new { name, description = "Sem permissões", permissions = Array.Empty<string>() });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        using var updated = await ReadJsonAsync(update);
        Assert.Empty(updated.RootElement.GetProperty("permissions").EnumerateArray());
    }

    [SqlFact]
    public async Task CategoriesRejectDuplicatesDeactivateAndRemainTenantIsolated()
    {
        using var client = Client();
        var adminA = await LoginAsync(client, TenantA, AdminEmail);
        var name = $"Categoria {Guid.NewGuid():N}";
        var payload = new { name, description = "Teste de integração", isActive = true };
        var create = await AuthorizedJsonAsync(client, HttpMethod.Post, "/api/categories", adminA.AccessToken, payload);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        using var created = await ReadJsonAsync(create);
        var id = created.RootElement.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.Conflict, (await AuthorizedJsonAsync(client, HttpMethod.Post, "/api/categories", adminA.AccessToken, payload)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await AuthorizedJsonAsync(client, HttpMethod.Delete, $"/api/categories/{id}", adminA.AccessToken, new { })).StatusCode);

        var adminB = await LoginAsync(client, TenantB, AdminEmail);
        using var categoriesB = await ReadJsonAsync(await AuthorizedGetAsync(client, "/api/categories", adminB.AccessToken));
        Assert.DoesNotContain(categoriesB.RootElement.EnumerateArray(), x => x.GetProperty("name").GetString() == name);
    }

    [SqlFact]
    public async Task ProductsValidateCategoryPricesAndSkuAndRemainTenantIsolated()
    {
        using var client = Client();
        var adminA = await LoginAsync(client, TenantA, AdminEmail);
        var categoryResponse = await AuthorizedJsonAsync(client, HttpMethod.Post, "/api/categories", adminA.AccessToken,
            new { name = $"Produtos {Guid.NewGuid():N}", description = "Teste", isActive = true });
        using var category = await ReadJsonAsync(categoryResponse);
        var categoryId = category.RootElement.GetProperty("id").GetGuid();
        var sku = $"SKU-{Guid.NewGuid():N}";
        object ProductPayload(Guid selectedCategory, decimal sale, decimal cost) => new { categoryId = selectedCategory, name = "Produto teste", sku, barcode = (string?)null, salePrice = sale, costPrice = cost, minimumStock = 1, isActive = true, rowVersion = (string?)null };

        Assert.Equal(HttpStatusCode.BadRequest, (await AuthorizedJsonAsync(client, HttpMethod.Post, "/api/products", adminA.AccessToken, ProductPayload(Guid.NewGuid(), 10, 5))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await AuthorizedJsonAsync(client, HttpMethod.Post, "/api/products", adminA.AccessToken, ProductPayload(categoryId, 0, 5))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await AuthorizedJsonAsync(client, HttpMethod.Post, "/api/products", adminA.AccessToken, ProductPayload(categoryId, 10, -1))).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await AuthorizedJsonAsync(client, HttpMethod.Post, "/api/products", adminA.AccessToken, ProductPayload(categoryId, 10, 5))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await AuthorizedJsonAsync(client, HttpMethod.Post, "/api/products", adminA.AccessToken, ProductPayload(categoryId, 10, 5))).StatusCode);

        var adminB = await LoginAsync(client, TenantB, AdminEmail);
        using var productsB = await ReadJsonAsync(await AuthorizedGetAsync(client, "/api/products", adminB.AccessToken));
        Assert.DoesNotContain(productsB.RootElement.GetProperty("items").EnumerateArray(), x => x.GetProperty("sku").GetString() == sku);
    }

    [SqlFact]
    public async Task InventoryIsCreatedMovesAtomicallyAndRemainsTenantIsolated()
    {
        using var client = Client();
        var adminA = await LoginAsync(client, TenantA, AdminEmail);
        var productId = await CreateProductAsync(client, adminA.AccessToken, "INV");

        using var initial = await ReadJsonAsync(await AuthorizedGetAsync(client, $"/api/inventory/{productId}", adminA.AccessToken));
        Assert.Equal(0, initial.RootElement.GetProperty("quantity").GetDecimal());
        var initialVersion = initial.RootElement.GetProperty("rowVersion").GetString();
        Assert.Equal(HttpStatusCode.BadRequest, (await AuthorizedJsonAsync(client, HttpMethod.Post, $"/api/inventory/{productId}/movements", adminA.AccessToken,
            new { type = "StockEntry", quantity = 0, reason = "Inválido", rowVersion = initialVersion })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await AuthorizedJsonAsync(client, HttpMethod.Post, $"/api/inventory/{productId}/movements", adminA.AccessToken,
            new { type = "PositiveAdjustment", quantity = 1, reason = (string?)null, rowVersion = initialVersion })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await AuthorizedGetAsync(client, $"/api/inventory/{Guid.NewGuid()}", adminA.AccessToken)).StatusCode);

        var entry = await AuthorizedJsonAsync(client, HttpMethod.Post, $"/api/inventory/{productId}/movements", adminA.AccessToken,
            new { type = "StockEntry", quantity = 10, reason = "Estoque inicial", rowVersion = initialVersion });
        Assert.Equal(HttpStatusCode.OK, entry.StatusCode);
        using var entryResult = await ReadJsonAsync(entry);
        var currentVersion = entryResult.RootElement.GetProperty("inventory").GetProperty("rowVersion").GetString();

        await using var dbA = CreateTenantDb(TenantA);
        Assert.Equal(10, await dbA.Inventories.Where(x => x.ProductId == productId).Select(x => x.Quantity).SingleAsync());
        Assert.Equal(1, await dbA.InventoryMovements.CountAsync(x => x.ProductId == productId));
        var failed = await AuthorizedJsonAsync(client, HttpMethod.Post, $"/api/inventory/{productId}/movements", adminA.AccessToken,
            new { type = "StockExit", quantity = 11, reason = "Deve falhar", rowVersion = currentVersion });
        Assert.Equal(HttpStatusCode.BadRequest, failed.StatusCode);
        Assert.Equal(10, await dbA.Inventories.Where(x => x.ProductId == productId).Select(x => x.Quantity).SingleAsync());
        Assert.Equal(1, await dbA.InventoryMovements.CountAsync(x => x.ProductId == productId));
        Assert.Equal(1, await dbA.AuditLogs.CountAsync(x => x.EntityName == "InventoryMovement" && x.EntityId == productId.ToString()));

        var adminB = await LoginAsync(client, TenantB, AdminEmail);
        Assert.Equal(HttpStatusCode.NotFound, (await AuthorizedGetAsync(client, $"/api/inventory/{productId}", adminB.AccessToken)).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await AuthorizedJsonAsync(client, HttpMethod.Delete, $"/api/products/{productId}", adminA.AccessToken, new { })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await AuthorizedJsonAsync(client, HttpMethod.Post, $"/api/inventory/{productId}/movements", adminA.AccessToken,
            new { type = "StockEntry", quantity = 1, reason = "Produto inativo", rowVersion = currentVersion })).StatusCode);
    }

    [SqlFact]
    public async Task ConcurrentStockExitsNeverProduceNegativeBalance()
    {
        using var client = Client();
        var admin = await LoginAsync(client, TenantA, AdminEmail);
        var productId = await CreateProductAsync(client, admin.AccessToken, "CON");
        using var initial = await ReadJsonAsync(await AuthorizedGetAsync(client, $"/api/inventory/{productId}", admin.AccessToken));
        var initialVersion = initial.RootElement.GetProperty("rowVersion").GetString();
        var entry = await AuthorizedJsonAsync(client, HttpMethod.Post, $"/api/inventory/{productId}/movements", admin.AccessToken,
            new { type = "StockEntry", quantity = 1, reason = "Concorrência", rowVersion = initialVersion });
        using var entryResult = await ReadJsonAsync(entry);
        var currentVersion = entryResult.RootElement.GetProperty("inventory").GetProperty("rowVersion").GetString();

        var exits = await Task.WhenAll(
            AuthorizedJsonAsync(client, HttpMethod.Post, $"/api/inventory/{productId}/movements", admin.AccessToken, new { type = "StockExit", quantity = 1, reason = "A", rowVersion = currentVersion }),
            AuthorizedJsonAsync(client, HttpMethod.Post, $"/api/inventory/{productId}/movements", admin.AccessToken, new { type = "StockExit", quantity = 1, reason = "B", rowVersion = currentVersion }));

        Assert.Single(exits, x => x.StatusCode == HttpStatusCode.OK);
        Assert.Single(exits, x => x.StatusCode == HttpStatusCode.Conflict);
        await using var db = CreateTenantDb(TenantA);
        Assert.Equal(0, await db.Inventories.Where(x => x.ProductId == productId).Select(x => x.Quantity).SingleAsync());
        Assert.Equal(2, await db.InventoryMovements.CountAsync(x => x.ProductId == productId));
    }

    [SqlFact]
    public async Task SaleIsIdempotentMovesStockAndCancellationRestoresIt()
    {
        using var client = Client();
        var admin = await LoginAsync(client, TenantA, AdminEmail);
        await EnsureCashClosedAsync(client, admin.AccessToken);
        Assert.Equal(HttpStatusCode.Created, (await AuthorizedJsonAsync(client, HttpMethod.Post, "/api/cash/open", admin.AccessToken, new { openingAmount = 100 })).StatusCode);
        var productId = await CreateProductAsync(client, admin.AccessToken, "SALE");
        using var initial = await ReadJsonAsync(await AuthorizedGetAsync(client, $"/api/inventory/{productId}", admin.AccessToken));
        var entry = await AuthorizedJsonAsync(client, HttpMethod.Post, $"/api/inventory/{productId}/movements", admin.AccessToken,
            new { type = "StockEntry", quantity = 20, reason = "Venda", rowVersion = initial.RootElement.GetProperty("rowVersion").GetString() });
        Assert.Equal(HttpStatusCode.OK, entry.StatusCode);
        var key = Guid.NewGuid().ToString("N");
        var payload = new { paymentMethod = "Pix", discount = 2, customerId = (Guid?)null, items = new[] { new { productId, quantity = 2 } } };
        var first = await AuthorizedIdempotentJsonAsync(client, "/api/sales", admin.AccessToken, key, payload);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        using var created = await ReadJsonAsync(first);
        var saleId = created.RootElement.GetProperty("id").GetGuid();
        var repeated = await AuthorizedIdempotentJsonAsync(client, "/api/sales", admin.AccessToken, key, payload);
        Assert.Equal(HttpStatusCode.Created, repeated.StatusCode);
        using var repeatedSale = await ReadJsonAsync(repeated);
        Assert.Equal(saleId, repeatedSale.RootElement.GetProperty("id").GetGuid());

        await using var db = CreateTenantDb(TenantA);
        Assert.Equal(18, await db.Inventories.Where(x => x.ProductId == productId).Select(x => x.Quantity).SingleAsync());
        Assert.Equal(1, await db.Sales.CountAsync(x => x.Id == saleId));
        var cancel = await AuthorizedJsonAsync(client, HttpMethod.Post, $"/api/sales/{saleId}/cancel", admin.AccessToken,
            new { reason = "Teste", rowVersion = created.RootElement.GetProperty("rowVersion").GetString() });
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
        Assert.Equal(20, await db.Inventories.Where(x => x.ProductId == productId).Select(x => x.Quantity).SingleAsync());
        Assert.Equal(HttpStatusCode.Conflict, (await AuthorizedJsonAsync(client, HttpMethod.Post, $"/api/sales/{saleId}/cancel", admin.AccessToken,
            new { reason = "Novamente", rowVersion = created.RootElement.GetProperty("rowVersion").GetString() })).StatusCode);

        var adminB = await LoginAsync(client, TenantB, AdminEmail);
        Assert.Equal(HttpStatusCode.NotFound, (await AuthorizedGetAsync(client, $"/api/sales/{saleId}", adminB.AccessToken)).StatusCode);
        await EnsureCashClosedAsync(client, admin.AccessToken);
    }

    [SqlFact]
    public async Task SaleDiscountPermissionAndInsufficientStockRollbackAreEnforced()
    {
        using var client = Client();
        var admin = await LoginAsync(client, TenantA, AdminEmail);
        await EnsureCashClosedAsync(client, admin.AccessToken);
        Assert.Equal(HttpStatusCode.Created, (await AuthorizedJsonAsync(client, HttpMethod.Post, "/api/cash/open", admin.AccessToken, new { openingAmount = 0 })).StatusCode);
        var productId = await CreateProductAsync(client, admin.AccessToken, "LIMIT");
        using var inventory = await ReadJsonAsync(await AuthorizedGetAsync(client, $"/api/inventory/{productId}", admin.AccessToken));
        Assert.Equal(HttpStatusCode.OK, (await AuthorizedJsonAsync(client, HttpMethod.Post, $"/api/inventory/{productId}/movements", admin.AccessToken,
            new { type = "StockEntry", quantity = 1, reason = "Limite", rowVersion = inventory.RootElement.GetProperty("rowVersion").GetString() })).StatusCode);

        var roleResponse = await AuthorizedJsonAsync(client, HttpMethod.Post, "/api/roles", admin.AccessToken,
            new { name = $"Operador {Guid.NewGuid():N}", description = "Sem desconto", permissions = new[] { Permissions.SalesCreate } });
        using var role = await ReadJsonAsync(roleResponse);
        var email = $"operador-{Guid.NewGuid():N}@teste.local";
        Assert.Equal(HttpStatusCode.Created, (await AuthorizedJsonAsync(client, HttpMethod.Post, "/api/users", admin.AccessToken,
            new { name = "Operador sem desconto", email, password = Password(), isActive = true, roleIds = new[] { role.RootElement.GetProperty("id").GetGuid() } })).StatusCode);
        var limited = await LoginAsync(client, TenantA, email);

        var discounted = new { paymentMethod = "Pix", discount = 1, customerId = (Guid?)null, items = new[] { new { productId, quantity = 1 } } };
        Assert.Equal(HttpStatusCode.Forbidden, (await AuthorizedIdempotentJsonAsync(client, "/api/sales", limited.AccessToken, Guid.NewGuid().ToString("N"), discounted)).StatusCode);
        var insufficient = new { paymentMethod = "Pix", discount = 0, customerId = (Guid?)null, items = new[] { new { productId, quantity = 2 } } };
        Assert.Equal(HttpStatusCode.BadRequest, (await AuthorizedIdempotentJsonAsync(client, "/api/sales", limited.AccessToken, Guid.NewGuid().ToString("N"), insufficient)).StatusCode);
        await using var db = CreateTenantDb(TenantA);
        Assert.Equal(1, await db.Inventories.Where(x => x.ProductId == productId).Select(x => x.Quantity).SingleAsync());
        Assert.False(await db.Sales.AnyAsync(x => x.Items.Any(i => i.ProductId == productId)));
        await EnsureCashClosedAsync(client, admin.AccessToken);
    }

    [SqlFact]
    public async Task CashSupportsOpenSupplyWithdrawalAndClose()
    {
        using var client = Client(); var admin = await LoginAsync(client, TenantA, AdminEmail); await EnsureCashClosedAsync(client, admin.AccessToken);
        var openedResponse = await AuthorizedJsonAsync(client, HttpMethod.Post, "/api/cash/open", admin.AccessToken, new { openingAmount = 100 }); Assert.Equal(HttpStatusCode.Created, openedResponse.StatusCode); using var opened = await ReadJsonAsync(openedResponse); var version = opened.RootElement.GetProperty("rowVersion").GetString();
        Assert.Equal(HttpStatusCode.Conflict, (await AuthorizedJsonAsync(client, HttpMethod.Post, "/api/cash/open", admin.AccessToken, new { openingAmount = 0 })).StatusCode);
        var supplyResponse = await AuthorizedJsonAsync(client, HttpMethod.Post, "/api/cash/supply", admin.AccessToken, new { amount = 25, reason = "Troco", rowVersion = version }); using var supplied = await ReadJsonAsync(supplyResponse); version = supplied.RootElement.GetProperty("rowVersion").GetString();
        var withdrawalResponse = await AuthorizedJsonAsync(client, HttpMethod.Post, "/api/cash/withdraw", admin.AccessToken, new { amount = 10, reason = "Despesa", rowVersion = version }); using var withdrawn = await ReadJsonAsync(withdrawalResponse); version = withdrawn.RootElement.GetProperty("rowVersion").GetString(); Assert.Equal(115, withdrawn.RootElement.GetProperty("expectedAmount").GetDecimal());
        Assert.Equal(HttpStatusCode.OK, (await AuthorizedJsonAsync(client, HttpMethod.Post, "/api/cash/close", admin.AccessToken, new { closingAmount = 115, rowVersion = version })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await AuthorizedJsonAsync(client, HttpMethod.Post, "/api/cash/supply", admin.AccessToken, new { amount = 1, reason = "Fechado", rowVersion = version })).StatusCode);
    }

    [SqlFact]
    public async Task DashboardReportsAndExportsUseTenantData()
    {
        using var client = Client(); var admin = await LoginAsync(client, TenantA, AdminEmail); await EnsureCashClosedAsync(client, admin.AccessToken); Assert.Equal(HttpStatusCode.Created, (await AuthorizedJsonAsync(client, HttpMethod.Post, "/api/cash/open", admin.AccessToken, new { openingAmount = 0 })).StatusCode);
        var from = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(-1).ToString("O", CultureInfo.InvariantCulture)); var through = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(1).ToString("O", CultureInfo.InvariantCulture)); using var before = await ReadJsonAsync(await AuthorizedGetAsync(client, $"/api/reports?from={from}&through={through}", admin.AccessToken)); var revenue = before.RootElement.GetProperty("revenue").GetDecimal(); var count = before.RootElement.GetProperty("saleCount").GetInt32();
        var productId = await CreateProductAsync(client, admin.AccessToken, "REP"); using var stock = await ReadJsonAsync(await AuthorizedGetAsync(client, $"/api/inventory/{productId}", admin.AccessToken)); Assert.Equal(HttpStatusCode.OK, (await AuthorizedJsonAsync(client, HttpMethod.Post, $"/api/inventory/{productId}/movements", admin.AccessToken, new { type = "StockEntry", quantity = 5, reason = "Relatório", rowVersion = stock.RootElement.GetProperty("rowVersion").GetString() })).StatusCode);
        var sale = await AuthorizedIdempotentJsonAsync(client, "/api/sales", admin.AccessToken, Guid.NewGuid().ToString("N"), new { paymentMethod = "Cash", discount = 0, customerId = (Guid?)null, items = new[] { new { productId, quantity = 2 } } }); Assert.Equal(HttpStatusCode.Created, sale.StatusCode);
        using var after = await ReadJsonAsync(await AuthorizedGetAsync(client, $"/api/reports?from={from}&through={through}", admin.AccessToken)); Assert.Equal(revenue + 20, after.RootElement.GetProperty("revenue").GetDecimal()); Assert.Equal(count + 1, after.RootElement.GetProperty("saleCount").GetInt32()); using var dashboard = await ReadJsonAsync(await AuthorizedGetAsync(client, "/api/dashboard", admin.AccessToken)); Assert.True(dashboard.RootElement.GetProperty("saleCountToday").GetInt32() > 0);
        using var excel = await AuthorizedGetAsync(client, $"/api/reports/export?from={from}&through={through}&format=excel", admin.AccessToken); Assert.Equal(HttpStatusCode.OK, excel.StatusCode); Assert.Contains("ms-excel", excel.Content.Headers.ContentType?.MediaType, StringComparison.OrdinalIgnoreCase);
        using var pdf = await AuthorizedGetAsync(client, $"/api/reports/export?from={from}&through={through}&format=pdf", admin.AccessToken); Assert.Equal("%PDF", (await pdf.Content.ReadAsStringAsync())[..4]); await EnsureCashClosedAsync(client, admin.AccessToken);
    }

    [SqlFact]
    public async Task TenantSettingsAreAuditedAndIsolated()
    {
        using var client = Client(); var adminA = await LoginAsync(client, TenantA, AdminEmail); using var original = await ReadJsonAsync(await AuthorizedGetAsync(client, "/api/settings", adminA.AccessToken)); var originalName = original.RootElement.GetProperty("tradeName").GetString()!;
        var changed = $"Empresa A {Guid.NewGuid():N}"; var update = await AuthorizedJsonAsync(client, HttpMethod.Put, "/api/settings", adminA.AccessToken, new { tradeName = changed, legalName = "Empresa A Testes Ltda", cnpj = (string?)null, phone = "11999999999", email = "contato@empresa-a.local", address = "Rua de teste", allowNegativeStock = false, currency = "BRL", timeZone = "America/Sao_Paulo" }); Assert.True(update.StatusCode == HttpStatusCode.OK, await update.Content.ReadAsStringAsync());
        var adminB = await LoginAsync(client, TenantB, AdminEmail); using var settingsB = await ReadJsonAsync(await AuthorizedGetAsync(client, "/api/settings", adminB.AccessToken)); Assert.NotEqual(changed, settingsB.RootElement.GetProperty("tradeName").GetString()); await using var db = CreateTenantDb(TenantA); Assert.True(await db.AuditLogs.AnyAsync(x => x.Action == AuditAction.TenantSettingsUpdated));
        Assert.Equal(HttpStatusCode.OK, (await AuthorizedJsonAsync(client, HttpMethod.Put, "/api/settings", adminA.AccessToken, new { tradeName = originalName, legalName = (string?)null, cnpj = (string?)null, phone = (string?)null, email = (string?)null, address = (string?)null, allowNegativeStock = false, currency = "BRL", timeZone = "America/Sao_Paulo" })).StatusCode);
    }

    [SqlFact]
    public async Task CustomersSupportCrudAndRemainTenantIsolated()
    {
        using var client = Client(); var adminA = await LoginAsync(client, TenantA, AdminEmail);
        var document = Random.Shared.NextInt64(10_000_000_000, 99_999_999_999).ToString(CultureInfo.InvariantCulture);
        var create = await AuthorizedJsonAsync(client, HttpMethod.Post, "/api/customers", adminA.AccessToken, new { name = "Cliente SQL", document, email = "cliente@teste.local", phone = "11999999999", notes = "Teste", isActive = true });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode); using var created = await ReadJsonAsync(create); var id = created.RootElement.GetProperty("id").GetGuid();
        var update = await AuthorizedJsonAsync(client, HttpMethod.Put, $"/api/customers/{id}", adminA.AccessToken, new { name = "Cliente Atualizado", document, email = "cliente@teste.local", phone = "11999999999", notes = "Atualizado", isActive = false });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode); using var updated = await ReadJsonAsync(update); Assert.False(updated.RootElement.GetProperty("isActive").GetBoolean());
        var adminB = await LoginAsync(client, TenantB, AdminEmail); using var listB = await ReadJsonAsync(await AuthorizedGetAsync(client, $"/api/customers?search={document}", adminB.AccessToken));
        Assert.Equal(0, listB.RootElement.GetProperty("total").GetInt32());
    }

    [SqlFact]
    public async Task SuppliersAndPurchaseReceiptMoveStockAndRemainTenantIsolated()
    {
        using var client = Client(); var admin = await LoginAsync(client, TenantA, AdminEmail); var doc = Random.Shared.NextInt64(10_000_000_000, 99_999_999_999).ToString(CultureInfo.InvariantCulture);
        var supplierResponse = await AuthorizedJsonAsync(client, HttpMethod.Post, "/api/suppliers", admin.AccessToken, new { name = "Fornecedor SQL", document = doc, email = "fornecedor@teste.local", phone = "11999999999", contactName = "Contato", notes = "Teste", isActive = true });
        Assert.Equal(HttpStatusCode.Created, supplierResponse.StatusCode); using var supplier = await ReadJsonAsync(supplierResponse); var supplierId = supplier.RootElement.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await AuthorizedJsonAsync(client, HttpMethod.Put, $"/api/suppliers/{supplierId}", admin.AccessToken, new { name = "Fornecedor Atualizado", document = doc, email = "fornecedor@teste.local", phone = "11999999999", contactName = "Contato", notes = "Atualizado", isActive = true })).StatusCode);
        var productId = await CreateProductAsync(client, admin.AccessToken, "PUR"); var purchaseResponse = await AuthorizedJsonAsync(client, HttpMethod.Post, "/api/purchases", admin.AccessToken, new { supplierId, notes = "Compra SQL", items = new[] { new { productId, quantity = 50, unitCost = 4 } } });
        Assert.Equal(HttpStatusCode.Created, purchaseResponse.StatusCode); using var purchase = await ReadJsonAsync(purchaseResponse); var purchaseId = purchase.RootElement.GetProperty("id").GetGuid(); var version = purchase.RootElement.GetProperty("rowVersion").GetString();
        Assert.Equal(HttpStatusCode.OK, (await AuthorizedJsonAsync(client, HttpMethod.Post, $"/api/purchases/{purchaseId}/receive", admin.AccessToken, new { rowVersion = version })).StatusCode);
        await using var db = CreateTenantDb(TenantA); Assert.Equal(50, await db.Inventories.Where(x => x.ProductId == productId).Select(x => x.Quantity).SingleAsync()); Assert.Equal(1, await db.InventoryMovements.CountAsync(x => x.ProductId == productId && x.Type == InventoryMovementType.Purchase));
        Assert.Equal(HttpStatusCode.Conflict, (await AuthorizedJsonAsync(client, HttpMethod.Post, $"/api/purchases/{purchaseId}/cancel", admin.AccessToken, new { rowVersion = version })).StatusCode);
        var adminB = await LoginAsync(client, TenantB, AdminEmail); Assert.Equal(HttpStatusCode.NotFound, (await AuthorizedGetAsync(client, $"/api/purchases/{purchaseId}", adminB.AccessToken)).StatusCode); using var suppliersB = await ReadJsonAsync(await AuthorizedGetAsync(client, $"/api/suppliers?search={doc}", adminB.AccessToken)); Assert.Equal(0, suppliersB.RootElement.GetProperty("total").GetInt32());
    }

    [SqlFact]
    public async Task FinancialInstallmentsPaymentsReversalsAndTenantIsolation()
    {
        using var client = Client(); var admin = await LoginAsync(client, TenantA, AdminEmail);
        var token = admin.AccessToken; var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var categoryId = await CreateFinancialCategoryAsync(client, token, "Income");
        var create = await AuthorizedJsonAsync(client, HttpMethod.Post, "/api/financial/accounts-receivable", token,
            new { financialCategoryId = categoryId, description = "Parcelamento SQL", amount = 100m, issueDate = today.AddDays(-2), dueDate = today.AddDays(-1), installments = 3 });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode); using var accounts = await ReadJsonAsync(create);
        var rows = accounts.RootElement.EnumerateArray().ToArray(); Assert.Equal(3, rows.Length);
        Assert.Equal(100m, rows.Sum(x => x.GetProperty("originalAmount").GetDecimal()));
        Assert.Equal(33.34m, rows[0].GetProperty("originalAmount").GetDecimal());
        Assert.Equal("Overdue", rows[0].GetProperty("status").GetString());
        var id = rows[0].GetProperty("id").GetGuid(); var path = $"/api/financial/accounts-receivable/{id}";
        var version = rows[0].GetProperty("rowVersion").GetString();
        var other = await LoginAsync(client, TenantB, AdminEmail);
        Assert.Equal(HttpStatusCode.NotFound, (await AuthorizedGetAsync(client, path, other.AccessToken)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await AuthorizedJsonAsync(client, HttpMethod.Post, "/api/financial/accounts-receivable", other.AccessToken,
            new { financialCategoryId = categoryId, description = "Não pode cruzar tenant", amount = 10m, issueDate = today, dueDate = today, installments = 1 })).StatusCode);
        var viewer = await LoginAsync(client, TenantA, ViewerEmail);
        Assert.Equal(HttpStatusCode.Forbidden, (await AuthorizedGetAsync(client, "/api/financial/dashboard", viewer.AccessToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await AuthorizedJsonAsync(client, HttpMethod.Post, path + "/payments", viewer.AccessToken, new { })).StatusCode);
        var key = Guid.NewGuid().ToString("N");
        var body = new { amount = 10m, paymentDate = today, paymentMethod = "Pix", discount = 0m, interest = 0m, penalty = 0m, rowVersion = version };
        using var partial = await ReadJsonAsync(await AuthorizedIdempotentJsonAsync(client, path + "/payments", token, key, body));
        Assert.Equal(23.34m, partial.RootElement.GetProperty("openAmount").GetDecimal());
        Assert.Equal(HttpStatusCode.OK, (await AuthorizedIdempotentJsonAsync(client, path + "/payments", token, key, body)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await AuthorizedIdempotentJsonAsync(client, path + "/payments", token, Guid.NewGuid().ToString("N"), body)).StatusCode);
        using var fresh = await ReadJsonAsync(await AuthorizedGetAsync(client, path, token));
        var current = fresh.RootElement.GetProperty("rowVersion").GetString();
        Assert.Equal(HttpStatusCode.Conflict, (await AuthorizedIdempotentJsonAsync(client, path + "/payments", token, Guid.NewGuid().ToString("N"),
            new { amount = 50m, paymentDate = today, paymentMethod = "Pix", rowVersion = current })).StatusCode);
        using var paid = await ReadJsonAsync(await AuthorizedIdempotentJsonAsync(client, path + "/payments", token, Guid.NewGuid().ToString("N"),
            new { amount = 23.34m, paymentDate = today, paymentMethod = "Pix", rowVersion = current }));
        Assert.Equal("Paid", paid.RootElement.GetProperty("status").GetString());
        Assert.Equal(0m, paid.RootElement.GetProperty("openAmount").GetDecimal());
        using var payments = await ReadJsonAsync(await AuthorizedGetAsync(client, path + "/payments", token));
        Assert.Equal(2, payments.RootElement.GetArrayLength());
        foreach (var payment in payments.RootElement.EnumerateArray())
        {
            var reversalPath = path + $"/payments/{payment.GetProperty("id").GetGuid()}/reverse";
            var reversal = new { reason = "Teste de estorno", rowVersion = payment.GetProperty("rowVersion").GetString() };
            Assert.Equal(HttpStatusCode.OK, (await AuthorizedJsonAsync(client, HttpMethod.Post, reversalPath, token, reversal)).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await AuthorizedJsonAsync(client, HttpMethod.Post, reversalPath, token, reversal)).StatusCode);
        }
        using var restored = await ReadJsonAsync(await AuthorizedGetAsync(client, path, token));
        Assert.Equal(33.34m, restored.RootElement.GetProperty("openAmount").GetDecimal());
        Assert.Equal(HttpStatusCode.OK, (await AuthorizedJsonAsync(client, HttpMethod.Post, path + "/cancel", token,
            new { reason = "Sem obrigação", rowVersion = restored.RootElement.GetProperty("rowVersion").GetString() })).StatusCode);
        using var filtered = await ReadJsonAsync(await AuthorizedGetAsync(client, $"/api/financial/accounts-receivable?categoryId={categoryId}&status=Cancelled", token));
        Assert.Equal(1, filtered.RootElement.GetProperty("total").GetInt32());
        await using var db = CreateTenantDb(TenantA);
        Assert.Equal(2, await db.FinancialPayments.CountAsync(x => x.AccountReceivableId == id));
        Assert.True(await db.AuditLogs.AnyAsync(x => x.Action == AuditAction.FinancialPaymentReversed));
    }

    [SqlFact]
    public async Task FinancialCashWritesAreAtomicAndReversalsRestoreTheCashBalance()
    {
        using var client = Client(); var admin = await LoginAsync(client, TenantA, AdminEmail); var token = admin.AccessToken;
        await EnsureCashClosedAsync(client, token); var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var categoryId = await CreateFinancialCategoryAsync(client, token, "Expense");
        using var created = await ReadJsonAsync(await AuthorizedJsonAsync(client, HttpMethod.Post, "/api/financial/accounts-payable", token,
            new { financialCategoryId = categoryId, description = "Despesa em dinheiro", amount = 30m, issueDate = today, dueDate = today, installments = 1 }));
        var account = created.RootElement[0]; var id = account.GetProperty("id").GetGuid(); var path = $"/api/financial/accounts-payable/{id}";
        var key = Guid.NewGuid().ToString("N"); var body = new { amount = 30m, paymentDate = today, paymentMethod = "Cash", rowVersion = account.GetProperty("rowVersion").GetString() };
        Assert.Equal(HttpStatusCode.Conflict, (await AuthorizedIdempotentJsonAsync(client, path + "/payments", token, key, body)).StatusCode);
        await using (var db = CreateTenantDb(TenantA))
        {
            Assert.Equal(30m, await db.AccountsPayable.Where(x => x.Id == id).Select(x => x.OpenAmount).SingleAsync());
            Assert.False(await db.FinancialPayments.AnyAsync(x => x.AccountPayableId == id));
        }
        Assert.Equal(HttpStatusCode.Created, (await AuthorizedJsonAsync(client, HttpMethod.Post, "/api/cash/open", token, new { openingAmount = 100m })).StatusCode);
        using var paid = await ReadJsonAsync(await AuthorizedIdempotentJsonAsync(client, path + "/payments", token, key, body));
        using var cash = await ReadJsonAsync(await AuthorizedGetAsync(client, "/api/cash/current", token));
        Assert.Equal(70m, cash.RootElement.GetProperty("expectedAmount").GetDecimal());
        using var payments = await ReadJsonAsync(await AuthorizedGetAsync(client, path + "/payments", token)); var payment = payments.RootElement[0];
        Assert.Equal(HttpStatusCode.OK, (await AuthorizedJsonAsync(client, HttpMethod.Post, path + $"/payments/{payment.GetProperty("id").GetGuid()}/reverse", token,
            new { reason = "Correção", rowVersion = payment.GetProperty("rowVersion").GetString() })).StatusCode);
        using var cashRestored = await ReadJsonAsync(await AuthorizedGetAsync(client, "/api/cash/current", token));
        Assert.Equal(100m, cashRestored.RootElement.GetProperty("expectedAmount").GetDecimal());
        var income = await CreateFinancialCategoryAsync(client, token, "Income");
        using var receivable = await ReadJsonAsync(await AuthorizedJsonAsync(client, HttpMethod.Post, "/api/financial/accounts-receivable", token,
            new { financialCategoryId = income, description = "Recebimento em dinheiro", amount = 20m, issueDate = today, dueDate = today, installments = 1 }));
        var receipt = receivable.RootElement[0];
        var receiptPath = $"/api/financial/accounts-receivable/{receipt.GetProperty("id").GetGuid()}";
        Assert.Equal(HttpStatusCode.OK, (await AuthorizedIdempotentJsonAsync(client, receiptPath + "/payments", token, Guid.NewGuid().ToString("N"),
            new { amount = 20m, paymentDate = today, paymentMethod = "Cash", rowVersion = receipt.GetProperty("rowVersion").GetString() })).StatusCode);
        using var cashIncome = await ReadJsonAsync(await AuthorizedGetAsync(client, "/api/cash/current", token));
        Assert.Equal(120m, cashIncome.RootElement.GetProperty("expectedAmount").GetDecimal());
        await EnsureCashClosedAsync(client, token);
    }

    [SqlFact]
    public async Task FinancialDashboardKeepsPaymentsInTheirPeriodAndDeductsReversalsInTheCurrentPeriod()
    {
        using var client = Client(); var admin = await LoginAsync(client, TenantA, AdminEmail); var token = admin.AccessToken;
        var today = DateOnly.FromDateTime(DateTime.UtcNow); var yesterday = today.AddDays(-1);
        var category = await CreateFinancialCategoryAsync(client, token, "Income");
        var oldDashboard = $"/api/financial/dashboard?from={yesterday:yyyy-MM-dd}&through={yesterday:yyyy-MM-dd}";
        var currentDashboard = $"/api/financial/dashboard?from={today:yyyy-MM-dd}&through={today:yyyy-MM-dd}";
        using var baselineOld = await ReadJsonAsync(await AuthorizedGetAsync(client, oldDashboard, token));
        using var baselineToday = await ReadJsonAsync(await AuthorizedGetAsync(client, currentDashboard, token));
        using var accounts = await ReadJsonAsync(await AuthorizedJsonAsync(client, HttpMethod.Post, "/api/financial/accounts-receivable", token,
            new { financialCategoryId = category, description = "Fluxo por período", amount = 10m, issueDate = yesterday, dueDate = today, installments = 1 }));
        var account = accounts.RootElement[0]; var id = account.GetProperty("id").GetGuid();
        var path = $"/api/financial/accounts-receivable/{id}";
        Assert.Equal(HttpStatusCode.OK, (await AuthorizedIdempotentJsonAsync(client, path + "/payments", token, Guid.NewGuid().ToString("N"),
            new { amount = 10m, paymentDate = yesterday, paymentMethod = "Pix", rowVersion = account.GetProperty("rowVersion").GetString() })).StatusCode);
        using var payments = await ReadJsonAsync(await AuthorizedGetAsync(client, path + "/payments", token)); var payment = payments.RootElement[0];
        Assert.Equal(HttpStatusCode.OK, (await AuthorizedJsonAsync(client, HttpMethod.Post, path + $"/payments/{payment.GetProperty("id").GetGuid()}/reverse", token,
            new { reason = "Estorno em período diferente", rowVersion = payment.GetProperty("rowVersion").GetString() })).StatusCode);
        using var afterOld = await ReadJsonAsync(await AuthorizedGetAsync(client, oldDashboard, token));
        using var afterToday = await ReadJsonAsync(await AuthorizedGetAsync(client, currentDashboard, token));
        Assert.Equal(baselineOld.RootElement.GetProperty("received").GetDecimal() + 10m, afterOld.RootElement.GetProperty("received").GetDecimal());
        Assert.Equal(baselineToday.RootElement.GetProperty("received").GetDecimal() - 10m, afterToday.RootElement.GetProperty("received").GetDecimal());
    }

    [SqlFact]
    public async Task DeferredSalesAndReceivedPurchasesGenerateFinancialAccountsExactlyOnce()
    {
        using var client = Client(); var admin = await LoginAsync(client, TenantA, AdminEmail); var token = admin.AccessToken;
        var today = DateOnly.FromDateTime(DateTime.UtcNow); await EnsureCashClosedAsync(client, token);
        Assert.Equal(HttpStatusCode.Created, (await AuthorizedJsonAsync(client, HttpMethod.Post, "/api/cash/open", token, new { openingAmount = 0 })).StatusCode);
        var income = await CreateFinancialCategoryAsync(client, token, "Income"); var expense = await CreateFinancialCategoryAsync(client, token, "Expense");
        using var customer = await ReadJsonAsync(await AuthorizedJsonAsync(client, HttpMethod.Post, "/api/customers", token,
            new { name = "Cliente Financeiro", document = Random.Shared.NextInt64(10_000_000_000, 99_999_999_999).ToString(CultureInfo.InvariantCulture), isActive = true }));
        using var supplier = await ReadJsonAsync(await AuthorizedJsonAsync(client, HttpMethod.Post, "/api/suppliers", token,
            new { name = "Fornecedor Financeiro", document = Random.Shared.NextInt64(10_000_000_000, 99_999_999_999).ToString(CultureInfo.InvariantCulture), isActive = true }));
        var customerId = customer.RootElement.GetProperty("id").GetGuid(); var supplierId = supplier.RootElement.GetProperty("id").GetGuid();
        var productId = await CreateProductAsync(client, token, "FIN");
        using var inventory = await ReadJsonAsync(await AuthorizedGetAsync(client, $"/api/inventory/{productId}", token));
        Assert.Equal(HttpStatusCode.OK, (await AuthorizedJsonAsync(client, HttpMethod.Post, $"/api/inventory/{productId}/movements", token,
            new { type = "StockEntry", quantity = 10, reason = "Financeiro", rowVersion = inventory.RootElement.GetProperty("rowVersion").GetString() })).StatusCode);
        var key = Guid.NewGuid().ToString("N");
        var saleRequest = new { paymentMethod = "Deferred", customerId, discount = 0, financialTerms = new { financialCategoryId = income, firstDueDate = today, installments = 3 }, items = new[] { new { productId, quantity = 2 } } };
        using var sale = await ReadJsonAsync(await AuthorizedIdempotentJsonAsync(client, "/api/sales", token, key, saleRequest));
        var saleId = sale.RootElement.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.Created, (await AuthorizedIdempotentJsonAsync(client, "/api/sales", token, key, saleRequest)).StatusCode);
        await using var db = CreateTenantDb(TenantA);
        var receivables = await db.AccountsReceivable.AsNoTracking().Where(x => x.SaleId == saleId).OrderBy(x => x.InstallmentNumber).ToListAsync();
        Assert.Equal(3, receivables.Count); Assert.Equal(20m, receivables.Sum(x => x.OriginalAmount));
        Assert.False(await db.CashMovements.AnyAsync(x => x.SaleId == saleId));
        var accountPath = $"/api/financial/accounts-receivable/{receivables[0].Id}";
        using var payment = await ReadJsonAsync(await AuthorizedIdempotentJsonAsync(client, accountPath + "/payments", token, Guid.NewGuid().ToString("N"),
            new { amount = 1m, paymentDate = today, paymentMethod = "Pix", rowVersion = Convert.ToBase64String(receivables[0].RowVersion) }));
        var cancelSale = new { reason = "Cancelamento financeiro", rowVersion = sale.RootElement.GetProperty("rowVersion").GetString() };
        Assert.Equal(HttpStatusCode.Conflict, (await AuthorizedJsonAsync(client, HttpMethod.Post, $"/api/sales/{saleId}/cancel", token, cancelSale)).StatusCode);
        Assert.Equal(8m, await db.Inventories.Where(x => x.ProductId == productId).Select(x => x.Quantity).SingleAsync());
        using var payments = await ReadJsonAsync(await AuthorizedGetAsync(client, accountPath + "/payments", token)); var paid = payments.RootElement[0];
        Assert.Equal(HttpStatusCode.OK, (await AuthorizedJsonAsync(client, HttpMethod.Post, accountPath + $"/payments/{paid.GetProperty("id").GetGuid()}/reverse", token,
            new { reason = "Antes de cancelar venda", rowVersion = paid.GetProperty("rowVersion").GetString() })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await AuthorizedJsonAsync(client, HttpMethod.Post, $"/api/sales/{saleId}/cancel", token, cancelSale)).StatusCode);
        Assert.Equal(3, await db.AccountsReceivable.CountAsync(x => x.SaleId == saleId && x.Status == FinancialAccountStatus.Cancelled));
        Assert.Equal(10m, await db.Inventories.Where(x => x.ProductId == productId).Select(x => x.Quantity).SingleAsync());
        using var purchase = await ReadJsonAsync(await AuthorizedJsonAsync(client, HttpMethod.Post, "/api/purchases", token,
            new { supplierId, items = new[] { new { productId, quantity = 3, unitCost = 4.01m } } }));
        var purchaseId = purchase.RootElement.GetProperty("id").GetGuid();
        var receive = new { rowVersion = purchase.RootElement.GetProperty("rowVersion").GetString(), financialTerms = new { financialCategoryId = expense, firstDueDate = today, installments = 2 } };
        Assert.Equal(HttpStatusCode.OK, (await AuthorizedJsonAsync(client, HttpMethod.Post, $"/api/purchases/{purchaseId}/receive", token, receive)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await AuthorizedJsonAsync(client, HttpMethod.Post, $"/api/purchases/{purchaseId}/receive", token, receive)).StatusCode);
        var payables = await db.AccountsPayable.AsNoTracking().Where(x => x.PurchaseId == purchaseId).OrderBy(x => x.InstallmentNumber).ToListAsync();
        Assert.Equal(2, payables.Count); Assert.Equal(12.03m, payables.Sum(x => x.OriginalAmount)); Assert.Equal(6.02m, payables[0].OriginalAmount);
        Assert.All(payables, x => { Assert.Equal(supplierId, x.SupplierId); Assert.Equal(expense, x.FinancialCategoryId); });
        using var dashboard = await ReadJsonAsync(await AuthorizedGetAsync(client, $"/api/financial/dashboard?from={today:yyyy-MM-dd}&through={today:yyyy-MM-dd}", token));
        Assert.True(dashboard.RootElement.GetProperty("totalPayable").GetDecimal() >= 12.03m);
        await EnsureCashClosedAsync(client, token);
    }

    private static async Task<Guid> CreateFinancialCategoryAsync(HttpClient client, string token, string type)
    {
        using var response = await ReadJsonAsync(await AuthorizedJsonAsync(client, HttpMethod.Post, "/api/financial/categories", token,
            new { name = $"Financeiro {Guid.NewGuid():N}", type, isActive = true }));
        return response.RootElement.GetProperty("id").GetGuid();
    }

    [SqlFact]
    public async Task QuoteLifecyclePreservesSnapshotsAndConvertsAtomicallyThroughExistingSaleFlow()
    {
        using var client = Client(); var admin = await LoginAsync(client, TenantA, AdminEmail); var token = admin.AccessToken;
        var today = DateOnly.FromDateTime(DateTime.UtcNow); await EnsureCashClosedAsync(client, token);
        var productId = await CreateProductAsync(client, token, "QUOTE");
        using var customer = await ReadJsonAsync(await AuthorizedJsonAsync(client, HttpMethod.Post, "/api/customers", token,
            new { name = "Cliente Orçamento", isActive = true }));
        var customerId = customer.RootElement.GetProperty("id").GetGuid();
        var request = new { customerId, validUntil = today.AddDays(15), discount = 2m, notes = "Orçamento SQL", items = new[] { new { productId, quantity = 2m, discount = 1m } } };
        using var created = await ReadJsonAsync(await AuthorizedJsonAsync(client, HttpMethod.Post, "/api/quotes", token, request));
        var id = created.RootElement.GetProperty("id").GetGuid(); var path = $"/api/quotes/{id}";
        Assert.Equal("Draft", created.RootElement.GetProperty("status").GetString()); Assert.Equal(17m, created.RootElement.GetProperty("total").GetDecimal());
        await using var db = CreateTenantDb(TenantA);
        Assert.Equal(0m, await db.Inventories.Where(x => x.ProductId == productId).Select(x => x.Quantity).SingleAsync());
        Assert.False(await db.InventoryMovements.AnyAsync(x => x.ProductId == productId)); Assert.False(await db.Sales.AnyAsync(x => x.Items.Any(i => i.ProductId == productId)));
        Assert.False(await db.AccountsReceivable.AnyAsync(x => x.CustomerId == customerId));
        var viewer = await LoginAsync(client, TenantA, ViewerEmail);
        Assert.Equal(HttpStatusCode.Forbidden, (await AuthorizedGetAsync(client, "/api/quotes", viewer.AccessToken)).StatusCode);
        var other = await LoginAsync(client, TenantB, AdminEmail);
        Assert.Equal(HttpStatusCode.NotFound, (await AuthorizedGetAsync(client, path, other.AccessToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await AuthorizedGetAsync(client, path + "/pdf", other.AccessToken)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await AuthorizedJsonAsync(client, HttpMethod.Post, "/api/quotes", other.AccessToken, request)).StatusCode);
        var version = created.RootElement.GetProperty("rowVersion").GetString();
        using var edited = await ReadJsonAsync(await AuthorizedJsonAsync(client, HttpMethod.Put, path, token,
            new { customerId, validUntil = today.AddDays(15), discount = 2m, notes = "Editado", rowVersion = version, items = new[] { new { productId, quantity = 2m, discount = 1m } } }));
        using var sent = await ReadJsonAsync(await AuthorizedJsonAsync(client, HttpMethod.Post, path + "/send", token, new { rowVersion = edited.RootElement.GetProperty("rowVersion").GetString() }));
        Assert.Equal(HttpStatusCode.Conflict, (await AuthorizedJsonAsync(client, HttpMethod.Post, path + "/approve", token, new { rowVersion = version })).StatusCode);
        using var approved = await ReadJsonAsync(await AuthorizedJsonAsync(client, HttpMethod.Post, path + "/approve", token, new { rowVersion = sent.RootElement.GetProperty("rowVersion").GetString() }));
        version = approved.RootElement.GetProperty("rowVersion").GetString();
        Assert.Equal(HttpStatusCode.Conflict, (await AuthorizedJsonAsync(client, HttpMethod.Post, path + "/convert-to-sale", token, new { rowVersion = version, paymentMethod = "Pix" })).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await AuthorizedJsonAsync(client, HttpMethod.Post, "/api/cash/open", token, new { openingAmount = 0 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await AuthorizedJsonAsync(client, HttpMethod.Post, path + "/convert-to-sale", token, new { rowVersion = version, paymentMethod = "Pix" })).StatusCode);
        Assert.False(await db.Sales.AnyAsync(x => x.Items.Any(i => i.ProductId == productId)));
        Assert.Equal(QuoteStatus.Approved, await db.Quotes.Where(x => x.Id == id).Select(x => x.Status).SingleAsync());
        using var inventory = await ReadJsonAsync(await AuthorizedGetAsync(client, $"/api/inventory/{productId}", token));
        Assert.Equal(HttpStatusCode.OK, (await AuthorizedJsonAsync(client, HttpMethod.Post, $"/api/inventory/{productId}/movements", token,
            new { type = "StockEntry", quantity = 10m, reason = "Conversão", rowVersion = inventory.RootElement.GetProperty("rowVersion").GetString() })).StatusCode);
        var product = await db.Products.SingleAsync(x => x.Id == productId); product.SalePrice = 20; await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await AuthorizedJsonAsync(client, HttpMethod.Post, path + "/convert-to-sale", token,
            new { rowVersion = version, paymentMethod = "Deferred", financialTerms = new { financialCategoryId = Guid.NewGuid(), firstDueDate = today, installments = 2 } })).StatusCode);
        Assert.Equal(10m, await db.Inventories.AsNoTracking().Where(x => x.ProductId == productId).Select(x => x.Quantity).SingleAsync());
        Assert.False(await db.Sales.AnyAsync(x => x.Items.Any(i => i.ProductId == productId)));
        var category = await CreateFinancialCategoryAsync(client, token, "Income");
        var conversion = new { rowVersion = version, paymentMethod = "Deferred", financialTerms = new { financialCategoryId = category, firstDueDate = today, installments = 2 } };
        using var sale = await ReadJsonAsync(await AuthorizedJsonAsync(client, HttpMethod.Post, path + "/convert-to-sale", token, conversion));
        var saleId = sale.RootElement.GetProperty("id").GetGuid();
        Assert.Equal(17m, sale.RootElement.GetProperty("total").GetDecimal()); Assert.Equal(10m, sale.RootElement.GetProperty("items")[0].GetProperty("unitPrice").GetDecimal());
        Assert.Equal(HttpStatusCode.Conflict, (await AuthorizedJsonAsync(client, HttpMethod.Post, path + "/convert-to-sale", token, conversion)).StatusCode);
        Assert.Equal(8m, await db.Inventories.AsNoTracking().Where(x => x.ProductId == productId).Select(x => x.Quantity).SingleAsync());
        Assert.Equal(2, await db.AccountsReceivable.CountAsync(x => x.SaleId == saleId));
        Assert.Equal(17m, await db.AccountsReceivable.Where(x => x.SaleId == saleId).SumAsync(x => x.OriginalAmount));
        Assert.False(await db.CashMovements.AnyAsync(x => x.SaleId == saleId));
        using var final = await ReadJsonAsync(await AuthorizedGetAsync(client, path, token));
        Assert.Equal("Converted", final.RootElement.GetProperty("status").GetString()); Assert.Equal(saleId, final.RootElement.GetProperty("saleId").GetGuid());
        Assert.True(await db.AuditLogs.AnyAsync(x => x.EntityId == id.ToString() && x.Action == AuditAction.QuoteConverted));
        await EnsureCashClosedAsync(client, token);
    }

    [SqlFact]
    public async Task QuotesValidateReferencesStatusFiltersAndPdf()
    {
        using var client = Client(); var admin = await LoginAsync(client, TenantA, AdminEmail); var token = admin.AccessToken;
        var today = DateOnly.FromDateTime(DateTime.UtcNow); var productId = await CreateProductAsync(client, token, "QPDF");
        using var customer = await ReadJsonAsync(await AuthorizedJsonAsync(client, HttpMethod.Post, "/api/customers", token, new { name = "Cliente PDF", isActive = true }));
        var customerId = customer.RootElement.GetProperty("id").GetGuid();
        using var quote = await ReadJsonAsync(await AuthorizedJsonAsync(client, HttpMethod.Post, "/api/quotes", token,
            new { customerId, validUntil = today, discount = 0, items = new[] { new { productId, quantity = 1 } } }));
        var id = quote.RootElement.GetProperty("id").GetGuid(); var path = $"/api/quotes/{id}";
        var version = quote.RootElement.GetProperty("rowVersion").GetString();
        Assert.Equal(HttpStatusCode.Conflict, (await AuthorizedJsonAsync(client, HttpMethod.Post, path + "/approve", token, new { rowVersion = version })).StatusCode);
        using var sent = await ReadJsonAsync(await AuthorizedJsonAsync(client, HttpMethod.Post, path + "/send", token, new { rowVersion = version }));
        using var rejected = await ReadJsonAsync(await AuthorizedJsonAsync(client, HttpMethod.Post, path + "/reject", token,
            new { rowVersion = sent.RootElement.GetProperty("rowVersion").GetString(), reason = "Cliente rejeitou" }));
        Assert.Equal("Rejected", rejected.RootElement.GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await AuthorizedJsonAsync(client, HttpMethod.Post, path + "/convert-to-sale", token,
            new { rowVersion = rejected.RootElement.GetProperty("rowVersion").GetString(), paymentMethod = "Pix" })).StatusCode);
        using var list = await ReadJsonAsync(await AuthorizedGetAsync(client, $"/api/quotes?customerId={customerId}&status=Rejected&from={today:yyyy-MM-dd}&through={today:yyyy-MM-dd}", token));
        Assert.Equal(1, list.RootElement.GetProperty("total").GetInt32());
        using var pdf = await AuthorizedGetAsync(client, path + "/pdf", token); Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType?.MediaType);
        var text = System.Text.Encoding.Latin1.GetString(await pdf.Content.ReadAsByteArrayAsync());
        Assert.StartsWith("%PDF-1.4", text); Assert.Contains("Cliente PDF", text); Assert.Contains("ORC-", text); Assert.Contains("Empresa A CI", text);
        await using var db = CreateTenantDb(TenantA); var product = await db.Products.SingleAsync(x => x.Id == productId); product.IsActive = false; await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await AuthorizedJsonAsync(client, HttpMethod.Post, "/api/quotes", token,
            new { customerId, validUntil = today, discount = 0, items = new[] { new { productId, quantity = 1 } } })).StatusCode);
        using var summary = await ReadJsonAsync(await AuthorizedGetAsync(client, "/api/quotes/summary", token));
        Assert.True(summary.RootElement.GetProperty("count").GetInt32() > 0);
    }

    [SqlFact]
    public async Task StocktakeLifecycleIsAtomicAndConcurrentCompletionCannotDuplicateAdjustments()
    {
        using var client=Client();var admin=await LoginAsync(client,TenantA,AdminEmail);var token=admin.AccessToken;
        var a=await CreateProductAsync(client,token,"COUNT-A");var b=await CreateProductAsync(client,token,"COUNT-B");
        await StockEntryAsync(client,token,a,10);await StockEntryAsync(client,token,b,5);
        using var created=await ReadJsonAsync(await AuthorizedJsonAsync(client,HttpMethod.Post,"/api/inventories",token,new { productIds=new[]{a,b},notes="Contagem SQL" }));
        var id=created.RootElement.GetProperty("id").GetGuid();var path=$"/api/inventories/{id}";var version=created.RootElement.GetProperty("rowVersion").GetString();
        await using var db=CreateTenantDb(TenantA);
        var before=await db.InventoryMovements.CountAsync(x=>x.ProductId==a||x.ProductId==b);
        var beforeSales=await db.Sales.CountAsync();var beforeReceivables=await db.AccountsReceivable.CountAsync();
        Assert.Equal(2,before);
        using var started=await ReadJsonAsync(await AuthorizedJsonAsync(client,HttpMethod.Post,path+"/start",token,new { rowVersion=version }));
        version=started.RootElement.GetProperty("rowVersion").GetString();
        Assert.Equal(HttpStatusCode.Conflict,(await AuthorizedJsonAsync(client,HttpMethod.Post,path+"/complete",token,new { rowVersion=version })).StatusCode);
        using var counted=await ReadJsonAsync(await AuthorizedJsonAsync(client,HttpMethod.Post,path+"/count",token,new { rowVersion=version,items=new[]{new{productId=a,quantity=8,notes="Falta"},new{productId=b,quantity=8,notes="Sobra"}} }));
        version=counted.RootElement.GetProperty("rowVersion").GetString();
        Assert.Equal(before,await db.InventoryMovements.CountAsync(x=>x.ProductId==a||x.ProductId==b));
        Assert.Equal(HttpStatusCode.Conflict,(await AuthorizedJsonAsync(client,HttpMethod.Post,path+"/start",token,new { rowVersion=version })).StatusCode);
        // A movement after counting blocks the whole finalization; product A is not partially adjusted.
        await StockEntryAsync(client,token,b,1);
        Assert.Equal(HttpStatusCode.Conflict,(await AuthorizedJsonAsync(client,HttpMethod.Post,path+"/complete",token,new { rowVersion=version })).StatusCode);
        Assert.Equal(10,await db.Inventories.Where(x=>x.ProductId==a).Select(x=>x.Quantity).SingleAsync());
        Assert.Equal(6,await db.Inventories.Where(x=>x.ProductId==b).Select(x=>x.Quantity).SingleAsync());
        Assert.Equal(StocktakeStatus.Counting,await db.Stocktakes.Where(x=>x.Id==id).Select(x=>x.Status).SingleAsync());
        using var recounted=await ReadJsonAsync(await AuthorizedJsonAsync(client,HttpMethod.Post,path+"/count",token,new { rowVersion=version,items=new[]{new{productId=b,quantity=8,notes="Recontado após entrada"}} }));
        version=recounted.RootElement.GetProperty("rowVersion").GetString();
        var results=await Task.WhenAll(AuthorizedJsonAsync(client,HttpMethod.Post,path+"/complete",token,new {rowVersion=version}),AuthorizedJsonAsync(client,HttpMethod.Post,path+"/complete",token,new {rowVersion=version}));
        Assert.Single(results,x=>x.StatusCode==HttpStatusCode.OK);Assert.Single(results,x=>x.StatusCode==HttpStatusCode.Conflict);
        Assert.Equal(8,await db.Inventories.Where(x=>x.ProductId==a).Select(x=>x.Quantity).SingleAsync());
        Assert.Equal(8,await db.Inventories.Where(x=>x.ProductId==b).Select(x=>x.Quantity).SingleAsync());
        var adjustments=await db.InventoryMovements.Where(x=>x.ReferenceType=="Stocktake"&&x.ReferenceId==id.ToString()).ToListAsync();
        Assert.Equal(2,adjustments.Count);Assert.All(adjustments,x=>Assert.Equal("Inventory",x.ReasonCode));
        Assert.Equal(beforeSales,await db.Sales.CountAsync());Assert.Equal(beforeReceivables,await db.AccountsReceivable.CountAsync());
        Assert.Equal(HttpStatusCode.Conflict,(await AuthorizedJsonAsync(client,HttpMethod.Put,path,token,new{productIds=new[]{a},notes="Imutável",rowVersion=version})).StatusCode);
        Assert.True(await db.AuditLogs.AnyAsync(x=>x.EntityId==id.ToString()&&x.Action==AuditAction.StocktakeCompleted));
        using var history=await ReadJsonAsync(await AuthorizedGetAsync(client,$"/api/stock/movements?productId={a}&origin=Stocktake",token));
        Assert.Equal(1,history.RootElement.GetProperty("total").GetInt32());Assert.StartsWith("INV-",history.RootElement.GetProperty("items")[0].GetProperty("referenceNumber").GetString());
    }

    [SqlFact]
    public async Task StocktakesValidateTenantPermissionsReferencesVersionsAndCancellation()
    {
        using var client=Client();var admin=await LoginAsync(client,TenantA,AdminEmail);var token=admin.AccessToken;
        var product=await CreateProductAsync(client,token,"COUNT-SEC");
        using var created=await ReadJsonAsync(await AuthorizedJsonAsync(client,HttpMethod.Post,"/api/inventories",token,new{productIds=new[]{product},notes="Isolado"}));
        var id=created.RootElement.GetProperty("id").GetGuid();var version=created.RootElement.GetProperty("rowVersion").GetString();var path=$"/api/inventories/{id}";
        var other=await LoginAsync(client,TenantB,AdminEmail);
        Assert.Equal(HttpStatusCode.NotFound,(await AuthorizedGetAsync(client,path,other.AccessToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await AuthorizedGetAsync(client,$"/api/inventory/{product}",other.AccessToken)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await AuthorizedJsonAsync(client,HttpMethod.Post,"/api/inventories",other.AccessToken,new{productIds=new[]{product}})).StatusCode);
        using var foreignReport=await ReadJsonAsync(await AuthorizedGetAsync(client,$"/api/stock/movements?productId={product}",other.AccessToken));Assert.Equal(0,foreignReport.RootElement.GetProperty("total").GetInt32());
        var viewer=await LoginAsync(client,TenantA,ViewerEmail);
        Assert.Equal(HttpStatusCode.Forbidden,(await AuthorizedGetAsync(client,path,viewer.AccessToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await AuthorizedJsonAsync(client,HttpMethod.Post,"/api/stock/adjustments",viewer.AccessToken,new{})).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await AuthorizedGetAsync(client,"/api/stock/reports/low-stock",viewer.AccessToken)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await AuthorizedJsonAsync(client,HttpMethod.Put,path,token,new{productIds=new[]{product},rowVersion="invalid"})).StatusCode);
        using var edited=await ReadJsonAsync(await AuthorizedJsonAsync(client,HttpMethod.Put,path,token,new{productIds=new[]{product},notes="Editado",rowVersion=version}));
        Assert.Equal(HttpStatusCode.Conflict,(await AuthorizedJsonAsync(client,HttpMethod.Post,path+"/start",token,new{rowVersion=version})).StatusCode);
        version=edited.RootElement.GetProperty("rowVersion").GetString();
        Assert.Equal(HttpStatusCode.BadRequest,(await AuthorizedJsonAsync(client,HttpMethod.Post,path+"/cancel",token,new{rowVersion=version})).StatusCode);
        using var cancelled=await ReadJsonAsync(await AuthorizedJsonAsync(client,HttpMethod.Post,path+"/cancel",token,new{rowVersion=version,reason="Planejamento revisto"}));
        Assert.Equal("Cancelled",cancelled.RootElement.GetProperty("status").GetString());
        using var list=await ReadJsonAsync(await AuthorizedGetAsync(client,$"/api/inventories?search={created.RootElement.GetProperty("number").GetString()}&status=Cancelled",token));Assert.Equal(1,list.RootElement.GetProperty("total").GetInt32());
    }

    [SqlFact]
    public async Task ManualAdjustmentsReportsAndIndicatorsUseCurrentStockWithoutInventingFinancialCosts()
    {
        using var client=Client();var admin=await LoginAsync(client,TenantA,AdminEmail);var token=admin.AccessToken;
        var product=await CreateProductAsync(client,token,"COUNT-REPORT");await using var db=CreateTenantDb(TenantA);
        var p=await db.Products.SingleAsync(x=>x.Id==product);p.CreatedAt=DateTimeOffset.UtcNow.AddDays(-100);await db.SaveChangesAsync();
        using var initial=await ReadJsonAsync(await AuthorizedGetAsync(client,$"/api/inventory/{product}",token));
        var version=initial.RootElement.GetProperty("rowVersion").GetString();
        using var unused=await ReadJsonAsync(await AuthorizedGetAsync(client,"/api/stock/reports/no-movement?days=30",token));Assert.Contains(unused.RootElement.EnumerateArray(),x=>x.GetProperty("productId").GetGuid()==product);
        using var adjusted=await ReadJsonAsync(await AuthorizedJsonAsync(client,HttpMethod.Post,"/api/stock/adjustments",token,new{productId=product,type="PositiveAdjustment",quantity=1,reason="Correction",observation="Saldo físico",rowVersion=version}));
        Assert.Equal(1,adjusted.RootElement.GetProperty("inventory").GetProperty("quantity").GetDecimal());
        Assert.Equal("Correction",adjusted.RootElement.GetProperty("movement").GetProperty("reasonCode").GetString());
        Assert.Equal(HttpStatusCode.Conflict,(await AuthorizedJsonAsync(client,HttpMethod.Post,"/api/stock/adjustments",token,new{productId=product,type="PositiveAdjustment",quantity=1,reason="Correction",rowVersion=version})).StatusCode);
        version=adjusted.RootElement.GetProperty("inventory").GetProperty("rowVersion").GetString();
        Assert.Equal(HttpStatusCode.BadRequest,(await AuthorizedJsonAsync(client,HttpMethod.Post,"/api/stock/adjustments",token,new{productId=product,type="NegativeAdjustment",quantity=2,reason="Loss",rowVersion=version})).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await AuthorizedJsonAsync(client,HttpMethod.Post,"/api/stock/adjustments",token,new{productId=product,type="PositiveAdjustment",quantity=.0001m,reason="Correction",rowVersion=version})).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await AuthorizedJsonAsync(client,HttpMethod.Post,"/api/stock/adjustments",token,new{productId=product,type="PositiveAdjustment",quantity=1,reason="Inventory",rowVersion=version})).StatusCode);
        using var low=await ReadJsonAsync(await AuthorizedGetAsync(client,"/api/stock/reports/low-stock",token));
        var row=low.RootElement.EnumerateArray().Single(x=>x.GetProperty("productId").GetGuid()==product);
        Assert.Equal(1,row.GetProperty("suggestedReplacement").GetDecimal());Assert.Equal(4,row.GetProperty("estimatedValue").GetDecimal());
        using var used=await ReadJsonAsync(await AuthorizedGetAsync(client,"/api/stock/reports/no-movement?days=30",token));Assert.DoesNotContain(used.RootElement.EnumerateArray(),x=>x.GetProperty("productId").GetGuid()==product);
        Assert.Equal(HttpStatusCode.BadRequest,(await AuthorizedGetAsync(client,"/api/stock/reports/no-movement?days=12",token)).StatusCode);
        using var overview=await ReadJsonAsync(await AuthorizedGetAsync(client,"/api/stock/overview",token));Assert.True(overview.RootElement.GetProperty("lowStock").GetInt32()>0);
        using var exit=await ReadJsonAsync(await AuthorizedJsonAsync(client,HttpMethod.Post,"/api/stock/adjustments",token,new{productId=product,type="NegativeAdjustment",quantity=1,reason="Damage",rowVersion=version}));
        Assert.Equal(0,exit.RootElement.GetProperty("inventory").GetProperty("quantity").GetDecimal());
    }

    private static async Task StockEntryAsync(HttpClient client,string token,Guid product,decimal quantity)
    {
        using var stock=await ReadJsonAsync(await AuthorizedGetAsync(client,$"/api/inventory/{product}",token));
        var response=await AuthorizedJsonAsync(client,HttpMethod.Post,$"/api/inventory/{product}/movements",token,new{type="StockEntry",quantity,reason="Teste de inventário",rowVersion=stock.RootElement.GetProperty("rowVersion").GetString()});
        Assert.Equal(HttpStatusCode.OK,response.StatusCode);
    }

    [SqlFact]
    public async Task AdvancedInventoryMigrationUpgradesExistingAdministratorAndStockSchema()
    {
        var connection=new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(RequiredEnvironment("TenantDatabases__EmpresaA_CI")) { InitialCatalog="Forjix_Inventory_Upgrade_"+Guid.NewGuid().ToString("N") };
        await using var db=new TenantDbContext(new DbContextOptionsBuilder<TenantDbContext>().UseSqlServer(connection.ConnectionString).Options);
        await db.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>().MigrateAsync("20260926144839_AddQuotesV1");
        var roleId=Guid.NewGuid();
        db.Roles.Add(new Forjix.Domain.Entities.Identity.Role{Id=roleId,Name="Administrador",NormalizedName="ADMINISTRADOR",IsSystem=true,CreatedAt=DateTimeOffset.UtcNow,UpdatedAt=DateTimeOffset.UtcNow});
        await db.SaveChangesAsync();await db.Database.MigrateAsync();
        Assert.Equal(7,await db.RolePermissions.CountAsync(x=>x.RoleId==roleId));
        Assert.All(await db.RolePermissions.Where(x=>x.RoleId==roleId).ToListAsync(),x=>Assert.True(x.GrantedAt.Year>=2026));
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.Equal(0,await db.Stocktakes.CountAsync());
    }

    [SqlFact]
    public async Task StockManageAloneCannotBypassManualAdjustmentPermissionThroughLegacyEndpoint()
    {
        using var client=Client();var admin=await LoginAsync(client,TenantA,AdminEmail);var token=admin.AccessToken;
        var product=await CreateProductAsync(client,token,"COUNT-PERM");
        using var role=await ReadJsonAsync(await AuthorizedJsonAsync(client,HttpMethod.Post,"/api/roles",token,new{name="Estoque limitado "+Guid.NewGuid().ToString("N"),description="Sem ajuste",permissions=StockOnlyPermissions}));
        var email="stock-"+Guid.NewGuid().ToString("N")+"@teste.local";
        var user=await AuthorizedJsonAsync(client,HttpMethod.Post,"/api/users",token,new{name="Operador sem ajuste",email,password=Password(),isActive=true,roleIds=new[]{role.RootElement.GetProperty("id").GetGuid()}});
        Assert.Equal(HttpStatusCode.Created,user.StatusCode);
        var limited=await LoginAsync(client,TenantA,email);
        using var stock=await ReadJsonAsync(await AuthorizedGetAsync(client,$"/api/inventory/{product}",limited.AccessToken));
        var version=stock.RootElement.GetProperty("rowVersion").GetString();
        Assert.Equal(HttpStatusCode.Forbidden,(await AuthorizedJsonAsync(client,HttpMethod.Post,$"/api/inventory/{product}/movements",limited.AccessToken,new{type="PositiveAdjustment",quantity=1,reason="Correção",rowVersion=version})).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await AuthorizedJsonAsync(client,HttpMethod.Post,"/api/stock/adjustments",limited.AccessToken,new{productId=product,type="PositiveAdjustment",quantity=1,reason="Correction",rowVersion=version})).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await AuthorizedJsonAsync(client,HttpMethod.Post,$"/api/inventory/{product}/movements",token,new{type="Sale",quantity=1,rowVersion=version})).StatusCode);
        await using var db=CreateTenantDb(TenantA);
        var p=await db.Products.SingleAsync(x=>x.Id==product);
        Assert.Equal(HttpStatusCode.BadRequest,(await AuthorizedJsonAsync(client,HttpMethod.Post,"/api/products",token,new{categoryId=p.CategoryId,name="Min inválido",sku="BAD-"+Guid.NewGuid().ToString("N"),salePrice=10,costPrice=4,minimumStock=.0001m,isActive=true})).StatusCode);
    }

    private static string Password() =>
        Environment.GetEnvironmentVariable("FORJIX_CI_ADMIN_PASSWORD")
        ?? throw new InvalidOperationException("FORJIX_CI_ADMIN_PASSWORD is required for SQL integration tests.");

    private static async Task<Guid> CreateProductAsync(HttpClient client, string token, string prefix)
    {
        var categoryResponse = await AuthorizedJsonAsync(client, HttpMethod.Post, "/api/categories", token,
            new { name = $"{prefix} {Guid.NewGuid():N}", description = "Estoque", isActive = true });
        using var category = await ReadJsonAsync(categoryResponse);
        var productResponse = await AuthorizedJsonAsync(client, HttpMethod.Post, "/api/products", token,
            new { categoryId = category.RootElement.GetProperty("id").GetGuid(), name = $"Produto {prefix}", sku = $"{prefix}-{Guid.NewGuid():N}", barcode = (string?)null, salePrice = 10, costPrice = 4, minimumStock = 2, isActive = true, rowVersion = (string?)null });
        using var product = await ReadJsonAsync(productResponse);
        return product.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task EnsureCashClosedAsync(HttpClient client, string token)
    {
        using var current = await AuthorizedGetAsync(client, "/api/cash/current", token); Assert.True(current.IsSuccessStatusCode);
        var content = await current.Content.ReadAsStringAsync(); if (string.IsNullOrWhiteSpace(content)) return;
        using var response = JsonDocument.Parse(content); if (response.RootElement.ValueKind == JsonValueKind.Null) return;
        var version = response.RootElement.GetProperty("rowVersion").GetString(); var expected = response.RootElement.GetProperty("expectedAmount").GetDecimal();
        var closed = await AuthorizedJsonAsync(client, HttpMethod.Post, "/api/cash/close", token, new { closingAmount = Math.Max(0, expected), rowVersion = version }); Assert.Equal(HttpStatusCode.OK, closed.StatusCode);
    }

    private HttpClient Client() => factory.CreateClient(new()
    {
        BaseAddress = new Uri("https://localhost"),
        HandleCookies = false
    });

    private static ForjixMasterDbContext CreateMasterDb() => new(
        new DbContextOptionsBuilder<ForjixMasterDbContext>()
            .UseSqlServer(RequiredEnvironment("ConnectionStrings__ForjixMaster")).Options);

    private static TenantDbContext CreateTenantDb(string slug) => new(
        new DbContextOptionsBuilder<TenantDbContext>()
            .UseSqlServer(RequiredEnvironment(slug == TenantA
                ? "TenantDatabases__EmpresaA_CI"
                : "TenantDatabases__EmpresaB_CI")).Options);

    private static string RequiredEnvironment(string name) =>
        Environment.GetEnvironmentVariable(name)
        ?? throw new InvalidOperationException($"Environment variable '{name}' is required.");

    private static async Task AssertTenantSeedCountsAsync(string slug)
    {
        await using var db = CreateTenantDb(slug);
        Assert.Equal(1, await db.Users.CountAsync(x => x.NormalizedEmail == "ADMIN@TESTE.LOCAL"));
        Assert.Equal(1, await db.Users.CountAsync(x => x.NormalizedEmail == "VIEWER@TESTE.LOCAL"));
        var adminRole = await db.Roles.SingleAsync(x => x.NormalizedName == "ADMINISTRADOR");
        Assert.Equal(Permissions.All.Count + 1, await db.Permissions.CountAsync());
        Assert.Equal(Permissions.All.Count + 1, await db.RolePermissions.CountAsync(x => x.RoleId == adminRole.Id));
        Assert.Equal(1, await db.UserRoles.CountAsync(x => x.RoleId == adminRole.Id && x.User.NormalizedEmail == "ADMIN@TESTE.LOCAL"));
    }

    private static async Task<ApiSession> LoginAsync(HttpClient client, string tenantSlug, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new { tenantSlug, email, password = Password() });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return new ApiSession(await AccessTokenAsync(response), RefreshCookie(response));
    }

    private static async Task<ApiSession> RefreshAsync(HttpClient client, string cookie)
    {
        var response = await RefreshResponseAsync(client, cookie);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return new ApiSession(await AccessTokenAsync(response), RefreshCookie(response));
    }

    private static async Task<HttpResponseMessage> RefreshResponseAsync(HttpClient client, string cookie)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh")
        {
            Content = JsonContent.Create(new { })
        };
        request.Headers.Add("Cookie", cookie);
        return await client.SendAsync(request);
    }

    private static async Task<JsonDocument> GetMeAsync(HttpClient client, string token)
    {
        using var response = await AuthorizedGetAsync(client, "/api/me", token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    private static Task<HttpResponseMessage> AuthorizedGetAsync(HttpClient client, string path, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client.SendAsync(request);
    }

    private static Task<HttpResponseMessage> AuthorizedJsonAsync(HttpClient client, HttpMethod method, string path, string token, object body)
    {
        var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client.SendAsync(request);
    }

    private static Task<HttpResponseMessage> AuthorizedIdempotentJsonAsync(HttpClient client, string path, string token, string key, object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("Idempotency-Key", key);
        return client.SendAsync(request);
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    private static async Task<string> AccessTokenAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("accessToken").GetString()!;
    }

    private static string RefreshCookie(HttpResponseMessage response) => response.Headers
        .GetValues("Set-Cookie")
        .Single(value => value.Contains("forjix-refresh", StringComparison.OrdinalIgnoreCase))
        .Split(';', 2)[0];

    private static string[] PermissionCodes(JsonDocument context) => context.RootElement
        .GetProperty("permissions").EnumerateArray().Select(x => x.GetString()!).ToArray();

    private sealed record ApiSession(string AccessToken, string Cookie);
}

public sealed class SqlFactAttribute : FactAttribute
{
    public SqlFactAttribute()
    {
        if (!SqlTestsEnabled())
        {
            Skip = "Set FORJIX_RUN_SQL_TESTS=true after provisioning the isolated CI databases.";
        }
    }

    private static bool SqlTestsEnabled() => string.Equals(
        Environment.GetEnvironmentVariable("FORJIX_RUN_SQL_TESTS"), "true", StringComparison.OrdinalIgnoreCase);
}

public sealed class SqlTheoryAttribute : TheoryAttribute
{
    public SqlTheoryAttribute()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("FORJIX_RUN_SQL_TESTS"), "true", StringComparison.OrdinalIgnoreCase))
        {
            Skip = "Set FORJIX_RUN_SQL_TESTS=true after provisioning the isolated CI databases.";
        }
    }
}
