using Forjix.Domain.Enums;

namespace Forjix.Domain.Entities.Audit;

public sealed class AuditLog
{
    public long Id { get; set; }
    public Guid? UserId { get; set; }
    public AuditAction Action { get; set; }
    public required string EntityName { get; set; }
    public string? EntityId { get; set; }
    public string? BeforeData { get; set; }
    public string? AfterData { get; set; }
    public string? IpAddress { get; set; }
    public required string CorrelationId { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
}

