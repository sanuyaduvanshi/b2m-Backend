using Pettle.Domain.Common;

namespace Pettle.Domain.Messages;

/// <summary>
/// Reusable message template (FR-MB-12 + FR-MSG-02). Body supports {{variable}} placeholders that
/// are substituted at send-time (e.g. {{parent_name}}, {{pet_name}}, {{booking_date}}).
/// </summary>
public class MessageTemplate : SoftDeletableTenantEntity
{
    public string Name { get; set; } = string.Empty;
    public MessageChannel Channel { get; set; }
    public MessageTemplateCategory Category { get; set; } = MessageTemplateCategory.Generic;
    public string? Subject { get; set; }              // Email only.
    public string Body { get; set; } = string.Empty;  // Contains {{var}} placeholders.
    public bool IsActive { get; set; } = true;
}

public enum MessageChannel
{
    WhatsApp = 0,
    Email = 1,
    Sms = 2,
    InApp = 3
}

public enum MessageTemplateCategory
{
    Generic = 0,
    Reminder = 1,
    Booking = 2,
    Invoice = 3,
    Promo = 4
}
