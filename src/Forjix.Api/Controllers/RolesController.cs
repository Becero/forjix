using Forjix.Api.Authorization;
using Forjix.Application.Common;
using Forjix.Application.Features.Management;
using Microsoft.AspNetCore.Mvc;

namespace Forjix.Api.Controllers;

[ApiController, Route("api/roles")]
public sealed class RolesController(IManagementService service) : ControllerBase
{
    [HttpGet, RequirePermission(Permissions.AdministrationRolesView)]
    public async Task<ActionResult<IReadOnlyList<RoleItem>>> Get(CancellationToken cancellationToken) => Ok(await service.GetRolesAsync(cancellationToken));

    [HttpGet("permissions"), RequirePermission(Permissions.AdministrationRolesView)]
    public async Task<ActionResult<IReadOnlyList<PermissionItem>>> ListPermissions(CancellationToken cancellationToken) => Ok(await service.GetPermissionsAsync(cancellationToken));

    [HttpPost, RequirePermission(Permissions.AdministrationRolesManage)]
    public async Task<ActionResult<RoleItem>> Post(SaveRoleRequest request, CancellationToken cancellationToken) => StatusCode(StatusCodes.Status201Created, await service.CreateRoleAsync(request, cancellationToken));

    [HttpPut("{id:guid}"), RequirePermission(Permissions.AdministrationRolesManage)]
    public async Task<ActionResult<RoleItem>> Put(Guid id, SaveRoleRequest request, CancellationToken cancellationToken) => Ok(await service.UpdateRoleAsync(id, request, cancellationToken));
}
