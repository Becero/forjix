using System.Net;
using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Forjix.Application.Common;
using Forjix.Domain.Enums;
using Forjix.Infrastructure.Persistence.Master;
using Forjix.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;

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
        Assert.DoesNotContain(productsB.RootElement.EnumerateArray(), x => x.GetProperty("sku").GetString() == sku);
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
    public async Task CashSupportsOpenSupplyWithdrawalAndClose()
    {
        using var client=Client();var admin=await LoginAsync(client,TenantA,AdminEmail);await EnsureCashClosedAsync(client,admin.AccessToken);
        var openedResponse=await AuthorizedJsonAsync(client,HttpMethod.Post,"/api/cash/open",admin.AccessToken,new{openingAmount=100});Assert.Equal(HttpStatusCode.Created,openedResponse.StatusCode);using var opened=await ReadJsonAsync(openedResponse);var version=opened.RootElement.GetProperty("rowVersion").GetString();
        Assert.Equal(HttpStatusCode.Conflict,(await AuthorizedJsonAsync(client,HttpMethod.Post,"/api/cash/open",admin.AccessToken,new{openingAmount=0})).StatusCode);
        var supplyResponse=await AuthorizedJsonAsync(client,HttpMethod.Post,"/api/cash/supply",admin.AccessToken,new{amount=25,reason="Troco",rowVersion=version});using var supplied=await ReadJsonAsync(supplyResponse);version=supplied.RootElement.GetProperty("rowVersion").GetString();
        var withdrawalResponse=await AuthorizedJsonAsync(client,HttpMethod.Post,"/api/cash/withdraw",admin.AccessToken,new{amount=10,reason="Despesa",rowVersion=version});using var withdrawn=await ReadJsonAsync(withdrawalResponse);version=withdrawn.RootElement.GetProperty("rowVersion").GetString();Assert.Equal(115,withdrawn.RootElement.GetProperty("expectedAmount").GetDecimal());
        Assert.Equal(HttpStatusCode.OK,(await AuthorizedJsonAsync(client,HttpMethod.Post,"/api/cash/close",admin.AccessToken,new{closingAmount=115,rowVersion=version})).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await AuthorizedJsonAsync(client,HttpMethod.Post,"/api/cash/supply",admin.AccessToken,new{amount=1,reason="Fechado",rowVersion=version})).StatusCode);
    }

    [SqlFact]
    public async Task DashboardReportsAndExportsUseTenantData()
    {
        using var client=Client();var admin=await LoginAsync(client,TenantA,AdminEmail);await EnsureCashClosedAsync(client,admin.AccessToken);Assert.Equal(HttpStatusCode.Created,(await AuthorizedJsonAsync(client,HttpMethod.Post,"/api/cash/open",admin.AccessToken,new{openingAmount=0})).StatusCode);
        var from=Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(-1).ToString("O",CultureInfo.InvariantCulture));var through=Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(1).ToString("O",CultureInfo.InvariantCulture));using var before=await ReadJsonAsync(await AuthorizedGetAsync(client,$"/api/reports?from={from}&through={through}",admin.AccessToken));var revenue=before.RootElement.GetProperty("revenue").GetDecimal();var count=before.RootElement.GetProperty("saleCount").GetInt32();
        var productId=await CreateProductAsync(client,admin.AccessToken,"REP");using var stock=await ReadJsonAsync(await AuthorizedGetAsync(client,$"/api/inventory/{productId}",admin.AccessToken));Assert.Equal(HttpStatusCode.OK,(await AuthorizedJsonAsync(client,HttpMethod.Post,$"/api/inventory/{productId}/movements",admin.AccessToken,new{type="StockEntry",quantity=5,reason="Relatório",rowVersion=stock.RootElement.GetProperty("rowVersion").GetString()})).StatusCode);
        var sale=await AuthorizedIdempotentJsonAsync(client,"/api/sales",admin.AccessToken,Guid.NewGuid().ToString("N"),new{paymentMethod="Cash",discount=0,customerId=(Guid?)null,items=new[]{new{productId,quantity=2}}});Assert.Equal(HttpStatusCode.Created,sale.StatusCode);
        using var after=await ReadJsonAsync(await AuthorizedGetAsync(client,$"/api/reports?from={from}&through={through}",admin.AccessToken));Assert.Equal(revenue+20,after.RootElement.GetProperty("revenue").GetDecimal());Assert.Equal(count+1,after.RootElement.GetProperty("saleCount").GetInt32());using var dashboard=await ReadJsonAsync(await AuthorizedGetAsync(client,"/api/dashboard",admin.AccessToken));Assert.True(dashboard.RootElement.GetProperty("saleCountToday").GetInt32()>0);
        using var excel=await AuthorizedGetAsync(client,$"/api/reports/export?from={from}&through={through}&format=excel",admin.AccessToken);Assert.Equal(HttpStatusCode.OK,excel.StatusCode);Assert.Contains("ms-excel",excel.Content.Headers.ContentType?.MediaType,StringComparison.OrdinalIgnoreCase);
        using var pdf=await AuthorizedGetAsync(client,$"/api/reports/export?from={from}&through={through}&format=pdf",admin.AccessToken);Assert.Equal("%PDF",(await pdf.Content.ReadAsStringAsync())[..4]);await EnsureCashClosedAsync(client,admin.AccessToken);
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
        using var client=Client();var admin=await LoginAsync(client,TenantA,AdminEmail);var doc=Random.Shared.NextInt64(10_000_000_000,99_999_999_999).ToString(CultureInfo.InvariantCulture);
        var supplierResponse=await AuthorizedJsonAsync(client,HttpMethod.Post,"/api/suppliers",admin.AccessToken,new{name="Fornecedor SQL",document=doc,email="fornecedor@teste.local",phone="11999999999",contactName="Contato",notes="Teste",isActive=true});
        Assert.Equal(HttpStatusCode.Created,supplierResponse.StatusCode);using var supplier=await ReadJsonAsync(supplierResponse);var supplierId=supplier.RootElement.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK,(await AuthorizedJsonAsync(client,HttpMethod.Put,$"/api/suppliers/{supplierId}",admin.AccessToken,new{name="Fornecedor Atualizado",document=doc,email="fornecedor@teste.local",phone="11999999999",contactName="Contato",notes="Atualizado",isActive=true})).StatusCode);
        var productId=await CreateProductAsync(client,admin.AccessToken,"PUR");var purchaseResponse=await AuthorizedJsonAsync(client,HttpMethod.Post,"/api/purchases",admin.AccessToken,new{supplierId,notes="Compra SQL",items=new[]{new{productId,quantity=50,unitCost=4}}});
        Assert.Equal(HttpStatusCode.Created,purchaseResponse.StatusCode);using var purchase=await ReadJsonAsync(purchaseResponse);var purchaseId=purchase.RootElement.GetProperty("id").GetGuid();var version=purchase.RootElement.GetProperty("rowVersion").GetString();
        Assert.Equal(HttpStatusCode.OK,(await AuthorizedJsonAsync(client,HttpMethod.Post,$"/api/purchases/{purchaseId}/receive",admin.AccessToken,new{rowVersion=version})).StatusCode);
        await using var db=CreateTenantDb(TenantA);Assert.Equal(50,await db.Inventories.Where(x=>x.ProductId==productId).Select(x=>x.Quantity).SingleAsync());Assert.Equal(1,await db.InventoryMovements.CountAsync(x=>x.ProductId==productId&&x.Type==InventoryMovementType.Purchase));
        Assert.Equal(HttpStatusCode.Conflict,(await AuthorizedJsonAsync(client,HttpMethod.Post,$"/api/purchases/{purchaseId}/cancel",admin.AccessToken,new{rowVersion=version})).StatusCode);
        var adminB=await LoginAsync(client,TenantB,AdminEmail);Assert.Equal(HttpStatusCode.NotFound,(await AuthorizedGetAsync(client,$"/api/purchases/{purchaseId}",adminB.AccessToken)).StatusCode);using var suppliersB=await ReadJsonAsync(await AuthorizedGetAsync(client,$"/api/suppliers?search={doc}",adminB.AccessToken));Assert.Equal(0,suppliersB.RootElement.GetProperty("total").GetInt32());
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

    private static async Task EnsureCashClosedAsync(HttpClient client,string token)
    {
        using var current=await AuthorizedGetAsync(client,"/api/cash/current",token);Assert.True(current.IsSuccessStatusCode);
        var content=await current.Content.ReadAsStringAsync();if(string.IsNullOrWhiteSpace(content))return;
        using var response=JsonDocument.Parse(content);if(response.RootElement.ValueKind==JsonValueKind.Null)return;
        var version=response.RootElement.GetProperty("rowVersion").GetString();var expected=response.RootElement.GetProperty("expectedAmount").GetDecimal();
        var closed=await AuthorizedJsonAsync(client,HttpMethod.Post,"/api/cash/close",token,new{closingAmount=Math.Max(0,expected),rowVersion=version});Assert.Equal(HttpStatusCode.OK,closed.StatusCode);
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
