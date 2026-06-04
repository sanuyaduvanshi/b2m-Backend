using Microsoft.AspNetCore.Mvc;
using Pettle.Api.Authorization;
using Pettle.Application.Common;
using Pettle.Application.Reports;
using Pettle.Domain.Identity;

namespace Pettle.Api.Controllers;

[ApiController]
[Route("api/reports")]
public class ReportsController : ControllerBase
{
    private readonly IReportsService _svc;
    public ReportsController(IReportsService svc) => _svc = svc;

    private static DateRange ResolveRange(DateOnly? from, DateOnly? to)
    {
        var (f, t) = DateRangeGuard.Resolve(from, to);
        return new DateRange(f, t);
    }

    [HttpGet("overview")] [HasPermission(Modules.Reports, Actions.View)]
    public async Task<IActionResult> Overview([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct)
        => Ok(await _svc.OverviewAsync(ResolveRange(from, to), ct));

    [HttpGet("revenue")] [HasPermission(Modules.Reports, Actions.View)]
    public async Task<IActionResult> Revenue([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct)
        => Ok(await _svc.RevenueAsync(ResolveRange(from, to), ct));

    [HttpGet("bookings")] [HasPermission(Modules.Reports, Actions.View)]
    public async Task<IActionResult> Bookings([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct)
        => Ok(await _svc.BookingsAsync(ResolveRange(from, to), ct));

    [HttpGet("clients")] [HasPermission(Modules.Reports, Actions.View)]
    public async Task<IActionResult> Clients([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct)
        => Ok(await _svc.ClientsAsync(ResolveRange(from, to), ct));

    [HttpGet("inventory")] [HasPermission(Modules.Reports, Actions.View)]
    public async Task<IActionResult> Inventory(CancellationToken ct) => Ok(await _svc.InventoryAsync(ct));
}
