using Forjix.Application.Abstractions.Authentication;
using Forjix.Application.Abstractions.Tenancy;
using Forjix.Application.Features.Authentication;
using Forjix.Domain.Entities.Audit;
using Forjix.Domain.Entities.Identity;

namespace Forjix.Application.Tests;

public sealed class AuthenticationServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 13, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task LoginWithUnknownTenantIsRejected() =>
        Assert.Null(await Create(tenantExists: false).Service.LoginAsync(Login(), "c"));

    [Fact]
    public async Task LoginWithInactiveTenantIsRejectedByResolver() =>
        Assert.Null(await Create(tenantExists: false).Service.LoginAsync(Login(), "c"));

    [Fact]
    public async Task LoginWithUnknownUserIsRejected()
    {
        var fixture = Create(userExists: false);
        Assert.Null(await fixture.Service.LoginAsync(Login(), "c"));
        Assert.Single(fixture.Store.Audits);
    }

    [Fact]
    public async Task LoginWithWrongPasswordIsRejected()
    {
        var fixture = Create(passwordValid: false);
        Assert.Null(await fixture.Service.LoginAsync(Login(), "c"));
    }

    [Fact]
    public async Task LoginWithInactiveUserIsRejected()
    {
        var fixture = Create(user: User(active: false));
        Assert.Null(await fixture.Service.LoginAsync(Login(), "c"));
    }

    [Fact]
    public async Task ValidLoginReturnsContextAndPersistsHashedRefreshToken()
    {
        var fixture = Create();
        var result = await fixture.Service.LoginAsync(Login(), "correlation");

        Assert.NotNull(result);
        Assert.Equal("jwt", result.AccessToken);
        Assert.Equal("Empresa Demo", result.Context.Tenant.Name);
        Assert.Contains("audit.view", result.Context.Permissions);
        Assert.NotEqual("refresh", fixture.Store.AddedToken!.TokenHash);
    }

    [Fact]
    public async Task ValidRefreshRotatesToken()
    {
        var token = Token();
        var fixture = Create(refreshToken: token);
        var result = await fixture.Service.RefreshAsync(new RefreshCommand(Tenant().TenantId, "refresh"), "c");

        Assert.NotNull(result);
        Assert.True(fixture.Store.RotationAttempted);
        Assert.NotEqual(token.Id, fixture.Store.AddedToken!.Id);
        Assert.Equal(token.FamilyId, fixture.Store.AddedToken.FamilyId);
    }

    [Fact]
    public async Task RevokedRefreshIsRejectedAndRevokesFamily()
    {
        var token = Token(); token.RevokedAt = Now.AddMinutes(-1);
        var fixture = Create(refreshToken: token);
        Assert.Null(await fixture.Service.RefreshAsync(new RefreshCommand(Tenant().TenantId, "refresh"), "c"));
        Assert.Equal(token.FamilyId, fixture.Store.RevokedFamily);
    }

    [Fact]
    public async Task ExpiredRefreshIsRejected()
    {
        var fixture = Create(refreshToken: Token(Now.AddSeconds(-1)));
        Assert.Null(await fixture.Service.RefreshAsync(new RefreshCommand(Tenant().TenantId, "refresh"), "c"));
        Assert.False(fixture.Store.RotationAttempted);
    }

    [Fact]
    public async Task SessionContextUsesOnlyRequestedTenantId()
    {
        var tenantA = Tenant();
        var user = User();
        var fixture = Create(resolvedTenant: tenantA, user: user);
        var result = await fixture.Service.GetSessionContextAsync(tenantA.TenantId, user.Id);
        Assert.Equal(tenantA.TenantId, result!.Tenant.Id);
        Assert.Equal([tenantA.TenantId], fixture.Resolver.RequestedTenantIds);
    }

    [Fact]
    public async Task TenantATokenCannotResolveTenantBContext()
    {
        var tenantA = Tenant();
        var fixture = Create(resolvedTenant: tenantA);

        var result = await fixture.Service.GetSessionContextAsync(Guid.NewGuid(), User().Id);

        Assert.Null(result);
    }

    private static LoginCommand Login() => new("empresa-demo", "admin@demo.com", "valid-password");
    private static ResolvedTenantDatabase Tenant() => new(
        Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), "Empresa Demo", "empresa-demo", "Forjix_EmpresaDemo",
        "Server=A;Database=Forjix_EmpresaDemo", "1", [], new Dictionary<string, string>());
    private static User User(bool active = true)
    {
        var permission = new Permission { Id = Guid.NewGuid(), Code = "audit.view", Name = "Audit", Module = "audit" };
        var role = new Role { Id = Guid.NewGuid(), Name = "Administrador", NormalizedName = "ADMINISTRADOR", CreatedAt = Now, UpdatedAt = Now };
        role.RolePermissions.Add(new RolePermission { Role = role, Permission = permission, RoleId = role.Id, PermissionId = permission.Id });
        var user = new User { Id = Guid.NewGuid(), Name = "Administrador", Email = "admin@demo.com", NormalizedEmail = "ADMIN@DEMO.COM", PasswordHash = "hash", IsActive = active, CreatedAt = Now, UpdatedAt = Now };
        user.UserRoles.Add(new UserRole { User = user, Role = role, UserId = user.Id, RoleId = role.Id });
        return user;
    }
    private static RefreshToken Token(DateTimeOffset? expires = null) => new()
    {
        Id = Guid.NewGuid(),
        UserId = User().Id,
        TokenHash = "hash:refresh",
        FamilyId = Guid.NewGuid(),
        CreatedAt = Now.AddDays(-1),
        ExpiresAt = expires ?? Now.AddDays(1)
    };

    private static Fixture Create(
        ResolvedTenantDatabase? resolvedTenant = default,
        User? user = default,
        bool passwordValid = true,
        RefreshToken? refreshToken = null,
        bool tenantExists = true,
        bool userExists = true)
    {
        resolvedTenant = tenantExists ? resolvedTenant ?? Tenant() : null;
        user = userExists ? user ?? User() : null;
        if (refreshToken is not null) refreshToken.UserId = user!.Id;
        var resolver = new FakeResolver(resolvedTenant);
        var store = new FakeStore(user, refreshToken);
        var service = new AuthenticationService(resolver, new FakeStoreFactory(store), new FakePasswords(passwordValid),
            new FakeAccessToken(), new FakeRefreshTokens(), new FixedTimeProvider(Now));
        return new Fixture(service, store, resolver);
    }

    private sealed record Fixture(AuthenticationService Service, FakeStore Store, FakeResolver Resolver);
    private sealed class FakeResolver(ResolvedTenantDatabase? tenant) : ITenantDatabaseResolver
    {
        public List<Guid> RequestedTenantIds { get; } = [];
        public Task<ResolvedTenantDatabase?> ResolveBySlugAsync(string slug, CancellationToken ct = default) => Task.FromResult(tenant);
        public Task<ResolvedTenantDatabase?> ResolveByTenantIdAsync(Guid id, CancellationToken ct = default) { RequestedTenantIds.Add(id); return Task.FromResult(tenant?.TenantId == id ? tenant : null); }
    }
    private sealed class FakeStoreFactory(FakeStore store) : ITenantIdentityStoreFactory { public ITenantIdentityStore Create(ResolvedTenantDatabase _) => store; }
    private sealed class FakeStore(User? user, RefreshToken? refresh) : ITenantIdentityStore
    {
        public List<AuditLog> Audits { get; } = [];
        public RefreshToken? AddedToken { get; private set; }
        public bool RotationAttempted { get; private set; }
        public Guid? RevokedFamily { get; private set; }
        public Task<User?> FindUserByNormalizedEmailAsync(string _, CancellationToken ct) => Task.FromResult(user);
        public Task<User?> FindUserByIdAsync(Guid id, CancellationToken ct) => Task.FromResult(user?.Id == id ? user : null);
        public Task<RefreshToken?> FindRefreshTokenByHashAsync(string hash, CancellationToken ct) => Task.FromResult(refresh);
        public Task AddLoginResultAsync(RefreshToken? token, AuditLog audit, CancellationToken ct) { AddedToken = token; Audits.Add(audit); return Task.CompletedTask; }
        public Task<bool> TryRotateRefreshTokenAsync(RefreshToken current, RefreshToken replacement, AuditLog audit, CancellationToken ct) { RotationAttempted = true; AddedToken = replacement; return Task.FromResult(true); }
        public Task<bool> RevokeRefreshTokenAsync(RefreshToken token, AuditLog audit, CancellationToken ct) => Task.FromResult(true);
        public Task RevokeTokenFamilyAsync(Guid familyId, DateTimeOffset at, CancellationToken ct) { RevokedFamily = familyId; return Task.CompletedTask; }
        public Task<bool> HasPermissionAsync(Guid userId, string permission, CancellationToken ct) => Task.FromResult(true);
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
    private sealed class FakePasswords(bool valid) : IPasswordHashService { public string HashPassword(User user, string password) => "hash"; public bool VerifyPassword(User user, string hash, string password) => valid; }
    private sealed class FakeAccessToken : IAccessTokenIssuer { public AccessTokenResult Issue(User user, Guid tenantId, string slug, IReadOnlyCollection<string> roles) => new("jwt", Now.AddMinutes(15)); }
    private sealed class FakeRefreshTokens : IRefreshTokenGenerator { public GeneratedRefreshToken Generate() => new("refresh", "hash:refresh"); public string Hash(string token) => "hash:" + token; }
    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }
}
