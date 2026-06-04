using Microsoft.AspNetCore.Mvc;
using Pettle.Api.Authorization;
using Pettle.Application.Expenses;
using Pettle.Domain.Identity;

namespace Pettle.Api.Controllers;

[ApiController]
[Route("api/expenses")]
public class ExpensesController : ControllerBase
{
    private readonly IExpenseService _svc;
    public ExpensesController(IExpenseService svc) => _svc = svc;

    [HttpGet]
    [HasPermission(Modules.Expenses, Actions.View)]
    public async Task<IActionResult> List([FromQuery] string? search, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
        => Ok(await _svc.ListAsync(search, from, to, page, pageSize, ct));

    [HttpPost]
    [HasPermission(Modules.Expenses, Actions.Create)]
    public async Task<IActionResult> Create([FromBody] CreateOrUpdateExpenseRequest req, CancellationToken ct)
        => Ok(await _svc.CreateAsync(req, ct));

    [HttpPut("{id:guid}")]
    [HasPermission(Modules.Expenses, Actions.Edit)]
    public async Task<IActionResult> Update(Guid id, [FromBody] CreateOrUpdateExpenseRequest req, CancellationToken ct)
    {
        var r = await _svc.UpdateAsync(id, req, ct);
        return r is null ? NotFound() : Ok(r);
    }

    [HttpDelete("{id:guid}")]
    [HasPermission(Modules.Expenses, Actions.Delete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => await _svc.DeleteAsync(id, ct) ? NoContent() : NotFound();

    [HttpGet("categories")]
    [HasPermission(Modules.Expenses, Actions.View)]
    public async Task<IActionResult> Categories(CancellationToken ct) => Ok(await _svc.ListCategoriesAsync(ct));

    [HttpPost("categories")]
    [HasPermission(Modules.Expenses, Actions.Create)]
    public async Task<IActionResult> CreateCategory([FromBody] CreateCategoryRequest req, CancellationToken ct)
        => Ok(await _svc.CreateCategoryAsync(req.Name, ct));
}

public record CreateCategoryRequest(string Name);
