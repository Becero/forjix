using Forjix.Application.Abstractions.Tenancy;

namespace Forjix.Infrastructure.Tenancy;

internal sealed class TenantContext : ITenantContext
{
    public Guid TenantId => Guid.Empty;
    public string TenantSlug => string.Empty;
    public bool IsResolved => false;
}

