using System.Text;
using Microsoft.AspNetCore.Mvc;
using Pettle.Api.Authorization;
using Pettle.Application.DailyTasks;
using Pettle.Domain.Identity;

namespace Pettle.Api.Controllers;

[ApiController]
[Route("api/daily-tasks")]
public class DailyTasksController : ControllerBase
{
    private readonly IDailyTaskService _svc;
    public DailyTasksController(IDailyTaskService svc) => _svc = svc;

    [HttpGet]
    [HasPermission(Modules.DailyTasks, Actions.View)]
    public async Task<IActionResult> Board(
        [FromQuery] DateOnly? date,
        [FromQuery] string? serviceType,
        [FromQuery] string? status,
        CancellationToken ct)
    {
        var d = date ?? DateOnly.FromDateTime(DateTime.UtcNow);
        return Ok(await _svc.BoardAsync(d, serviceType, status, ct));
    }

    [HttpPost("{entryId:guid}/status")]
    [HasPermission(Modules.DailyTasks, Actions.Edit)]
    public async Task<IActionResult> UpdateStatus(Guid entryId, [FromBody] UpdateDailyTaskStatusRequest req, CancellationToken ct)
        => await _svc.UpdateStatusAsync(entryId, req, ct) ? NoContent() : NotFound();

    [HttpGet("export")]
    [HasPermission(Modules.DailyTasks, Actions.Export)]
    public async Task<IActionResult> Export([FromQuery] DateOnly? date, [FromQuery] string? serviceType, CancellationToken ct)
    {
        var d = date ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var csv = await _svc.ExportCsvAsync(d, serviceType, ct);
        return File(Encoding.UTF8.GetBytes(csv), "text/csv", $"daily-tasks-{d:yyyy-MM-dd}.csv");
    }
}
