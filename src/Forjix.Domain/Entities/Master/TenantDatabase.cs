namespace Forjix.Domain.Entities.Master;

public sealed class TenantDatabase
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public required string DatabaseName { get; set; }
    public required string ServerReference { get; set; }
    public required string SecretReference { get; set; }
    public required string SchemaVersion { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Tenant Tenant { get; set; } = null!;
}

