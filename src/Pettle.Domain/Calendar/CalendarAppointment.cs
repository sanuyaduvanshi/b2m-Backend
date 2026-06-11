using Pettle.Domain.Common;

namespace Pettle.Domain.Calendar;

/// <summary>
/// A manually-created calendar entry (appointment / reminder / note) — distinct from the
/// booking-derived events the calendar also shows. Supports full add/edit/delete.
/// </summary>
public class CalendarAppointment : TenantEntity
{
    public string Title { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public TimeOnly? StartTime { get; set; }
    public TimeOnly? EndTime { get; set; }
    public string? Notes { get; set; }
    public string? Color { get; set; }
}
