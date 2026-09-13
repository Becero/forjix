namespace Forjix.Application.Features.Authentication;

public sealed record LoginCommand(string TenantSlug, string Email, string Password);

public sealed record RefreshCommand(Guid TenantId, string RefreshToken);

public sealed record LogoutCommand(Guid TenantId, string RefreshToken);

public sealed record AuthenticatedSession(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset AccessTokenExpiresAt,
    DateTimeOffset RefreshTokenExpiresAt,
    SessionContext Context);

public sealed record SessionContext(
    SessionUser User,
    SessionTenant Tenant,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions,
    IReadOnlyList<string> Features,
    IReadOnlyDictionary<string, object> Settings);

public sealed record SessionUser(Guid Id, string Name, string Email);

public sealed record SessionTenant(Guid Id, string Name, string Slug);
