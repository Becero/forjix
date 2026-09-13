namespace Forjix.Application.Abstractions.Identity;

public interface ICurrentUser
{
    Guid? UserId { get; }
    Guid? TenantId { get; }
    string? TenantSlug { get; }
    bool IsAuthenticated { get; }
    string CorrelationId { get; }
    string? IpAddress { get; }
}
