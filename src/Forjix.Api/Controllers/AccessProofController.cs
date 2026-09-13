using Forjix.Api.Authorization;
using Forjix.Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace Forjix.Api.Controllers;

[ApiController]
[Route("api/access-proof")]
public sealed class AccessProofController : ControllerBase
{
    [HttpGet("administration-users")]
    [RequirePermission(Permissions.AdministrationUsersView)]
    public IActionResult AdministrationUsers() => Ok(new { allowed = true });
}
