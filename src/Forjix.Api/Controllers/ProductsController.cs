using Forjix.Api.Authorization;
using Forjix.Application.Common;
using Forjix.Application.Features.Management;
using Microsoft.AspNetCore.Mvc;

namespace Forjix.Api.Controllers;

[ApiController, Route("api/products")]
public sealed class ProductsController(IManagementService service) : ControllerBase
{
    [HttpGet, RequirePermission(Permissions.ProductsView)]
    public async Task<ActionResult<PagedProducts>> Get(string? search, Guid? categoryId, bool? isActive, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default) => Ok(await service.GetProductsAsync(search, categoryId, isActive, page, pageSize, cancellationToken));

    [HttpPost, RequirePermission(Permissions.ProductsManage)]
    public async Task<ActionResult<ProductItem>> Post(SaveProductRequest request, CancellationToken cancellationToken) => StatusCode(StatusCodes.Status201Created, await service.CreateProductAsync(request, cancellationToken));

    [HttpPut("{id:guid}"), RequirePermission(Permissions.ProductsManage)]
    public async Task<ActionResult<ProductItem>> Put(Guid id, SaveProductRequest request, CancellationToken cancellationToken) => Ok(await service.UpdateProductAsync(id, request, cancellationToken));

    [HttpDelete("{id:guid}"), RequirePermission(Permissions.ProductsManage)]
    public async Task<ActionResult<ProductItem>> Delete(Guid id, CancellationToken cancellationToken) => Ok(await service.DeactivateProductAsync(id, cancellationToken));
}
