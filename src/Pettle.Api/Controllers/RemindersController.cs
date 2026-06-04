using Microsoft.AspNetCore.Mvc;
using Pettle.Api.Authorization;
using Pettle.Application.Reminders;
using Pettle.Domain.Identity;
using Pettle.Domain.Reminders;

namespace Pettle.Api.Controllers;

[ApiController]
[Route("api/reminders")]
public class RemindersController : ControllerBase
{
    private readonly IReminderService _svc;
    public RemindersController(IReminderService svc) => _svc = svc;

    [HttpGet]
    [HasPermission(Modules.Reminders, Actions.View)]
    public async Task<IActionResult> List([FromQuery] ReminderType? type, [FromQuery] ReminderStatus? status,
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
        => Ok(await _svc.ListAsync(type, status, from, to, page, pageSize, ct));

    [HttpGet("overview")]
    [HasPermission(Modules.Reminders, Actions.View)]
    public async Task<IActionResult> Overview([FromQuery] int? year, [FromQuery] int? month, CancellationToken ct)
        => Ok(await _svc.OverviewAsync(year ?? DateTime.UtcNow.Year, month ?? DateTime.UtcNow.Month, ct));

    [HttpPost]
    [HasPermission(Modules.Reminders, Actions.Create)]
    public async Task<IActionResult> Create([FromBody] CreateReminderRequest req, CancellationToken ct)
        => Ok(await _svc.CreateAsync(req, ct));

    [HttpPost("{id:guid}/snooze")]
    [HasPermission(Modules.Reminders, Actions.Edit)]
    public async Task<IActionResult> Snooze(Guid id, [FromBody] SnoozeRequest req, CancellationToken ct)
        => await _svc.SnoozeAsync(id, req, ct) ? NoContent() : NotFound();

    [HttpPost("{id:guid}/dismiss")]
    [HasPermission(Modules.Reminders, Actions.Edit)]
    public async Task<IActionResult> Dismiss(Guid id, CancellationToken ct)
        => await _svc.DismissAsync(id, ct) ? NoContent() : NotFound();

    [HttpPost("{id:guid}/sent")]
    [HasPermission(Modules.Reminders, Actions.Edit)]
    public async Task<IActionResult> MarkSent(Guid id, [FromBody] MarkSentRequest req, CancellationToken ct)
        => await _svc.MarkSentAsync(id, req.Via, ct) ? NoContent() : NotFound();

    [HttpGet("feed")]
    [HasPermission(Modules.Reminders, Actions.View)]
    public async Task<IActionResult> Feed([FromQuery] string type, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var f = from ?? today;
        var t = to   ?? today.AddDays(60);
        return Ok(await _svc.FeedAsync(type, f, t, ct));
    }

    [HttpGet("calendar")]
    [HasPermission(Modules.Reminders, Actions.View)]
    public async Task<IActionResult> Calendar([FromQuery] int? year, [FromQuery] int? month, CancellationToken ct)
        => Ok(await _svc.CalendarAsync(year ?? DateTime.UtcNow.Year, month ?? DateTime.UtcNow.Month, ct));
}

public record MarkSentRequest(string Via);
