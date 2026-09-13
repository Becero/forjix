using Forjix.Api.Authorization;
using Forjix.Application.Common;
using Forjix.Application.Features.Analytics;
using Microsoft.AspNetCore.Mvc;
namespace Forjix.Api.Controllers; [ApiController, Route("api/dashboard")] public sealed class DashboardController(IAnalyticsService service) : ControllerBase { [HttpGet, RequirePermission(Permissions.DashboardView)] public async Task<ActionResult<DashboardView>> Get(CancellationToken ct) => Ok(await service.DashboardAsync(ct)); }
