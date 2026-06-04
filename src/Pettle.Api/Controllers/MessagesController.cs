using Microsoft.AspNetCore.Mvc;
using Pettle.Api.Authorization;
using Pettle.Application.Messages;
using Pettle.Domain.Identity;

namespace Pettle.Api.Controllers;

[ApiController]
[Route("api/messages")]
public class MessagesController : ControllerBase
{
    private readonly IMessageService _svc;
    public MessagesController(IMessageService svc) => _svc = svc;

    [HttpGet("conversations")]
    [HasPermission(Modules.Messages, Actions.View)]
    public async Task<IActionResult> Conversations(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
        => Ok(await _svc.ListConversationsAsync(search, page, pageSize, ct));

    [HttpGet("thread/{petParentId:guid}")]
    [HasPermission(Modules.Messages, Actions.View)]
    public async Task<IActionResult> Thread(Guid petParentId, [FromQuery] int messageLimit = 200, CancellationToken ct = default)
    {
        var view = await _svc.GetThreadAsync(petParentId, messageLimit, ct);
        return view is null ? NotFound() : Ok(view);
    }

    [HttpPost("send")]
    [HasPermission(Modules.Messages, Actions.Create)]
    public async Task<IActionResult> Send([FromBody] SendMessageRequest req, CancellationToken ct)
        => Ok(await _svc.SendAsync(req, ct));

    [HttpPost("conversations/{conversationId:guid}/read")]
    [HasPermission(Modules.Messages, Actions.Edit)]
    public async Task<IActionResult> MarkRead(Guid conversationId, CancellationToken ct)
        => await _svc.MarkReadAsync(conversationId, ct) ? NoContent() : NotFound();
}
