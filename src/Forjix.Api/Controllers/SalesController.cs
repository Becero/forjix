using Forjix.Api.Authorization;
using Forjix.Application.Common;
using Forjix.Application.Features.Sales;
using Microsoft.AspNetCore.Mvc;

namespace Forjix.Api.Controllers;

[ApiController, Route("api/sales")]
public sealed class SalesController(ISalesService service) : ControllerBase
{
    [HttpGet, RequirePermission(Permissions.SalesView)]
    public async Task<ActionResult<PagedSales>> Get(DateTimeOffset? from, DateTimeOffset? to, string? status, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default) => Ok(await service.GetAsync(from, to, status, page, pageSize, cancellationToken));

    [HttpGet("{id:guid}"), RequirePermission(Permissions.SalesView)]
    public async Task<ActionResult<SaleView>> GetById(Guid id, CancellationToken cancellationToken) => Ok(await service.GetByIdAsync(id, cancellationToken));

    [HttpPost, RequirePermission(Permissions.SalesCreate)]
    public async Task<ActionResult<SaleView>> Post([FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, CreateSaleRequest request, CancellationToken cancellationToken) => StatusCode(StatusCodes.Status201Created, await service.CreateAsync(idempotencyKey ?? string.Empty, request, cancellationToken));

    [HttpPost("{id:guid}/cancel"), RequirePermission(Permissions.SalesCancel)]
    public async Task<ActionResult<SaleView>> Cancel(Guid id, CancelSaleRequest request, CancellationToken cancellationToken) => Ok(await service.CancelAsync(id, request, cancellationToken));
}
