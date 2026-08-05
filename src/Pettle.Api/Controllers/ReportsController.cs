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
    public async Task<IActionResult> Clients([FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        [FromQuery] bool allTime = false, CancellationToken ct = default)
        // Omitted dates default to the last 30 days, so "all time" has to say so explicitly —
        // sending blank from/to would quietly give a month's figure under an "All Time" label.
        => Ok(await _svc.ClientsAsync(allTime ? DateRange.AllTime : ResolveRange(from, to), ct));

    [HttpGet("inventory")] [HasPermission(Modules.Reports, Actions.View)]
    public async Task<IActionResult> Inventory(CancellationToken ct) => Ok(await _svc.InventoryAsync(ct));

    [HttpGet("expenses")] [HasPermission(Modules.Reports, Actions.View)]
    public async Task<IActionResult> Expenses([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct)
        => Ok(await _svc.ExpensesAsync(ResolveRange(from, to), ct));

    [HttpGet("profit")] [HasPermission(Modules.Reports, Actions.View)]
    public async Task<IActionResult> Profit([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct)
        => Ok(await _svc.ProfitAsync(ResolveRange(from, to), ct));

    [HttpGet("monthly")] [HasPermission(Modules.Reports, Actions.View)]
    public async Task<IActionResult> Monthly([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct)
        => Ok(await _svc.MonthlyAsync(ResolveRange(from, to), ct));

    [HttpGet("period-summary")] [HasPermission(Modules.Reports, Actions.View)]
    public async Task<IActionResult> PeriodSummary([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct)
        => Ok(await _svc.PeriodSummaryAsync(ResolveRange(from, to), ct));
}
