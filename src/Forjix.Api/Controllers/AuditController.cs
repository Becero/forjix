using Forjix.Api.Authorization;
using Forjix.Application.Common;
using Forjix.Application.Features.Management;
using Microsoft.AspNetCore.Mvc;

namespace Forjix.Api.Controllers;

[ApiController, Route("api/audit")]
public sealed class AuditController(IManagementService service) : ControllerBase
{
    [HttpGet, RequirePermission(Permissions.AuditView)]
    public async Task<ActionResult<IReadOnlyList<AuditItem>>> Get(Guid? userId, string? action, string? entity, DateTimeOffset? from, [FromQuery(Name = "to")] DateTimeOffset? toDate, CancellationToken cancellationToken) =>
        Ok(await service.GetAuditAsync(userId, action, entity, from, toDate, cancellationToken));
}
