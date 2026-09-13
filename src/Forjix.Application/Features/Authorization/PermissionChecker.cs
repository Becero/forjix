using Forjix.Application.Abstractions.Authentication;
using Forjix.Application.Abstractions.Authorization;
using Forjix.Application.Abstractions.Tenancy;

namespace Forjix.Application.Features.Authorization;

internal sealed class PermissionChecker(
    ITenantDatabaseResolver tenantDatabaseResolver,
    ITenantIdentityStoreFactory identityStoreFactory) : IPermissionChecker
{
    public async Task<bool> HasPermissionAsync(
        Guid tenantId,
        Guid userId,
        string permission,
        CancellationToken cancellationToken = default)
    {
        var tenant = await tenantDatabaseResolver.ResolveByTenantIdAsync(tenantId, cancellationToken);
        if (tenant is null)
        {
            return false;
        }

        await using var store = identityStoreFactory.Create(tenant);
        return await store.HasPermissionAsync(userId, permission, cancellationToken);
    }
}
