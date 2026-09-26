using Forjix.Api.Authorization;
using Forjix.Application.Common;
using Forjix.Application.Features.Financial;
using Microsoft.AspNetCore.Mvc;

namespace Forjix.Api.Controllers;

[ApiController, Route("api/financial/categories")]
public sealed class FinancialCategoriesController(IFinancialService service) : ControllerBase
{
    [HttpGet, RequirePermission(Permissions.FinancialCategoriesView)]
    public async Task<IActionResult> Get(string? type, CancellationToken ct) => Ok(await service.CategoriesAsync(type, ct));
    [HttpPost, RequirePermission(Permissions.FinancialCategoriesManage)]
    public async Task<IActionResult> Post(SaveFinancialCategoryRequest request, CancellationToken ct) => StatusCode(201, await service.SaveCategoryAsync(null, request, ct));
    [HttpPut("{id:guid}"), RequirePermission(Permissions.FinancialCategoriesManage)]
    public async Task<IActionResult> Put(Guid id, SaveFinancialCategoryRequest request, CancellationToken ct) => Ok(await service.SaveCategoryAsync(id, request, ct));
}
