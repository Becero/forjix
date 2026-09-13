using Forjix.Application.Abstractions.Identity;
using Forjix.Application.Features.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Forjix.Api.Controllers;

[ApiController]
[Route("api/me")]
[Authorize]
public sealed class MeController(
    ICurrentUser currentUser,
    IAuthenticationService authenticationService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<SessionContext>> Get(CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId || currentUser.TenantId is not { } tenantId)
        {
            return Unauthorized();
        }

        var context = await authenticationService.GetSessionContextAsync(tenantId, userId, cancellationToken);
        return context is null ? Unauthorized() : Ok(context);
    }
}
