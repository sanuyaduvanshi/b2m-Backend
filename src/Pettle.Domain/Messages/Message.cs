using Pettle.Domain.Common;

namespace Pettle.Domain.Messages;

public class Message : TenantEntity
{
    public Guid ConversationId { get; set; }
    public Conversation? Conversation { get; set; }

    public Guid PetParentId { get; set; }  // denormalised for direct queries
    public MessageDirection Direction { get; set; }
    public MessageChannel Channel { get; set; }

    public string? Subject { get; set; }
    public string Body { get; set; } = string.Empty;

    public Guid? TemplateId { get; set; }
    public MessageTemplate? Template { get; set; }

    public MessageStatus Status { get; set; } = MessageStatus.Pending;
    public string? FailureReason { get; set; }

    public DateTimeOffset? SentAt { get; set; }
    public DateTimeOffset? DeliveredAt { get; set; }
    public DateTimeOffset? ReadAt { get; set; }

    public string? SentByName { get; set; }

    /// <summary>Provider-side id (WhatsApp/SMS/Email message id) — populated when real BSP is wired up.</summary>
    public string? ExternalMessageId { get; set; }
}

public enum MessageDirection
{
    Outbound = 0,
    Inbound = 1
}

public enum MessageStatus
{
    Pending = 0,
    Sent = 1,
    Failed = 2,
    Delivered = 3,
    Read = 4
}
