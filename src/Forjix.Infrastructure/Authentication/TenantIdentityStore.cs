using Forjix.Application.Abstractions.Authentication;
using Forjix.Application.Abstractions.Tenancy;
using Forjix.Domain.Entities.Audit;
using Forjix.Domain.Entities.Identity;
using Forjix.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;

namespace Forjix.Infrastructure.Authentication;

internal sealed class TenantIdentityStoreFactory(ITenantDbContextFactory contextFactory) : ITenantIdentityStoreFactory
{
    public ITenantIdentityStore Create(ResolvedTenantDatabase tenantDatabase) =>
        new TenantIdentityStore(contextFactory.Create(tenantDatabase));
}
internal sealed class TenantIdentityStore(TenantDbContext context) : ITenantIdentityStore
{
    public Task<User?> FindUserByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        UserAccessQuery().SingleOrDefaultAsync(x => x.NormalizedEmail == normalizedEmail, cancellationToken);

    public Task<User?> FindUserByIdAsync(Guid userId, CancellationToken cancellationToken) =>
        UserAccessQuery().SingleOrDefaultAsync(x => x.Id == userId, cancellationToken);

    public Task<RefreshToken?> FindRefreshTokenByHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        context.RefreshTokens.SingleOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);

    public async Task AddLoginResultAsync(
        RefreshToken? refreshToken,
        AuditLog auditLog,
        CancellationToken cancellationToken)
    {
        if (refreshToken is not null)
        {
            context.RefreshTokens.Add(refreshToken);
        }

        context.AuditLogs.Add(auditLog);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> TryRotateRefreshTokenAsync(
        RefreshToken currentToken,
        RefreshToken replacement,
        AuditLog auditLog,
        CancellationToken cancellationToken)
    {
        currentToken.RevokedAt = auditLog.OccurredAt;
        currentToken.ReplacedByTokenId = replacement.Id;
        context.RefreshTokens.Add(replacement);
        context.AuditLogs.Add(auditLog);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
    }

    public async Task<bool> RevokeRefreshTokenAsync(
        RefreshToken refreshToken,
        AuditLog auditLog,
        CancellationToken cancellationToken)
    {
        if (refreshToken.RevokedAt is not null)
        {
            return false;
        }

        refreshToken.RevokedAt = auditLog.OccurredAt;
        context.AuditLogs.Add(auditLog);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
    }

    public async Task RevokeTokenFamilyAsync(
        Guid familyId,
        DateTimeOffset revokedAt,
        CancellationToken cancellationToken)
    {
        await context.RefreshTokens
            .Where(x => x.FamilyId == familyId && x.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.RevokedAt, revokedAt), cancellationToken);
    }

    public Task<bool> HasPermissionAsync(Guid userId, string permission, CancellationToken cancellationToken) =>
        context.Users.AnyAsync(
            user => user.Id == userId && user.IsActive &&
                    user.UserRoles.Any(userRole => userRole.Role.RolePermissions.Any(
                        rolePermission => rolePermission.Permission.Code == permission)),
            cancellationToken);

    public ValueTask DisposeAsync() => context.DisposeAsync();

    private IQueryable<User> UserAccessQuery() => context.Users
        .Include(x => x.UserRoles)
        .ThenInclude(x => x.Role)
        .ThenInclude(x => x.RolePermissions)
        .ThenInclude(x => x.Permission);
}
