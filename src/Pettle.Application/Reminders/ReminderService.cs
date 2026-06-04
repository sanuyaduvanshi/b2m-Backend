using Pettle.Application.Clients;
using Pettle.Domain.Reminders;

namespace Pettle.Application.Reminders;

public record ReminderListItem(
    Guid Id, ReminderType Type, string Title, string? Message,
    DateOnly DueDate, ReminderStatus Status, string? ContactName, string? ContactPhone
);

public record HeatmapCell(DateOnly Date, int Count);
public record ReminderOverview(IReadOnlyList<HeatmapCell> Heatmap, int TotalForMonth, IReadOnlyDictionary<string, int> ByType);

public record CreateReminderRequest(ReminderType Type, string Title, string? Message, DateOnly DueDate, Guid? PetParentId, Guid? PetId, Guid? SkuId);
public record SnoozeRequest(DateOnly Until);

/// <summary>
/// Unified shape used by the new reminders UI feed. Items can be either persisted Reminders
/// or derived (live) reminders computed from Pet.Birthday, upcoming bookings, vaccinations,
/// SKU stock levels and SKU expiry. Derived items have a synthetic id prefixed with the source.
/// </summary>
public record ReminderFeedItem(
    string Id,
    string Type,
    string Title,
    string? Subtitle,
    DateOnly DueDate,
    string? ContactName,
    string? Phone,
    string? Email,
    Guid? RelatedId
);

public record ReminderCalendarDay(DateOnly Date, int Count);
public record ReminderCalendarResponse(
    int Year, int Month,
    IReadOnlyDictionary<string, int> Totals,   // by-type counts in this month
    int GrandTotal,
    IReadOnlyList<ReminderCalendarDay> Days
);

public interface IReminderService
{
    Task<PagedResult<ReminderListItem>> ListAsync(ReminderType? type, ReminderStatus? status, DateOnly? from, DateOnly? to, int page, int pageSize, CancellationToken ct = default);
    Task<ReminderOverview> OverviewAsync(int year, int month, CancellationToken ct = default);
    Task<ReminderListItem> CreateAsync(CreateReminderRequest req, CancellationToken ct = default);
    Task<bool> SnoozeAsync(Guid id, SnoozeRequest req, CancellationToken ct = default);
    Task<bool> DismissAsync(Guid id, CancellationToken ct = default);
    Task<bool> MarkSentAsync(Guid id, string via, CancellationToken ct = default);

    Task<IReadOnlyList<ReminderFeedItem>> FeedAsync(string type, DateOnly from, DateOnly to, CancellationToken ct = default);
    Task<ReminderCalendarResponse> CalendarAsync(int year, int month, CancellationToken ct = default);
}
