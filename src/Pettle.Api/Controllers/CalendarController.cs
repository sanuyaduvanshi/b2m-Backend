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

    // ---- Manual appointments ----
    [HttpGet("appointments")]
    [HasPermission(Modules.Calendar, Actions.View)]
    public async Task<IActionResult> Appointments([FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken ct)
        => Ok(await _svc.ListAppointmentsAsync(from, to, ct));

    [HttpPost("appointments")]
    [HasPermission(Modules.Calendar, Actions.Edit)]
    public async Task<IActionResult> CreateAppointment([FromBody] CreateOrUpdateAppointmentRequest req, CancellationToken ct)
        => Ok(await _svc.CreateAppointmentAsync(req, ct));

    [HttpPut("appointments/{id:guid}")]
    [HasPermission(Modules.Calendar, Actions.Edit)]
    public async Task<IActionResult> UpdateAppointment(Guid id, [FromBody] CreateOrUpdateAppointmentRequest req, CancellationToken ct)
    {
        var r = await _svc.UpdateAppointmentAsync(id, req, ct);
        return r is null ? NotFound() : Ok(r);
    }

    [HttpDelete("appointments/{id:guid}")]
    [HasPermission(Modules.Calendar, Actions.Edit)]
    public async Task<IActionResult> DeleteAppointment(Guid id, CancellationToken ct)
        => await _svc.DeleteAppointmentAsync(id, ct) ? NoContent() : NotFound();
}
