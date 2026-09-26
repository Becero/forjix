using Forjix.Api.Authorization;
using Forjix.Application.Common;
using Forjix.Application.Features.Financial;
using Microsoft.AspNetCore.Mvc;
namespace Forjix.Api.Controllers;
[ApiController, Route("api/financial/dashboard")]
public sealed class FinancialDashboardController(IFinancialService service) : ControllerBase
{
    [HttpGet, RequirePermission(Permissions.FinancialDashboardView)]
    public async Task<IActionResult> Get(DateOnly? from, DateOnly? through, CancellationToken ct) => Ok(await service.DashboardAsync(from, through, ct));
}
