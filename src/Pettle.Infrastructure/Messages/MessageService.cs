using Microsoft.EntityFrameworkCore;
using Pettle.Application.Clients;
using Pettle.Application.Common;
using Pettle.Application.Common.Errors;
using Pettle.Application.Messages;
using Pettle.Domain.Messages;
using Pettle.Infrastructure.Persistence;

namespace Pettle.Infrastructure.Messages;

/// <summary>
/// Conversation + outbound-message log. Real WhatsApp/SMS/Email delivery is gated on
/// stakeholder vendor selection (FRD §6 / Questionnaire slide 23) — until then, sends are
/// persisted with Status=Sent as an auditable record of what staff chose to communicate.
/// When a BSP is wired, replace the mock-send block in <see cref="SendAsync"/> with the real
/// provider call and have the webhook update Status/DeliveredAt/ReadAt + ExternalMessageId.
/// </summary>
public class MessageService : IMessageService
{
    private const int PreviewLength = 140;

    private readonly PettleDbContext _db;
    private readonly ICurrentUser _user;

    public MessageService(PettleDbContext db, ICurrentUser user) { _db = db; _user = user; }

    public async Task<PagedResult<ConversationRow>> ListConversationsAsync(string? search, int page, int pageSize, CancellationToken ct = default)
    {
        if (_user.TenantId is null)
            return new PagedResult<ConversationRow>(Array.Empty<ConversationRow>(), 0, page, pageSize);

        var q =
            from c in _db.Conversations.AsNoTracking()
            join p in _db.PetParents.AsNoTracking() on c.PetParentId equals p.Id
            where c.TenantId == _user.TenantId
            select new { c, p };

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            q = q.Where(x => x.p.Name.ToLower().Contains(s) || x.p.Phone.Contains(s)
                          || (x.p.Email != null && x.p.Email.ToLower().Contains(s)));
        }

        var total = await q.CountAsync(ct);
        var p2 = Math.Max(page, 1);
        var sz = Math.Clamp(pageSize, 1, 200);

        var rows = await q
            .OrderByDescending(x => x.c.LastMessageAt ?? x.c.CreatedAt)
            .Skip((p2 - 1) * sz).Take(sz)
            .Select(x => new ConversationRow(
                x.c.Id, x.p.Id, x.p.Name, x.p.Phone, x.p.Email,
                x.c.LastMessageAt, x.c.LastMessagePreview, x.c.LastChannel, x.c.LastDirection,
                x.c.UnreadCount))
            .ToListAsync(ct);

        return new PagedResult<ConversationRow>(rows, total, p2, sz);
    }

    public async Task<ThreadView?> GetThreadAsync(Guid petParentId, int messageLimit, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return null;

        var parent = await _db.PetParents.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == petParentId && p.TenantId == _user.TenantId, ct);
        if (parent is null) return null;

        var convo = await _db.Conversations
            .FirstOrDefaultAsync(c => c.PetParentId == petParentId && c.TenantId == _user.TenantId, ct);

        // Empty thread for a parent who has never been messaged — return a virtual conversation
        // so the UI can immediately compose without needing a separate "create thread" call.
        if (convo is null)
        {
            return new ThreadView(
                new ConversationRow(Guid.Empty, parent.Id, parent.Name, parent.Phone, parent.Email,
                    null, null, null, null, 0),
                Array.Empty<MessageRow>());
        }

        var take = Math.Clamp(messageLimit <= 0 ? 200 : messageLimit, 1, 500);

        var messages = await _db.Messages.AsNoTracking()
            .Where(m => m.ConversationId == convo.Id)
            .OrderBy(m => m.CreatedAt)
            .Take(take)
            .Select(m => new MessageRow(
                m.Id, m.ConversationId, m.Direction, m.Channel,
                m.Subject, m.Body, m.TemplateId,
                m.Template != null ? m.Template.Name : null,
                m.Status, m.FailureReason,
                m.SentAt, m.DeliveredAt, m.ReadAt,
                m.SentByName, m.ExternalMessageId, m.CreatedAt))
            .ToListAsync(ct);

        return new ThreadView(
            new ConversationRow(convo.Id, parent.Id, parent.Name, parent.Phone, parent.Email,
                convo.LastMessageAt, convo.LastMessagePreview, convo.LastChannel, convo.LastDirection,
                convo.UnreadCount),
            messages);
    }

    public async Task<MessageRow> SendAsync(SendMessageRequest req, CancellationToken ct = default)
    {
        if (_user.TenantId is null) throw AppException.Forbidden();
        var tid = _user.TenantId.Value;

        var parent = await _db.PetParents
            .FirstOrDefaultAsync(p => p.Id == req.PetParentId && p.TenantId == tid, ct)
            ?? throw AppException.Validation("Recipient not found",
                new Dictionary<string, string[]> { ["petParentId"] = new[] { "Recipient does not belong to this business." } });

        MessageTemplate? template = null;
        if (req.TemplateId.HasValue)
        {
            template = await _db.MessageTemplates
                .FirstOrDefaultAsync(t => t.Id == req.TemplateId && t.TenantId == tid, ct)
                ?? throw AppException.Validation("Template not found",
                    new Dictionary<string, string[]> { ["templateId"] = new[] { "Template not found." } });
            if (template.Channel != req.Channel)
                throw AppException.BusinessRule($"This template is for {template.Channel.Humanize()}, but you're trying to send it over {req.Channel.Humanize()}.");
        }

        // Resolve {{variables}} server-side so the persisted body matches what was actually "sent".
        var resolvedBody = TemplateVariables.Substitute(req.Body, req.Variables);

        var convo = await _db.Conversations.FirstOrDefaultAsync(c => c.PetParentId == parent.Id && c.TenantId == tid, ct);
        if (convo is null)
        {
            convo = new Conversation { PetParentId = parent.Id };
            _db.Conversations.Add(convo);
        }

        var now = DateTimeOffset.UtcNow;
        var preview = resolvedBody.Length > PreviewLength
            ? resolvedBody.Substring(0, PreviewLength) + "…"
            : resolvedBody;
        convo.LastMessageAt = now;
        convo.LastMessagePreview = preview;
        convo.LastChannel = req.Channel;
        convo.LastDirection = MessageDirection.Outbound;

        var msg = new Message
        {
            ConversationId = convo.Id,
            PetParentId = parent.Id,
            Direction = MessageDirection.Outbound,
            Channel = req.Channel,
            Subject = req.Channel == MessageChannel.Email ? req.Subject?.Trim() : null,
            Body = resolvedBody,
            TemplateId = template?.Id,
            // Mock send: BSP integration pending vendor decision. See class-level comment.
            Status = MessageStatus.Sent,
            SentAt = now,
            SentByName = _user.DisplayName ?? _user.Email,
        };
        convo.Messages.Add(msg);

        await _db.SaveChangesAsync(ct);

        return new MessageRow(
            msg.Id, msg.ConversationId, msg.Direction, msg.Channel,
            msg.Subject, msg.Body, msg.TemplateId, template?.Name,
            msg.Status, msg.FailureReason,
            msg.SentAt, msg.DeliveredAt, msg.ReadAt,
            msg.SentByName, msg.ExternalMessageId, msg.CreatedAt);
    }

    public async Task<bool> MarkReadAsync(Guid conversationId, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return false;
        var convo = await _db.Conversations.FirstOrDefaultAsync(c => c.Id == conversationId && c.TenantId == _user.TenantId, ct);
        if (convo is null) return false;
        if (convo.UnreadCount == 0) return true;
        convo.UnreadCount = 0;
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
