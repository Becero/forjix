using Forjix.Api.Authorization;
using Forjix.Application.Common;
using Forjix.Application.Features.Inventory;
using Microsoft.AspNetCore.Mvc;
namespace Forjix.Api.Controllers;
[ApiController,Route("api/stock")]
public sealed class StockController(IStocktakeService service) : ControllerBase
{
    [HttpGet("overview"),RequirePermission(Permissions.StockView)]
    public async Task<IActionResult> Overview(CancellationToken ct) => Ok(await service.OverviewAsync(ct));
    [HttpGet("movements"),RequirePermission(Permissions.StockView)]
    public async Task<IActionResult> Movements([FromQuery] StockMovementFilter filter,CancellationToken ct) => Ok(await service.MovementsAsync(filter,ct));
    [HttpPost("adjustments"),RequirePermission(Permissions.StockAdjust)]
    public async Task<IActionResult> Adjust(StockAdjustmentRequest request,CancellationToken ct) => Ok(await service.AdjustAsync(request,ct));
    [HttpGet("reports/low-stock"),RequirePermission(Permissions.StockReports)]
    public async Task<IActionResult> Low(CancellationToken ct) => Ok(await service.ReportAsync(true,30,null,null,ct));
    [HttpGet("reports/no-movement"),RequirePermission(Permissions.StockReports)]
    public async Task<IActionResult> NoMovement([FromQuery] int days = 30,[FromQuery] DateTimeOffset? from = null,[FromQuery] DateTimeOffset? to = null,CancellationToken ct = default) => Ok(await service.ReportAsync(false,days,from,to,ct));
}
