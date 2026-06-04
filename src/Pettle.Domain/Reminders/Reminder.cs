using Pettle.Domain.Clients;
using Pettle.Domain.Common;

namespace Pettle.Domain.Reminders;

public class Reminder : TenantEntity
{
    public ReminderType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Message { get; set; }
    public DateOnly DueDate { get; set; }
    public ReminderStatus Status { get; set; } = ReminderStatus.Pending;
    public DateOnly? SnoozedUntil { get; set; }
    public Guid? PetParentId { get; set; }
    public PetParent? PetParent { get; set; }
    public Guid? PetId { get; set; }
    public Pet? Pet { get; set; }
    public Guid? SkuId { get; set; }
    public DateTimeOffset? SentAt { get; set; }
    public string? SentVia { get; set; }
}

public enum ReminderType
{
    Birthday = 0,
    UpcomingBooking = 1,
    Vaccination = 2,
    BookingFollowUp = 3,
    Reorder = 4,
    InventoryExpiry = 5,
    LowInventory = 6,
    Custom = 99
}

public enum ReminderStatus { Pending = 0, Sent = 1, Dismissed = 2, Snoozed = 3 }

public class ReminderRule : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public ReminderType TriggerType { get; set; }
    public int LeadDays { get; set; }
    public string Channel { get; set; } = "WhatsApp";
    public string? TemplateBody { get; set; }
    public string Schedule { get; set; } = "08:00";
    public bool IsActive { get; set; } = true;
}
