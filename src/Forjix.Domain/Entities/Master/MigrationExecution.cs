namespace Forjix.Domain.Entities.Master;

public sealed class MigrationExecution
{
    public Guid Id { get; set; }
    public Guid? TenantId { get; set; }
    public required string DatabaseName { get; set; }
    public required string MigrationType { get; set; }
    public required string Status { get; set; }
    public string? AppliedMigration { get; set; }
    public string? ErrorSummary { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset CompletedAt { get; set; }

    public Tenant? Tenant { get; set; }
}
