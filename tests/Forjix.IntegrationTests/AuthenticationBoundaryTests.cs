using System.Net;
using System.Security.Claims;
using Forjix.Application.Abstractions.Authorization;
using Forjix.Application.Abstractions.Tenancy;
using Forjix.Application.Common;
using Forjix.Infrastructure.Authorization;
using Forjix.Infrastructure.Persistence.Tenant;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Forjix.IntegrationTests;

public sealed class AuthenticationBoundaryTests(ForjixWebApplicationFactory factory)
    : IClassFixture<ForjixWebApplicationFactory>
{
    [Fact]
    public async Task ProtectedEndpointWithoutTokenReturnsUnauthorized()
    {
        var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        var response = await client.GetAsync("/api/access-proof/administration-users");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PermissionHandlerHonorsDatabaseBackedDecision(bool granted)
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ForjixClaimNames.TenantId, tenantId.ToString()),
            new Claim(ForjixClaimNames.UserId, userId.ToString())
        ], "test"));
        var requirement = new PermissionRequirement(Permissions.AdministrationUsersView);
        var context = new AuthorizationHandlerContext([requirement], principal, null);

        await new PermissionAuthorizationHandler(new FixedPermissionChecker(granted)).HandleAsync(context);

        Assert.Equal(granted, context.HasSucceeded);
    }

    [Fact]
    public async Task ConcurrentTenantContextsKeepDistinctConnectionStrings()
    {
        var factory = new TenantDbContextFactory();
        var tenantA = Tenant(Guid.NewGuid(), "Server=alpha;Database=Forjix_A;User Id=a;Password=p;TrustServerCertificate=True");
        var tenantB = Tenant(Guid.NewGuid(), "Server=beta;Database=Forjix_B;User Id=b;Password=p;TrustServerCertificate=True");

        var connections = await Task.WhenAll(
            Task.Run(() => { using var db = factory.Create(tenantA); return db.Database.GetConnectionString(); }),
            Task.Run(() => { using var db = factory.Create(tenantB); return db.Database.GetConnectionString(); }));

        Assert.Contains("Initial Catalog=Forjix_A", connections[0]);
        Assert.Contains("Initial Catalog=Forjix_B", connections[1]);
        Assert.DoesNotContain("Forjix_B", connections[0]);
        Assert.DoesNotContain("Forjix_A", connections[1]);
    }

    private static ResolvedTenantDatabase Tenant(Guid id, string connection) =>
        new(id, id.ToString(), id.ToString(), "db", connection, "1", [], new Dictionary<string, string>());

    private sealed class FixedPermissionChecker(bool granted) : IPermissionChecker
    {
        public Task<bool> HasPermissionAsync(Guid tenantId, Guid userId, string permission, CancellationToken cancellationToken = default) =>
            Task.FromResult(granted);
    }
}
