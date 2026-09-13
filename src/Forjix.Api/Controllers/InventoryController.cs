using Forjix.Api.Authorization;
using Forjix.Application.Common;
using Forjix.Application.Features.Inventory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Forjix.Api.Controllers;

[ApiController]
[Route("api/inventory")]
[Authorize]
public sealed class InventoryController(IInventoryService service) : ControllerBase
{
    [HttpGet]
    [RequirePermission(Permissions.StockView)]
    public async Task<IActionResult> Get([FromQuery] string? search, [FromQuery] Guid? categoryId, [FromQuery] string? status, CancellationToken ct) => Ok(await service.GetAsync(search, categoryId, status, ct));

    [HttpGet("{productId:guid}")]
    [RequirePermission(Permissions.StockView)]
    public async Task<IActionResult> GetByProduct(Guid productId, CancellationToken ct) => Ok(await service.GetByProductAsync(productId, ct));

    [HttpGet("{productId:guid}/movements")]
    [RequirePermission(Permissions.StockView)]
    public async Task<IActionResult> GetMovements(Guid productId, [FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to, [FromQuery] string? type, [FromQuery] string? user, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default) => Ok(await service.GetMovementsAsync(productId, from, to, type, user, page, pageSize, ct));

    [HttpPost("{productId:guid}/movements")]
    [RequirePermission(Permissions.StockManage)]
    public async Task<IActionResult> CreateMovement(Guid productId, CreateInventoryMovementRequest request, CancellationToken ct) => Ok(await service.CreateMovementAsync(productId, request, ct));
}
