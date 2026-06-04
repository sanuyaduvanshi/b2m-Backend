using Microsoft.AspNetCore.Mvc;
using Pettle.Api.Authorization;
using Pettle.Application.BookingRequests;
using Pettle.Domain.Identity;

namespace Pettle.Api.Controllers;

[ApiController]
[Route("api/booking-requests")]
public class BookingRequestsController : ControllerBase
{
    private readonly IBookingRequestService _svc;
    public BookingRequestsController(IBookingRequestService svc) => _svc = svc;

    [HttpGet]
    [HasPermission(Modules.BookingRequests, Actions.View)]
    public async Task<IActionResult> Board(
        [FromQuery] string? tab,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
        => Ok(await _svc.ListAsync(tab, search, page, pageSize, ct));

    [HttpPost]
    [HasPermission(Modules.BookingRequests, Actions.Create)]
    public async Task<IActionResult> Create([FromBody] CreateBookingRequestRequest req, CancellationToken ct)
        => Ok(await _svc.CreateAsync(req, ct));

    [HttpPost("{id:guid}/approve")]
    [HasPermission(Modules.BookingRequests, Actions.Approve)]
    public async Task<IActionResult> Approve(Guid id, CancellationToken ct)
        => await _svc.ApproveAsync(id, ct) ? NoContent() : NotFound();

    [HttpPost("{id:guid}/reject")]
    [HasPermission(Modules.BookingRequests, Actions.Approve)]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectBookingRequestRequest req, CancellationToken ct)
        => await _svc.RejectAsync(id, req, ct) ? NoContent() : NotFound();

    [HttpPost("{id:guid}/converted")]
    [HasPermission(Modules.BookingRequests, Actions.Edit)]
    public async Task<IActionResult> MarkConverted(Guid id, [FromBody] LinkBookingRequest req, CancellationToken ct)
        => await _svc.MarkConvertedAsync(id, req.BookingId, ct) ? NoContent() : NotFound();
}
