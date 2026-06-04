using Microsoft.AspNetCore.Mvc;
using Pettle.Api.Authorization;
using Pettle.Application.Dashboard;
using Pettle.Domain.Identity;

namespace Pettle.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _svc;
    public DashboardController(IDashboardService svc) => _svc = svc;

    [HttpGet("overview")]
    [HasPermission(Modules.Dashboard, Actions.View)]
    public async Task<IActionResult> Overview(CancellationToken ct) => Ok(await _svc.OverviewAsync(ct));
}
