using Forjix.Api.Authorization;
using Forjix.Application.Common;
using Forjix.Application.Features.Financial;
using Forjix.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace Forjix.Api.Controllers;

[ApiController, Route("api/financial/accounts-payable")]
public sealed class AccountsPayableController(IFinancialService service) : ControllerBase
{
    private const FinancialOperationType Type = FinancialOperationType.Payable;
    [HttpGet, RequirePermission(Permissions.FinancialPayableView)]
    public async Task<IActionResult> Get([FromQuery] FinancialAccountFilter filter, CancellationToken ct) => Ok(await service.ListAsync(Type, filter, ct));
    [HttpGet("{id:guid}"), RequirePermission(Permissions.FinancialPayableView)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) => Ok(await service.GetAsync(Type, id, ct));
    [HttpPost, RequirePermission(Permissions.FinancialPayableManage)]
    public async Task<IActionResult> Post(SaveFinancialAccountRequest request, CancellationToken ct) => StatusCode(201, await service.CreateAsync(Type, request, ct));
    [HttpPut("{id:guid}"), RequirePermission(Permissions.FinancialPayableManage)]
    public async Task<IActionResult> Put(Guid id, SaveFinancialAccountRequest request, CancellationToken ct) => Ok(await service.UpdateAsync(Type, id, request, ct));
    [HttpPost("{id:guid}/cancel"), RequirePermission(Permissions.FinancialPayableManage)]
    public async Task<IActionResult> Cancel(Guid id, FinancialActionRequest request, CancellationToken ct) => Ok(await service.CancelAsync(Type, id, request, ct));
    [HttpGet("{id:guid}/payments"), RequirePermission(Permissions.FinancialPayableView)]
    public async Task<IActionResult> Payments(Guid id, CancellationToken ct) => Ok(await service.PaymentsAsync(Type, id, ct));
    [HttpPost("{id:guid}/payments"), RequirePermission(Permissions.FinancialPayablePay)]
    public async Task<IActionResult> Pay(Guid id, [FromHeader(Name = "Idempotency-Key")] string? key, CreateFinancialPaymentRequest request, CancellationToken ct) => Ok(await service.PayAsync(Type, id, key ?? "", request, ct));
    [HttpPost("{id:guid}/payments/{paymentId:guid}/reverse"), RequirePermission(Permissions.FinancialPayablePay)]
    public async Task<IActionResult> Reverse(Guid id, Guid paymentId, FinancialActionRequest request, CancellationToken ct) => Ok(await service.ReverseAsync(Type, id, paymentId, request, ct));
}
