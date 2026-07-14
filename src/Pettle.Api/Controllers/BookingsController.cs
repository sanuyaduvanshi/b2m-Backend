using Microsoft.AspNetCore.Mvc;
using Pettle.Api.Authorization;
using Pettle.Application.Bookings;
using Pettle.Domain.Identity;

namespace Pettle.Api.Controllers;

[ApiController]
[Route("api/bookings")]
public class BookingsController : ControllerBase
{
    private readonly IBookingService _svc;
    public BookingsController(IBookingService svc) => _svc = svc;

    [HttpGet]
    [HasPermission(Modules.BookingRecords, Actions.View)]
    public async Task<IActionResult> List([FromQuery] BookingListQuery query, CancellationToken ct)
        => Ok(await _svc.ListAsync(query, ct));

    [HttpGet("{id:guid}")]
    [HasPermission(Modules.BookingRecords, Actions.View)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var r = await _svc.GetAsync(id, ct);
        return r is null ? NotFound() : Ok(r);
    }

    [HttpPost]
    [HasPermission(Modules.BookingRecords, Actions.Create)]
    public async Task<IActionResult> Create([FromBody] CreateBookingRequest req, CancellationToken ct)
    {
        var r = await _svc.CreateAsync(req, ct);
        return CreatedAtAction(nameof(Get), new { id = r.Id }, r);
    }

    [HttpPost("services/{serviceId:guid}/status")]
    [HasPermission(Modules.BookingRecords, Actions.Edit)]
    public async Task<IActionResult> ChangeStatus(Guid serviceId, [FromBody] BookingStateChangeRequest req, CancellationToken ct)
        => await _svc.ChangeStatusAsync(serviceId, req, ct) ? NoContent() : NotFound();

    [HttpPost("{id:guid}/cancel")]
    [HasPermission(Modules.BookingRecords, Actions.Edit)]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] CancelRequest req, CancellationToken ct)
        => await _svc.CancelAsync(id, req.Reason, ct) ? NoContent() : NotFound();

    [HttpPut("{id:guid}/discount")]
    [HasPermission(Modules.BookingRecords, Actions.Edit)]
    public async Task<IActionResult> ApplyDiscount(Guid id, [FromBody] ApplyDiscountRequest req, CancellationToken ct)
        => await _svc.ApplyDiscountAsync(id, req.DiscountPercent, ct) ? NoContent() : NotFound();

    [HttpPut("{id:guid}/estimate")]
    [HasPermission(Modules.BookingRecords, Actions.Edit)]
    public async Task<IActionResult> SaveEstimate(Guid id, [FromBody] SaveEstimateRequest req, CancellationToken ct)
    {
        var r = await _svc.SaveEstimateAsync(id, req, ct);
        return r is null ? NotFound() : Ok(r);
    }

    [HttpPost("{id:guid}/change-requests")]
    [HasPermission(Modules.BookingRecords, Actions.Edit)]
    public async Task<IActionResult> AddChangeRequest(Guid id, [FromBody] CreateChangeRequestRequest req, CancellationToken ct)
    {
        var r = await _svc.AddChangeRequestAsync(id, req, ct);
        return r is null ? NotFound() : Ok(r);
    }

    [HttpPost("{id:guid}/change-requests/{changeRequestId:guid}/resolve")]
    [HasPermission(Modules.BookingRecords, Actions.Edit)]
    public async Task<IActionResult> ResolveChangeRequest(Guid id, Guid changeRequestId, [FromBody] ResolveChangeRequestRequest req, CancellationToken ct)
    {
        var r = await _svc.ResolveChangeRequestAsync(id, changeRequestId, req, ct);
        return r is null ? NotFound() : Ok(r);
    }
}

public record CancelRequest(string? Reason);
public record ApplyDiscountRequest(decimal DiscountPercent);
