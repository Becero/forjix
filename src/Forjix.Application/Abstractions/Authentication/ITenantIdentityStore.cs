using Forjix.Application.Abstractions.Tenancy;
using Forjix.Domain.Entities.Audit;
using Forjix.Domain.Entities.Identity;

namespace Forjix.Application.Abstractions.Authentication;

public interface ITenantIdentityStoreFactory
{
    ITenantIdentityStore Create(ResolvedTenantDatabase tenantDatabase);
}
public interface ITenantIdentityStore : IAsyncDisposable
{
    Task<User?> FindUserByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken);
    Task<User?> FindUserByIdAsync(Guid userId, CancellationToken cancellationToken);
    Task<RefreshToken?> FindRefreshTokenByHashAsync(string tokenHash, CancellationToken cancellationToken);
    Task AddLoginResultAsync(RefreshToken? refreshToken, AuditLog auditLog, CancellationToken cancellationToken);
    Task<bool> TryRotateRefreshTokenAsync(RefreshToken currentToken, RefreshToken replacement, AuditLog auditLog, CancellationToken cancellationToken);
    Task<bool> RevokeRefreshTokenAsync(RefreshToken refreshToken, AuditLog auditLog, CancellationToken cancellationToken);
    Task RevokeTokenFamilyAsync(Guid familyId, DateTimeOffset revokedAt, CancellationToken cancellationToken);
    Task<bool> HasPermissionAsync(Guid userId, string permission, CancellationToken cancellationToken);
}
