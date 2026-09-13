using Forjix.Api.Authorization;
using Forjix.Application.Common;
using Forjix.Application.Features.Analytics;
using Microsoft.AspNetCore.Mvc;
namespace Forjix.Api.Controllers; [ApiController, Route("api/reports")] public sealed class ReportsController(IAnalyticsService service) : ControllerBase { [HttpGet, RequirePermission(Permissions.ReportsView)] public async Task<ActionResult<ReportView>> Get(DateTimeOffset? from, DateTimeOffset? through, CancellationToken ct) => Ok(await service.ReportAsync(from, through, ct)); [HttpGet("export"), RequirePermission(Permissions.ReportsExport)] public async Task<IActionResult> Export(DateTimeOffset? from, DateTimeOffset? through, string format, CancellationToken ct) { var file = await service.ExportAsync(from, through, format, ct); return File(file.Content, file.ContentType, file.FileName); } }
