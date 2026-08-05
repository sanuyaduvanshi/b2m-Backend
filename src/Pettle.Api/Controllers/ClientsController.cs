using Microsoft.AspNetCore.Mvc;
using Pettle.Api.Authorization;
using Pettle.Application.Clients;
using Pettle.Domain.Clients;
using Pettle.Domain.Identity;
using Pettle.Infrastructure.Clients;

namespace Pettle.Api.Controllers;

[ApiController]
[Route("api/clients")]
public class ClientsController : ControllerBase
{
    private readonly IClientService _service;
    public ClientsController(IClientService service) => _service = service;

    [HttpGet]
    [HasPermission(Modules.ClientDatabase, Actions.View)]
    public async Task<IActionResult> List([FromQuery] ClientListQuery query, CancellationToken ct)
        => Ok(await _service.ListAsync(query, ct));

    [HttpGet("export")]
    [HasPermission(Modules.ClientDatabase, Actions.Export)]
    public async Task<IActionResult> Export([FromQuery] ClientListQuery query, CancellationToken ct)
        => Ok(await _service.ExportAsync(query, ct));

    [HttpGet("export.xlsx")]
    [HasPermission(Modules.ClientDatabase, Actions.Export)]
    public async Task<IActionResult> ExportWorkbook([FromQuery] ClientListQuery query, CancellationToken ct)
        => File(await _service.ExportWorkbookAsync(query, ct),
                ClientWorkbookBuilder.ContentType, ClientWorkbookBuilder.FileName());

    [HttpGet("{id:guid}")]
    [HasPermission(Modules.ClientDatabase, Actions.View)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var r = await _service.GetAsync(id, ct);
        return r is null ? NotFound() : Ok(r);
    }

    [HttpPost]
    [HasPermission(Modules.ClientDatabase, Actions.Create)]
    public async Task<IActionResult> Create([FromBody] CreatePetParentRequest req, CancellationToken ct)
    {
        var r = await _service.CreateAsync(req, ct);
        return CreatedAtAction(nameof(Get), new { id = r.Id }, r);
    }

    [HttpPut("{id:guid}")]
    [HasPermission(Modules.ClientDatabase, Actions.Edit)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdatePetParentRequest req, CancellationToken ct)
    {
        var r = await _service.UpdateAsync(id, req, ct);
        return r is null ? NotFound() : Ok(r);
    }

    [HttpPost("{id:guid}/archive")]
    [HasPermission(Modules.ClientDatabase, Actions.Edit)]
    public async Task<IActionResult> Archive(Guid id, [FromBody] ArchiveRequest req, CancellationToken ct)
        => await _service.ArchiveAsync(id, req.Reason, ct) ? NoContent() : NotFound();

    [HttpDelete("{id:guid}")]
    [HasPermission(Modules.ClientDatabase, Actions.Delete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => await _service.DeleteAsync(id, ct) ? NoContent() : NotFound();

    // --- Pet endpoints ---

    [HttpPost("{id:guid}/pets")]
    [HasPermission(Modules.ClientDatabase, Actions.Edit)]
    public async Task<IActionResult> AddPet(Guid id, [FromBody] CreatePetRequest req, CancellationToken ct)
    {
        var r = await _service.AddPetAsync(id, req, ct);
        return r is null ? NotFound() : Ok(r);
    }

    [HttpPut("{id:guid}/pets/{petId:guid}")]
    [HasPermission(Modules.ClientDatabase, Actions.Edit)]
    public async Task<IActionResult> UpdatePet(Guid id, Guid petId, [FromBody] UpdatePetRequest req, CancellationToken ct)
    {
        var r = await _service.UpdatePetAsync(id, petId, req, ct);
        return r is null ? NotFound() : Ok(r);
    }

    [HttpDelete("{id:guid}/pets/{petId:guid}")]
    [HasPermission(Modules.ClientDatabase, Actions.Edit)]
    public async Task<IActionResult> DeletePet(Guid id, Guid petId, CancellationToken ct)
        => await _service.DeletePetAsync(id, petId, ct) ? NoContent() : NotFound();
}

public record ArchiveRequest(string Reason);
