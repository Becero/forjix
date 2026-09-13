using System.Net;
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

    private static string Password() =>
        Environment.GetEnvironmentVariable("FORJIX_CI_ADMIN_PASSWORD")
        ?? throw new InvalidOperationException("FORJIX_CI_ADMIN_PASSWORD is required for SQL integration tests.");

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
