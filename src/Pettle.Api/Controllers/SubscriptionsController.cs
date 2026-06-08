using Microsoft.AspNetCore.Mvc;
using Pettle.Api.Authorization;
using Pettle.Application.Subscriptions;
using Pettle.Domain.Identity;
using Pettle.Domain.Subscriptions;

namespace Pettle.Api.Controllers;

[ApiController]
[Route("api/subscriptions")]
public class SubscriptionsController : ControllerBase
{
    private readonly ISubscriptionService _svc;
    public SubscriptionsController(ISubscriptionService svc) => _svc = svc;

    [HttpGet("packages")]
    [HasPermission(Modules.Subscriptions, Actions.View)]
    public async Task<IActionResult> Packages(CancellationToken ct) => Ok(await _svc.ListPackagesAsync(ct));

    [HttpPost("packages")]
    [HasPermission(Modules.Subscriptions, Actions.Create)]
    public async Task<IActionResult> CreatePackage([FromBody] CreateOrUpdatePackageRequest req, CancellationToken ct)
        => Ok(await _svc.CreatePackageAsync(req, ct));

    [HttpPut("packages/{id:guid}")]
    [HasPermission(Modules.Subscriptions, Actions.Edit)]
    public async Task<IActionResult> UpdatePackage(Guid id, [FromBody] CreateOrUpdatePackageRequest req, CancellationToken ct)
    {
        var r = await _svc.UpdatePackageAsync(id, req, ct);
        return r is null ? NotFound() : Ok(r);
    }

    [HttpGet("issued")]
    [HasPermission(Modules.Subscriptions, Actions.View)]
    public async Task<IActionResult> Issued([FromQuery] string? search, [FromQuery] IssuedSubscriptionStatus? status,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
        => Ok(await _svc.ListIssuedAsync(search, status, page, pageSize, ct));

    [HttpPost("issued")]
    [HasPermission(Modules.Subscriptions, Actions.Create)]
    public async Task<IActionResult> Issue([FromBody] IssueSubscriptionRequest req, CancellationToken ct)
        => Ok(await _svc.IssueAsync(req, ct));

    [HttpPost("issued/{id:guid}/freeze")]
    [HasPermission(Modules.Subscriptions, Actions.Edit)]
    public async Task<IActionResult> Freeze(Guid id, CancellationToken ct)
        => await _svc.FreezeAsync(id, ct) ? NoContent() : NotFound();

    [HttpPost("issued/{id:guid}/cancel")]
    [HasPermission(Modules.Subscriptions, Actions.Edit)]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
        => await _svc.CancelAsync(id, ct) ? NoContent() : NotFound();
}
