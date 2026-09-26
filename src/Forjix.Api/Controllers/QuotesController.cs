using Forjix.Api.Authorization;
using Forjix.Application.Common;
using Forjix.Application.Features.Quotes;
using Microsoft.AspNetCore.Mvc;
namespace Forjix.Api.Controllers;
[ApiController, Route("api/quotes")]
public sealed class QuotesController(IQuoteService service) : ControllerBase
{
    [HttpGet, RequirePermission(Permissions.QuotesView)]
    public async Task<IActionResult> List([FromQuery] QuoteFilter filter, CancellationToken ct) => Ok(await service.ListAsync(filter, ct));
    [HttpGet("summary"), RequirePermission(Permissions.QuotesView)]
    public async Task<IActionResult> Summary(CancellationToken ct) => Ok(await service.SummaryAsync(ct));
    [HttpGet("{id:guid}"), RequirePermission(Permissions.QuotesView)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) => Ok(await service.GetAsync(id, ct));
    [HttpPost, RequirePermission(Permissions.QuotesCreate)]
    public async Task<IActionResult> Create(SaveQuoteRequest request, CancellationToken ct) => StatusCode(201, await service.SaveAsync(null, request, ct));
    [HttpPut("{id:guid}"), RequirePermission(Permissions.QuotesEdit)]
    public async Task<IActionResult> Update(Guid id, SaveQuoteRequest request, CancellationToken ct) => Ok(await service.SaveAsync(id, request, ct));
    [HttpPost("{id:guid}/send"), RequirePermission(Permissions.QuotesStatus)]
    public async Task<IActionResult> Send(Guid id, QuoteActionRequest request, CancellationToken ct) => Ok(await service.TransitionAsync(id, "send", request, ct));
    [HttpPost("{id:guid}/approve"), RequirePermission(Permissions.QuotesStatus)]
    public async Task<IActionResult> Approve(Guid id, QuoteActionRequest request, CancellationToken ct) => Ok(await service.TransitionAsync(id, "approve", request, ct));
    [HttpPost("{id:guid}/reject"), RequirePermission(Permissions.QuotesStatus)]
    public async Task<IActionResult> Reject(Guid id, QuoteActionRequest request, CancellationToken ct) => Ok(await service.TransitionAsync(id, "reject", request, ct));
    [HttpPost("{id:guid}/cancel"), RequirePermission(Permissions.QuotesCancel)]
    public async Task<IActionResult> Cancel(Guid id, QuoteActionRequest request, CancellationToken ct) => Ok(await service.TransitionAsync(id, "cancel", request, ct));
    [HttpPost("{id:guid}/convert-to-sale"), RequirePermission(Permissions.QuotesConvert)]
    public async Task<IActionResult> Convert(Guid id, ConvertQuoteRequest request, CancellationToken ct) => Ok(await service.ConvertAsync(id, request, ct));
    [HttpGet("{id:guid}/pdf"), RequirePermission(Permissions.QuotesPrint)]
    public async Task<IActionResult> Pdf(Guid id, CancellationToken ct)
    {
        var file = await service.PdfAsync(id, ct); return File(file.Content, file.ContentType, file.FileName);
    }
}
