using Microsoft.AspNetCore.Mvc;
using Pettle.Api.Authorization;
using Pettle.Application.Messages;
using Pettle.Domain.Identity;
using Pettle.Domain.Messages;

namespace Pettle.Api.Controllers;

[ApiController]
[Route("api/message-templates")]
public class MessageTemplatesController : ControllerBase
{
    private readonly IMessageTemplateService _svc;
    public MessageTemplatesController(IMessageTemplateService svc) => _svc = svc;

    [HttpGet]
    [HasPermission(Modules.Messages, Actions.View)]
    public async Task<IActionResult> List(
        [FromQuery] string? search,
        [FromQuery] MessageChannel? channel,
        [FromQuery] MessageTemplateCategory? category,
        [FromQuery] bool activeOnly = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
        => Ok(await _svc.ListAsync(search, channel, category, activeOnly, page, pageSize, ct));

    [HttpGet("{id:guid}")]
    [HasPermission(Modules.Messages, Actions.View)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var row = await _svc.GetAsync(id, ct);
        return row is null ? NotFound() : Ok(row);
    }

    [HttpPost]
    [HasPermission(Modules.Messages, Actions.Create)]
    public async Task<IActionResult> Create([FromBody] CreateMessageTemplateRequest req, CancellationToken ct)
        => Ok(await _svc.CreateAsync(req, ct));

    [HttpPut("{id:guid}")]
    [HasPermission(Modules.Messages, Actions.Edit)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateMessageTemplateRequest req, CancellationToken ct)
    {
        var row = await _svc.UpdateAsync(id, req, ct);
        return row is null ? NotFound() : Ok(row);
    }

    [HttpDelete("{id:guid}")]
    [HasPermission(Modules.Messages, Actions.Delete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => await _svc.DeleteAsync(id, ct) ? NoContent() : NotFound();
}
