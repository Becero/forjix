using Forjix.Application.Abstractions.Authorization;
using Forjix.Application.Common;
using Microsoft.AspNetCore.Authorization;

namespace Forjix.Infrastructure.Authorization;

internal sealed class PermissionAuthorizationHandler(IPermissionChecker permissionChecker)
    : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (!Guid.TryParse(context.User.FindFirst(ForjixClaimNames.TenantId)?.Value, out var tenantId) ||
            !Guid.TryParse(context.User.FindFirst(ForjixClaimNames.UserId)?.Value, out var userId))
        {
            return;
        }

        if (await permissionChecker.HasPermissionAsync(tenantId, userId, requirement.Permission))
        {
            context.Succeed(requirement);
        }
    }
}
