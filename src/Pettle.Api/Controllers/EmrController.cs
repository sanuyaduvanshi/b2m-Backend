using Microsoft.AspNetCore.Mvc;
using Pettle.Api.Authorization;
using Pettle.Application.Emr;
using Pettle.Domain.Identity;

namespace Pettle.Api.Controllers;

[ApiController]
[Route("api/emr")]
public class EmrController : ControllerBase
{
    private readonly IEmrService _svc;
    public EmrController(IEmrService svc) => _svc = svc;

    [HttpGet]
    [HasPermission(Modules.Emr, Actions.View)]
    public async Task<IActionResult> List([FromQuery] string? search, [FromQuery] Guid? petId,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default)
        => Ok(await _svc.ListAsync(search, petId, page, pageSize, ct));

    [HttpGet("{id:guid}")]
    [HasPermission(Modules.Emr, Actions.View)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var r = await _svc.GetAsync(id, ct);
        return r is null ? NotFound() : Ok(r);
    }

    [HttpPost]
    [HasPermission(Modules.Emr, Actions.Create)]
    public async Task<IActionResult> Create([FromBody] CreateOrUpdateEmrRequest req, CancellationToken ct)
    {
        var r = await _svc.CreateAsync(req, ct);
        return CreatedAtAction(nameof(Get), new { id = r.Id }, r);
    }

    [HttpPut("{id:guid}")]
    [HasPermission(Modules.Emr, Actions.Edit)]
    public async Task<IActionResult> Update(Guid id, [FromBody] CreateOrUpdateEmrRequest req, CancellationToken ct)
    {
        var r = await _svc.UpdateAsync(id, req, ct);
        return r is null ? NotFound() : Ok(r);
    }

    [HttpDelete("{id:guid}")]
    [HasPermission(Modules.Emr, Actions.Delete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => await _svc.DeleteAsync(id, ct) ? NoContent() : NotFound();

    [HttpGet("{id:guid}/pdf")]
    [HasPermission(Modules.Emr, Actions.View)]
    public async Task<IActionResult> GetPdf(Guid id, CancellationToken ct)
    {
        var detail = await _svc.GetAsync(id, ct);
        if (detail is null) return NotFound();
        var bytes = await _svc.GeneratePdfAsync(id, ct);
        if (bytes is null) return NotFound();
        return File(bytes, "application/pdf", $"Prescription-{detail.PetName}-{detail.VisitDate:yyyyMMdd}.pdf");
    }
}
