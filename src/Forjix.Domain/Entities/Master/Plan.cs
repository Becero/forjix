namespace Forjix.Domain.Entities.Master;

public sealed class Plan
{
    public Guid Id { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public bool IsActive { get; set; } = true;
    public int UserLimit { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<Tenant> Tenants { get; set; } = [];
    public ICollection<Subscription> Subscriptions { get; set; } = [];
}

