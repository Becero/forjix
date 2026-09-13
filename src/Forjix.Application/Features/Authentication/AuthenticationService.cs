using Forjix.Application.Abstractions.Authentication;
using Forjix.Application.Abstractions.Tenancy;
using Forjix.Application.Common;
using Forjix.Domain.Entities.Audit;
using Forjix.Domain.Entities.Identity;
using Forjix.Domain.Enums;

namespace Forjix.Application.Features.Authentication;

internal sealed class AuthenticationService(
    ITenantDatabaseResolver tenantDatabaseResolver,
    ITenantIdentityStoreFactory identityStoreFactory,
    IPasswordHashService passwordHashService,
    IAccessTokenIssuer accessTokenIssuer,
    IRefreshTokenGenerator refreshTokenGenerator,
    TimeProvider timeProvider) : IAuthenticationService
{
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(7);

    public async Task<AuthenticatedSession?> LoginAsync(
        LoginCommand command,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var tenant = await tenantDatabaseResolver.ResolveBySlugAsync(command.TenantSlug, cancellationToken);
        if (tenant is null)
        {
            return null;
        }

        await using var store = identityStoreFactory.Create(tenant);
        var normalizedEmail = command.Email.Trim().ToUpperInvariant();
        var user = await store.FindUserByNormalizedEmailAsync(normalizedEmail, cancellationToken);
        var now = timeProvider.GetUtcNow();

        if (user is null || !user.IsActive || user.LockedUntil > now ||
            !passwordHashService.VerifyPassword(user, user.PasswordHash, command.Password))
        {
            await store.AddLoginResultAsync(null, CreateAudit(user?.Id, AuditAction.LoginFailed, correlationId, now), cancellationToken);
            return null;
        }

        var session = CreateSession(tenant, user, now);
        var refreshToken = CreateRefreshToken(user.Id, session.RefreshToken, session.RefreshTokenExpiresAt, now);
        await store.AddLoginResultAsync(refreshToken, CreateAudit(user.Id, AuditAction.LoginSucceeded, correlationId, now), cancellationToken);
        return session;
    }

    public async Task<AuthenticatedSession?> RefreshAsync(
        RefreshCommand command,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var tenant = await tenantDatabaseResolver.ResolveByTenantIdAsync(command.TenantId, cancellationToken);
        if (tenant is null)
        {
            return null;
        }

        await using var store = identityStoreFactory.Create(tenant);
        var tokenHash = refreshTokenGenerator.Hash(command.RefreshToken);
        var currentToken = await store.FindRefreshTokenByHashAsync(tokenHash, cancellationToken);
        var now = timeProvider.GetUtcNow();

        if (currentToken is null || currentToken.ExpiresAt <= now)
        {
            return null;
        }

        if (currentToken.RevokedAt is not null)
        {
            await store.RevokeTokenFamilyAsync(currentToken.FamilyId, now, cancellationToken);
            return null;
        }

        var user = await store.FindUserByIdAsync(currentToken.UserId, cancellationToken);
        if (user is null || !user.IsActive || user.LockedUntil > now)
        {
            return null;
        }

        var session = CreateSession(tenant, user, now);
        var replacement = CreateRefreshToken(user.Id, session.RefreshToken, session.RefreshTokenExpiresAt, now, currentToken.FamilyId);
        var rotated = await store.TryRotateRefreshTokenAsync(
            currentToken,
            replacement,
            CreateAudit(user.Id, AuditAction.RefreshTokenRevoked, correlationId, now),
            cancellationToken);

        return rotated ? session : null;
    }

    public async Task<bool> LogoutAsync(
        LogoutCommand command,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var tenant = await tenantDatabaseResolver.ResolveByTenantIdAsync(command.TenantId, cancellationToken);
        if (tenant is null)
        {
            return false;
        }

        await using var store = identityStoreFactory.Create(tenant);
        var token = await store.FindRefreshTokenByHashAsync(refreshTokenGenerator.Hash(command.RefreshToken), cancellationToken);
        if (token is null)
        {
            return false;
        }

        return await store.RevokeRefreshTokenAsync(
            token,
            CreateAudit(token.UserId, AuditAction.Logout, correlationId, timeProvider.GetUtcNow()),
            cancellationToken);
    }

    public async Task<SessionContext?> GetSessionContextAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var tenant = await tenantDatabaseResolver.ResolveByTenantIdAsync(tenantId, cancellationToken);
        if (tenant is null)
        {
            return null;
        }

        await using var store = identityStoreFactory.Create(tenant);
        var user = await store.FindUserByIdAsync(userId, cancellationToken);
        return user is { IsActive: true } ? CreateContext(tenant, user) : null;
    }

    private AuthenticatedSession CreateSession(ResolvedTenantDatabase tenant, User user, DateTimeOffset now)
    {
        var context = CreateContext(tenant, user);
        var accessToken = accessTokenIssuer.Issue(user, tenant.TenantId, tenant.TenantSlug, context.Roles);
        var refreshToken = refreshTokenGenerator.Generate();
        return new AuthenticatedSession(
            accessToken.Token,
            refreshToken.Token,
            accessToken.ExpiresAt,
            now.Add(RefreshTokenLifetime),
            context);
    }

    private RefreshToken CreateRefreshToken(
        Guid userId,
        string rawToken,
        DateTimeOffset expiresAt,
        DateTimeOffset now,
        Guid? familyId = null) => new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = refreshTokenGenerator.Hash(rawToken),
            FamilyId = familyId ?? Guid.NewGuid(),
            ExpiresAt = expiresAt,
            CreatedAt = now
        };

    private static SessionContext CreateContext(ResolvedTenantDatabase tenant, User user)
    {
        var roles = user.UserRoles.Select(x => x.Role.Name).Distinct(StringComparer.Ordinal).Order().ToArray();
        var permissions = user.UserRoles
            .SelectMany(x => x.Role.RolePermissions)
            .Select(x => x.Permission.Code)
            .Distinct(StringComparer.Ordinal)
            .Order()
            .ToArray();
        var allowNegativeStock = tenant.Settings.TryGetValue(TenantSettingKeys.AllowNegativeStock, out var value)
            && bool.TryParse(value, out var parsed)
            && parsed;

        return new SessionContext(
            new SessionUser(user.Id, user.Name, user.Email),
            new SessionTenant(tenant.TenantId, tenant.TenantName, tenant.TenantSlug),
            roles,
            permissions,
            tenant.Features,
            new Dictionary<string, object> { [TenantSettingKeys.AllowNegativeStock] = allowNegativeStock });
    }

    private static AuditLog CreateAudit(
        Guid? userId,
        AuditAction action,
        string correlationId,
        DateTimeOffset occurredAt) => new()
        {
            UserId = userId,
            Action = action,
            EntityName = nameof(User),
            EntityId = userId?.ToString(),
            CorrelationId = correlationId,
            OccurredAt = occurredAt
        };
}
