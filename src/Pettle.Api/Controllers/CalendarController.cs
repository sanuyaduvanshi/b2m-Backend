using Microsoft.AspNetCore.Mvc;
using Pettle.Api.Authorization;
using Pettle.Application.Calendar;
using Pettle.Domain.Identity;

namespace Pettle.Api.Controllers;

[ApiController]
[Route("api/calendar")]
public class CalendarController : ControllerBase
{
    private readonly ICalendarService _svc;
    public CalendarController(ICalendarService svc) => _svc = svc;

    [HttpGet("events")]
    [HasPermission(Modules.Calendar, Actions.View)]
    public async Task<IActionResult> Events(
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        [FromQuery] string? serviceType,
        [FromQuery] bool overnightOnly = false,
        CancellationToken ct = default)
        => Ok(await _svc.EventsAsync(from, to, serviceType, overnightOnly, ct));

    [HttpGet("counters")]
    [HasPermission(Modules.Calendar, Actions.View)]
    public async Task<IActionResult> Counters([FromQuery] DateOnly date, CancellationToken ct = default)
        => Ok(await _svc.CountersAsync(date, ct));

    [HttpPost("{bookingServiceId:guid}/reschedule")]
    [HasPermission(Modules.Calendar, Actions.Edit)]
    public async Task<IActionResult> Reschedule(Guid bookingServiceId, [FromBody] RescheduleRequest req, CancellationToken ct)
    {
        var ev = await _svc.RescheduleAsync(bookingServiceId, req, ct);
        return ev is null ? NotFound() : Ok(ev);
    }
}
