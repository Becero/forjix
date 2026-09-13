using Forjix.Domain.Enums;

namespace Forjix.Domain.Entities.Master;

public sealed class Tenant
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string Slug { get; set; }
    public string? Cnpj { get; set; }
    public TenantStatus Status { get; set; } = TenantStatus.Provisioning;
    public Guid? PlanId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Plan? Plan { get; set; }
    public TenantDatabase? Database { get; set; }
    public ICollection<Subscription> Subscriptions { get; set; } = [];
    public ICollection<TenantSetting> Settings { get; set; } = [];
    public ICollection<TenantFeature> Features { get; set; } = [];
    public ICollection<MigrationExecution> MigrationExecutions { get; set; } = [];
}
