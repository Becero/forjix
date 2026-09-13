namespace Forjix.Application.Abstractions.Tenancy;

public interface ITenantDatabaseResolver
{
    Task<ResolvedTenantDatabase?> ResolveBySlugAsync(
        string tenantSlug,
        CancellationToken cancellationToken = default);
}

public sealed record ResolvedTenantDatabase(
    Guid TenantId,
    string TenantSlug,
    string DatabaseName,
    string ConnectionString,
    string SchemaVersion);

