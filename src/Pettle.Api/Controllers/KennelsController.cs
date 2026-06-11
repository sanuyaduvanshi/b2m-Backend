using Microsoft.AspNetCore.Mvc;
using Pettle.Api.Authorization;
using Pettle.Application.Kennels;
using Pettle.Domain.Identity;

namespace Pettle.Api.Controllers;

[ApiController]
[Route("api/kennels")]
public class KennelsController : ControllerBase
{
    private readonly IKennelService _svc;
    public KennelsController(IKennelService svc) => _svc = svc;

    [HttpGet]
    [HasPermission(Modules.Kennels, Actions.View)]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await _svc.ListAsync(ct));

    [HttpPost]
    [HasPermission(Modules.Kennels, Actions.Create)]
    public async Task<IActionResult> Create([FromBody] CreateOrUpdateKennelRequest req, CancellationToken ct)
        => Ok(await _svc.CreateAsync(req, ct));

    [HttpPut("{id:guid}")]
    [HasPermission(Modules.Kennels, Actions.Edit)]
    public async Task<IActionResult> Update(Guid id, [FromBody] CreateOrUpdateKennelRequest req, CancellationToken ct)
    {
        var r = await _svc.UpdateAsync(id, req, ct);
        return r is null ? NotFound() : Ok(r);
    }

    [HttpDelete("{id:guid}")]
    [HasPermission(Modules.Kennels, Actions.Delete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => await _svc.DeleteAsync(id, ct) ? NoContent() : NotFound();

    [HttpGet("live")]
    [HasPermission(Modules.Kennels, Actions.View)]
    public async Task<IActionResult> Live([FromQuery] DateOnly? date, CancellationToken ct)
        => Ok(await _svc.LiveGridAsync(date ?? DateOnly.FromDateTime(DateTime.UtcNow), ct));

    [HttpGet("timeline")]
    [HasPermission(Modules.Kennels, Actions.View)]
    public async Task<IActionResult> Timeline([FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken ct)
        => Ok(await _svc.TimelineAsync(from, to, ct));

    [HttpPost("{id:guid}/block")]
    [HasPermission(Modules.Kennels, Actions.Edit)]
    public async Task<IActionResult> Block(Guid id, [FromBody] KennelBlockRequest req, CancellationToken ct)
        => await _svc.BlockAsync(id, req, ct) ? NoContent() : NotFound();

    [HttpDelete("blockings/{blockingId:guid}")]
    [HasPermission(Modules.Kennels, Actions.Edit)]
    public async Task<IActionResult> Unblock(Guid blockingId, CancellationToken ct)
        => await _svc.UnblockAsync(blockingId, ct) ? NoContent() : NotFound();
}
