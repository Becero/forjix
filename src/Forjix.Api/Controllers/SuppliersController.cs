using Forjix.Api.Authorization;
using Forjix.Application.Common;
using Forjix.Application.Features.Purchases;
using Microsoft.AspNetCore.Mvc;
namespace Forjix.Api.Controllers;

[ApiController, Route("api/suppliers")] public sealed class SuppliersController(IPurchaseService service) : ControllerBase { [HttpGet, RequirePermission(Permissions.SuppliersView)] public async Task<ActionResult<PagedSuppliers>> Get(string? search, bool? isActive, int page = 1, int pageSize = 20, CancellationToken ct = default) => Ok(await service.GetSuppliersAsync(search, isActive, page, pageSize, ct)); [HttpPost, RequirePermission(Permissions.SuppliersManage)] public async Task<ActionResult<SupplierItem>> Post(SaveSupplierRequest request, CancellationToken ct) => StatusCode(201, await service.CreateSupplierAsync(request, ct)); [HttpPut("{id:guid}"), RequirePermission(Permissions.SuppliersManage)] public async Task<ActionResult<SupplierItem>> Put(Guid id, SaveSupplierRequest request, CancellationToken ct) => Ok(await service.UpdateSupplierAsync(id, request, ct)); }
