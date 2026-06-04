using Pettle.Domain.Clients;
using Pettle.Domain.Common;

namespace Pettle.Domain.Messages;

/// <summary>
/// One thread per (tenant × pet-parent). Aggregated metadata is denormalised onto
/// the conversation row to keep the inbox list query cheap (FR-MSG-01).
/// </summary>
public class Conversation : TenantEntity
{
    public Guid PetParentId { get; set; }
    public PetParent? PetParent { get; set; }

    public DateTimeOffset? LastMessageAt { get; set; }
    public string? LastMessagePreview { get; set; }
    public MessageChannel? LastChannel { get; set; }
    public MessageDirection? LastDirection { get; set; }

    public int UnreadCount { get; set; }

    public ICollection<Message> Messages { get; set; } = new List<Message>();
}
