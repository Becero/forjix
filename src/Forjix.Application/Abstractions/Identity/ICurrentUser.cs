namespace Forjix.Application.Abstractions.Identity;

public interface ICurrentUser
{
    Guid? UserId { get; }
    Guid? TenantId { get; }
    string? TenantSlug { get; }
    bool IsAuthenticated { get; }
}

