using Pettle.Application.Clients;
using Pettle.Domain.Messages;

namespace Pettle.Application.Messages;

// ---------- Templates ----------

public record MessageTemplateRow(
    Guid Id,
    string Name,
    MessageChannel Channel,
    MessageTemplateCategory Category,
    string? Subject,
    string Body,
    IReadOnlyList<string> Variables,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt
);

public record CreateMessageTemplateRequest(
    string Name,
    MessageChannel Channel,
    MessageTemplateCategory Category,
    string? Subject,
    string Body,
    bool IsActive
);

public record UpdateMessageTemplateRequest(
    string Name,
    MessageChannel Channel,
    MessageTemplateCategory Category,
    string? Subject,
    string Body,
    bool IsActive
);

public interface IMessageTemplateService
{
    Task<PagedResult<MessageTemplateRow>> ListAsync(string? search, MessageChannel? channel, MessageTemplateCategory? category, bool activeOnly, int page, int pageSize, CancellationToken ct = default);
    Task<MessageTemplateRow?> GetAsync(Guid id, CancellationToken ct = default);
    Task<MessageTemplateRow> CreateAsync(CreateMessageTemplateRequest req, CancellationToken ct = default);
    Task<MessageTemplateRow?> UpdateAsync(Guid id, UpdateMessageTemplateRequest req, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
}

// ---------- Conversations + Messages ----------

public record ConversationRow(
    Guid Id,
    Guid PetParentId,
    string ParentName,
    string Phone,
    string? Email,
    DateTimeOffset? LastMessageAt,
    string? LastMessagePreview,
    MessageChannel? LastChannel,
    MessageDirection? LastDirection,
    int UnreadCount
);

public record MessageRow(
    Guid Id,
    Guid ConversationId,
    MessageDirection Direction,
    MessageChannel Channel,
    string? Subject,
    string Body,
    Guid? TemplateId,
    string? TemplateName,
    MessageStatus Status,
    string? FailureReason,
    DateTimeOffset? SentAt,
    DateTimeOffset? DeliveredAt,
    DateTimeOffset? ReadAt,
    string? SentByName,
    string? ExternalMessageId,
    DateTimeOffset CreatedAt
);

public record ThreadView(
    ConversationRow Conversation,
    IReadOnlyList<MessageRow> Messages
);

public record SendMessageRequest(
    Guid PetParentId,
    MessageChannel Channel,
    string? Subject,
    string Body,
    Guid? TemplateId,
    IReadOnlyDictionary<string, string>? Variables
);

public interface IMessageService
{
    Task<PagedResult<ConversationRow>> ListConversationsAsync(string? search, int page, int pageSize, CancellationToken ct = default);
    Task<ThreadView?> GetThreadAsync(Guid petParentId, int messageLimit, CancellationToken ct = default);
    Task<MessageRow> SendAsync(SendMessageRequest req, CancellationToken ct = default);
    Task<bool> MarkReadAsync(Guid conversationId, CancellationToken ct = default);
}
