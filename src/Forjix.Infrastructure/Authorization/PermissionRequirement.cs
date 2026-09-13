using Microsoft.AspNetCore.Authorization;

namespace Forjix.Infrastructure.Authorization;

public sealed record PermissionRequirement(string Permission) : IAuthorizationRequirement;
