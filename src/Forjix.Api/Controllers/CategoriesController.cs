using Forjix.Api.Authorization;
using Forjix.Application.Common;
using Forjix.Application.Features.Management;
using Microsoft.AspNetCore.Mvc;

namespace Forjix.Api.Controllers;

[ApiController, Route("api/categories")]
public sealed class CategoriesController(IManagementService service) : ControllerBase
{
    [HttpGet, RequirePermission(Permissions.CategoriesView)]
    public async Task<ActionResult<IReadOnlyList<CategoryItem>>> Get(bool includeInactive = true, CancellationToken cancellationToken = default) => Ok(await service.GetCategoriesAsync(includeInactive, cancellationToken));

    [HttpPost, RequirePermission(Permissions.CategoriesManage)]
    public async Task<ActionResult<CategoryItem>> Post(SaveCategoryRequest request, CancellationToken cancellationToken) => StatusCode(StatusCodes.Status201Created, await service.CreateCategoryAsync(request, cancellationToken));

    [HttpPut("{id:guid}"), RequirePermission(Permissions.CategoriesManage)]
    public async Task<ActionResult<CategoryItem>> Put(Guid id, SaveCategoryRequest request, CancellationToken cancellationToken) => Ok(await service.UpdateCategoryAsync(id, request, cancellationToken));

    [HttpDelete("{id:guid}"), RequirePermission(Permissions.CategoriesManage)]
    public async Task<ActionResult<CategoryItem>> Delete(Guid id, CancellationToken cancellationToken) => Ok(await service.DeactivateCategoryAsync(id, cancellationToken));
}
