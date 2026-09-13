namespace Forjix.Domain.Entities.Master;

public sealed class TenantSetting
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public required string Key { get; set; }
    public required string Value { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Tenant Tenant { get; set; } = null!;
}

