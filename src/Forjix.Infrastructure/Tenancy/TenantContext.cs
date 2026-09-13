using Forjix.Application.Abstractions.Identity;
using Forjix.Application.Abstractions.Tenancy;

namespace Forjix.Infrastructure.Tenancy;

internal sealed class TenantContext(ICurrentUser currentUser) : ITenantContext
{
    public Guid TenantId => currentUser.TenantId ?? Guid.Empty;
    public string TenantSlug => currentUser.TenantSlug ?? string.Empty;
    public bool IsResolved => currentUser.IsAuthenticated && currentUser.TenantId.HasValue && !string.IsNullOrWhiteSpace(currentUser.TenantSlug);
}
