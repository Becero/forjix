using Forjix.Domain.Entities.Identity;

namespace Forjix.Application.Abstractions.Authentication;

public interface IAccessTokenIssuer
{
    AccessTokenResult Issue(
        User user,
        Guid tenantId,
        string tenantSlug,
        IReadOnlyCollection<string> roles);
}

public sealed record AccessTokenResult(string Token, DateTimeOffset ExpiresAt);
