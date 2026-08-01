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

    [HttpDelete("packages/{id:guid}")]
    [HasPermission(Modules.Subscriptions, Actions.Delete)]
    public async Task<IActionResult> DeletePackage(Guid id, CancellationToken ct)
        => await _svc.DeletePackageAsync(id, ct) ? NoContent() : NotFound();

    [HttpGet("issued")]
    [HasPermission(Modules.Subscriptions, Actions.View)]
    public async Task<IActionResult> Issued([FromQuery] string? search, [FromQuery] IssuedSubscriptionStatus? status,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
        => Ok(await _svc.ListIssuedAsync(search, status, page, pageSize, ct));

    [HttpGet("issued/status-summary")]
    [HasPermission(Modules.Subscriptions, Actions.View)]
    public async Task<IActionResult> StatusSummary(CancellationToken ct)
        => Ok(await _svc.StatusSummaryAsync(ct));

    [HttpGet("issued/export")]
    [HasPermission(Modules.Subscriptions, Actions.Export)]
    public async Task<IActionResult> ExportIssued([FromQuery] string? search, [FromQuery] IssuedSubscriptionStatus? status,
        [FromQuery] Guid? packageId, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct = default)
        => Ok(await _svc.ExportIssuedAsync(search, status, packageId, from, to, ct));

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

    [HttpDelete("issued/{id:guid}")]
    [HasPermission(Modules.Subscriptions, Actions.Delete)]
    public async Task<IActionResult> DeleteIssued(Guid id, CancellationToken ct)
        => await _svc.DeleteIssuedAsync(id, ct) ? NoContent() : NotFound();

    [HttpGet("issued/{id:guid}")]
    [HasPermission(Modules.Subscriptions, Actions.View)]
    public async Task<IActionResult> IssuedDetail(Guid id, CancellationToken ct)
    {
        var r = await _svc.GetIssuedAsync(id, ct);
        return r is null ? NotFound() : Ok(r);
    }

    [HttpPost("issued/{id:guid}/payments")]
    [HasPermission(Modules.Subscriptions, Actions.Edit)]
    public async Task<IActionResult> RecordPayment(Guid id, [FromBody] RecordSubscriptionPaymentRequest req, CancellationToken ct)
    {
        var p = await _svc.RecordPaymentAsync(id, req, ct);
        return p is null ? NotFound() : Ok(p);
    }

    [HttpPut("issued/{id:guid}/payments/{paymentId:guid}")]
    [HasPermission(Modules.Subscriptions, Actions.Edit)]
    public async Task<IActionResult> UpdatePayment(Guid id, Guid paymentId, [FromBody] RecordSubscriptionPaymentRequest req, CancellationToken ct)
    {
        var p = await _svc.UpdatePaymentAsync(id, paymentId, req, ct);
        return p is null ? NotFound() : Ok(p);
    }

    [HttpDelete("issued/{id:guid}/payments/{paymentId:guid}")]
    [HasPermission(Modules.Subscriptions, Actions.Edit)]
    public async Task<IActionResult> DeletePayment(Guid id, Guid paymentId, CancellationToken ct)
        => await _svc.DeletePaymentAsync(id, paymentId, ct) ? NoContent() : NotFound();

    [HttpGet("issued/active-for-client/{petParentId:guid}")]
    [HasPermission(Modules.Subscriptions, Actions.View)]
    public async Task<IActionResult> ActiveForClient(Guid petParentId, [FromQuery] string? type, CancellationToken ct)
    {
        var r = await _svc.GetActiveByClientAsync(petParentId, type, ct);
        return r is null ? NoContent() : Ok(r);
    }

    [HttpGet("issued/active-for-client/{petParentId:guid}/all")]
    [HasPermission(Modules.Subscriptions, Actions.View)]
    public async Task<IActionResult> ActiveSubscriptionsForClient(Guid petParentId, [FromQuery] string? type, CancellationToken ct)
        => Ok(await _svc.GetActiveSubscriptionsByClientAsync(petParentId, type, ct));
}
