namespace Forjix.Domain.Entities.Master;

public sealed class TenantFeature
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public required string FeatureCode { get; set; }
    public bool IsEnabled { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Tenant Tenant { get; set; } = null!;
}

