namespace Forjix.Application.Abstractions.Tenancy;

public interface ITenantContext
{
    Guid TenantId { get; }
    string TenantSlug { get; }
    bool IsResolved { get; }
}

