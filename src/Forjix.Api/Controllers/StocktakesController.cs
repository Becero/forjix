using Forjix.Api.Authorization;
using Forjix.Application.Common;
using Forjix.Application.Features.Inventory;
using Microsoft.AspNetCore.Mvc;
namespace Forjix.Api.Controllers;
[ApiController,Route("api/inventories")]
public sealed class StocktakesController(IStocktakeService service) : ControllerBase
{
    [HttpGet,RequirePermission(Permissions.StockInventoryView)]
    public async Task<IActionResult> List([FromQuery] StocktakeFilter filter,CancellationToken ct) => Ok(await service.ListAsync(filter,ct));
    [HttpGet("{id:guid}"),RequirePermission(Permissions.StockInventoryView)]
    public async Task<IActionResult> Get(Guid id,CancellationToken ct) => Ok(await service.GetAsync(id,ct));
    [HttpPost,RequirePermission(Permissions.StockInventoryCreate)]
    public async Task<IActionResult> Create(SaveStocktakeRequest request,CancellationToken ct) => StatusCode(201,await service.SaveAsync(null,request,ct));
    [HttpPut("{id:guid}"),RequirePermission(Permissions.StockInventoryCreate)]
    public async Task<IActionResult> Update(Guid id,SaveStocktakeRequest request,CancellationToken ct) => Ok(await service.SaveAsync(id,request,ct));
    [HttpPost("{id:guid}/start"),RequirePermission(Permissions.StockInventoryCount)]
    public async Task<IActionResult> Start(Guid id,StocktakeActionRequest request,CancellationToken ct) => Ok(await service.ActionAsync(id,"start",request,ct));
    [HttpPost("{id:guid}/count"),RequirePermission(Permissions.StockInventoryCount)]
    public async Task<IActionResult> Count(Guid id,CountStocktakeRequest request,CancellationToken ct) => Ok(await service.CountAsync(id,request,ct));
    [HttpPost("{id:guid}/complete"),RequirePermission(Permissions.StockInventoryComplete)]
    public async Task<IActionResult> Complete(Guid id,StocktakeActionRequest request,CancellationToken ct) => Ok(await service.ActionAsync(id,"complete",request,ct));
    [HttpPost("{id:guid}/cancel"),RequirePermission(Permissions.StockInventoryCancel)]
    public async Task<IActionResult> Cancel(Guid id,StocktakeActionRequest request,CancellationToken ct) => Ok(await service.ActionAsync(id,"cancel",request,ct));
}
