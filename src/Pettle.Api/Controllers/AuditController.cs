using Microsoft.AspNetCore.Mvc;
using Pettle.Api.Authorization;
using Pettle.Application.Audit;
using Pettle.Domain.Audit;
using Pettle.Domain.Identity;

namespace Pettle.Api.Controllers;

/// <summary>The activity log. Gated on AccessManagement — the module that exists precisely to mean
/// "can administer other people", which is exactly who should be able to read what they did. In
/// the seeded matrix that resolves to SystemAdmin and BusinessOwner only; BranchManager is
/// deliberately excluded from AccessManagement.* and so can't read the log either.</summary>
[ApiController]
[Route("api/audit")]
public class AuditController : ControllerBase
{
    private readonly IAuditService _svc;
    public AuditController(IAuditService svc) => _svc = svc;

    [HttpGet]
    [HasPermission(Modules.AccessManagement, Actions.View)]
    public async Task<IActionResult> List([FromQuery] string? search, [FromQuery] Guid? actorUserId,
        [FromQuery] string? role, [FromQuery] string? module, [FromQuery] AuditAction? action,
        [FromQuery] string? entityType, [FromQuery] string? entityId,
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
        => Ok(await _svc.ListAsync(new AuditLogQuery(search, actorUserId, role, module, action, entityType, entityId, from, to, page, pageSize), ct));

    /// <summary>Distinct actors/roles/modules/actions present in the log — the filter dropdowns
    /// are built from this so they never offer a choice that matches nothing.</summary>
    [HttpGet("filters")]
    [HasPermission(Modules.AccessManagement, Actions.View)]
    public async Task<IActionResult> Filters(CancellationToken ct)
        => Ok(await _svc.FilterOptionsAsync(ct));

    [HttpGet("{id:guid}")]
    [HasPermission(Modules.AccessManagement, Actions.View)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var r = await _svc.GetAsync(id, ct);
        return r is null ? NotFound() : Ok(r);
    }

    [HttpGet("export")]
    [HasPermission(Modules.AccessManagement, Actions.Export)]
    public async Task<IActionResult> Export([FromQuery] string? search, [FromQuery] Guid? actorUserId,
        [FromQuery] string? role, [FromQuery] string? module, [FromQuery] AuditAction? action,
        [FromQuery] string? entityType, [FromQuery] string? entityId,
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct = default)
        => Ok(await _svc.ExportAsync(new AuditLogQuery(search, actorUserId, role, module, action, entityType, entityId, from, to, 1, 10_000), ct));
}
