using Forjix.Application.Abstractions.Security;
using Forjix.Application.Abstractions.Tenancy;
using Forjix.Domain.Enums;
using Forjix.Infrastructure.Persistence.Master;
using Microsoft.EntityFrameworkCore;

namespace Forjix.Infrastructure.Tenancy;

internal sealed class TenantDatabaseResolver(
    ForjixMasterDbContext masterDbContext,
    ISecretProvider secretProvider,
    TimeProvider timeProvider) : ITenantDatabaseResolver
{
    public async Task<ResolvedTenantDatabase?> ResolveBySlugAsync(
        string tenantSlug,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantSlug);
        var normalizedSlug = tenantSlug.Trim().ToLowerInvariant();

        return await ResolveAsync(x => x.Slug == normalizedSlug, cancellationToken);
    }

    public async Task<ResolvedTenantDatabase?> ResolveByTenantIdAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
        {
            return null;
        }

        return await ResolveAsync(x => x.Id == tenantId, cancellationToken);
    }

    private async Task<ResolvedTenantDatabase?> ResolveAsync(
        System.Linq.Expressions.Expression<Func<Forjix.Domain.Entities.Master.Tenant, bool>> predicate,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var tenant = await masterDbContext.Tenants
            .AsNoTracking()
            .Where(predicate)
            .Include(x => x.Database)
            .Include(x => x.Subscriptions)
            .Include(x => x.Features)
            .Include(x => x.Settings)
            .SingleOrDefaultAsync(
                x => x.Status == TenantStatus.Active &&
                     x.Subscriptions.Any(subscription =>
                         subscription.Status == SubscriptionStatus.Active &&
                         subscription.StartsAt <= now &&
                         (subscription.EndsAt == null || subscription.EndsAt > now)),
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
            tenant.Name,
            tenant.Slug,
            tenant.Database.DatabaseName,
            connectionString,
            tenant.Database.SchemaVersion,
            tenant.Features
                .Where(x => x.IsEnabled && (x.ExpiresAt is null || x.ExpiresAt > now))
                .Select(x => x.FeatureCode)
                .Order(StringComparer.Ordinal)
                .ToArray(),
            tenant.Settings.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase));
    }
}
