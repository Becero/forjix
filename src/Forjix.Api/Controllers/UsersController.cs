using Forjix.Api.Authorization;
using Forjix.Application.Common;
using Forjix.Application.Features.Management;
using Microsoft.AspNetCore.Mvc;

namespace Forjix.Api.Controllers;

[ApiController, Route("api/users")]
public sealed class UsersController(IManagementService service) : ControllerBase
{
    [HttpGet, RequirePermission(Permissions.AdministrationUsersView)]
    public async Task<ActionResult<IReadOnlyList<UserItem>>> Get(CancellationToken cancellationToken) => Ok(await service.GetUsersAsync(cancellationToken));

    [HttpPost, RequirePermission(Permissions.AdministrationUsersManage)]
    public async Task<ActionResult<UserItem>> Post(SaveUserRequest request, CancellationToken cancellationToken) => StatusCode(StatusCodes.Status201Created, await service.CreateUserAsync(request, cancellationToken));

    [HttpPut("{id:guid}"), RequirePermission(Permissions.AdministrationUsersManage)]
    public async Task<ActionResult<UserItem>> Put(Guid id, SaveUserRequest request, CancellationToken cancellationToken) => Ok(await service.UpdateUserAsync(id, request, cancellationToken));
}
