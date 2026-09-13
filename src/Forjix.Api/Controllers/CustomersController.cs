using Forjix.Api.Authorization;
using Forjix.Application.Common;
using Forjix.Application.Features.Customers;
using Microsoft.AspNetCore.Mvc;

namespace Forjix.Api.Controllers;

[ApiController, Route("api/customers")]
public sealed class CustomersController(ICustomerService service) : ControllerBase
{
    [HttpGet, RequirePermission(Permissions.CustomersView)] public async Task<ActionResult<PagedCustomers>> Get(string? search, bool? isActive, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default) => Ok(await service.GetAsync(search, isActive, page, pageSize, cancellationToken));
    [HttpPost, RequirePermission(Permissions.CustomersManage)] public async Task<ActionResult<CustomerItem>> Post(SaveCustomerRequest request, CancellationToken cancellationToken) => StatusCode(StatusCodes.Status201Created, await service.CreateAsync(request, cancellationToken));
    [HttpPut("{id:guid}"), RequirePermission(Permissions.CustomersManage)] public async Task<ActionResult<CustomerItem>> Put(Guid id, SaveCustomerRequest request, CancellationToken cancellationToken) => Ok(await service.UpdateAsync(id, request, cancellationToken));
}
