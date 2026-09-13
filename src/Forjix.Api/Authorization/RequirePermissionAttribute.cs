using Forjix.Infrastructure.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace Forjix.Api.Authorization;

public sealed class RequirePermissionAttribute(string permission) : AuthorizeAttribute(
    PermissionPolicyProvider.PolicyPrefix + permission);
