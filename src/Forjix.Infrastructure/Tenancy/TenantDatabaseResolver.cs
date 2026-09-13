using Forjix.Application.Abstractions.Security;
using Forjix.Application.Abstractions.Tenancy;
using Forjix.Domain.Enums;
using Forjix.Infrastructure.Persistence.Master;
using Microsoft.EntityFrameworkCore;

namespace Forjix.Infrastructure.Tenancy;

internal sealed class TenantDatabaseResolver(
    ForjixMasterDbContext masterDbContext,
    ISecretProvider secretProvider) : ITenantDatabaseResolver
{
    public async Task<ResolvedTenantDatabase?> ResolveBySlugAsync(
        string tenantSlug,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantSlug);
        var normalizedSlug = tenantSlug.Trim().ToLowerInvariant();

        var tenant = await masterDbContext.Tenants
            .AsNoTracking()
            .Include(x => x.Database)
            .SingleOrDefaultAsync(
                x => x.Slug == normalizedSlug && x.Status == TenantStatus.Active,
                cancellationToken);

        if (tenant?.Database is null)
        {
            return null;
        }

        var connectionString = await secretProvider.GetSecretAsync(
            tenant.Database.SecretReference,
            cancellationToken);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("The tenant database secret could not be resolved.");
        }

        return new ResolvedTenantDatabase(
            tenant.Id,
            tenant.Slug,
            tenant.Database.DatabaseName,
            connectionString,
            tenant.Database.SchemaVersion);
    }
}

