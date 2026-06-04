using Microsoft.AspNetCore.Mvc;
using Pettle.Api.Authorization;
using Pettle.Application.ClientEnquiries;
using Pettle.Domain.Identity;

namespace Pettle.Api.Controllers;

[ApiController]
[Route("api/enquiries")]
public class ClientEnquiriesController : ControllerBase
{
    private readonly IClientEnquiryService _svc;
    public ClientEnquiriesController(IClientEnquiryService svc) => _svc = svc;

    [HttpGet]
    [HasPermission(Modules.ClientEnquiries, Actions.View)]
    public async Task<IActionResult> Board(
        [FromQuery] string? tab,
        [FromQuery] string? search,
        [FromQuery] string? source,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default)
        => Ok(await _svc.ListAsync(tab, search, source, page, pageSize, ct));

    [HttpGet("{id:guid}")]
    [HasPermission(Modules.ClientEnquiries, Actions.View)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var row = await _svc.GetAsync(id, ct);
        return row is null ? NotFound() : Ok(row);
    }

    [HttpPost]
    [HasPermission(Modules.ClientEnquiries, Actions.Create)]
    public async Task<IActionResult> Create([FromBody] CreateClientEnquiryRequest req, CancellationToken ct)
        => Ok(await _svc.CreateAsync(req, ct));

    [HttpPut("{id:guid}")]
    [HasPermission(Modules.ClientEnquiries, Actions.Edit)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateClientEnquiryRequest req, CancellationToken ct)
    {
        var row = await _svc.UpdateAsync(id, req, ct);
        return row is null ? NotFound() : Ok(row);
    }

    [HttpPost("{id:guid}/reject")]
    [HasPermission(Modules.ClientEnquiries, Actions.Edit)]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectClientEnquiryRequest req, CancellationToken ct)
        => await _svc.RejectAsync(id, req, ct) ? NoContent() : NotFound();

    [HttpPost("{id:guid}/convert")]
    [HasPermission(Modules.ClientEnquiries, Actions.Edit)]
    public async Task<IActionResult> Convert(Guid id, [FromBody] ConvertEnquiryRequest req, CancellationToken ct)
    {
        var res = await _svc.ConvertToClientAsync(id, req, ct);
        return res is null ? NotFound() : Ok(res);
    }

    [HttpDelete("{id:guid}")]
    [HasPermission(Modules.ClientEnquiries, Actions.Delete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => await _svc.DeleteAsync(id, ct) ? NoContent() : NotFound();
}
