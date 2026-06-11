using Pettle.Domain.Bookings;

namespace Pettle.Application.Calendar;

public record CalendarEvent(
    Guid Id,                       // BookingService.Id
    Guid BookingId,
    Guid PetParentId,
    string ParentName,
    string PetName,
    BookingServiceType ServiceType,
    BookingStatus Status,
    DateOnly StartDate,
    DateOnly EndDate,              // == StartDate for non-boarding
    TimeOnly? StartTime,           // null for boarding (date-only)
    TimeOnly? EndTime,
    bool AllDay,                   // true for boarding, false for timed services
    bool IsOvernight,              // boarding spans >= 1 night
    string? StaffName,
    Guid? StaffId,
    string? KennelLabel,
    Guid? KennelId,
    string? BoardingType,
    string? ServiceName
);

public record CalendarCounters(int CheckIn, int CheckOut, int DayBoarding, int NightBoarding);

public record RescheduleRequest(
    DateOnly NewStartDate,
    DateOnly? NewEndDate,          // boarding only; null = preserve length
    TimeOnly? NewStartTime,        // timed services only
    TimeOnly? NewEndTime,
    Guid? NewKennelId              // boarding only; null = keep current
);

// --- Manual calendar appointments (add/edit/delete) ---
public record CalendarAppointmentDto(Guid Id, string Title, DateOnly Date, TimeOnly? StartTime, TimeOnly? EndTime, string? Notes, string? Color);
public record CreateOrUpdateAppointmentRequest(string Title, DateOnly Date, TimeOnly? StartTime, TimeOnly? EndTime, string? Notes, string? Color);

public interface ICalendarService
{
    Task<IReadOnlyList<CalendarEvent>> EventsAsync(
        DateOnly fromDate, DateOnly toDate,
        string? serviceType,
        bool overnightOnly,
        CancellationToken ct = default);

    Task<CalendarCounters> CountersAsync(DateOnly date, CancellationToken ct = default);

    Task<CalendarEvent?> RescheduleAsync(Guid bookingServiceId, RescheduleRequest req, CancellationToken ct = default);

    Task<IReadOnlyList<CalendarAppointmentDto>> ListAppointmentsAsync(DateOnly from, DateOnly to, CancellationToken ct = default);
    Task<CalendarAppointmentDto> CreateAppointmentAsync(CreateOrUpdateAppointmentRequest req, CancellationToken ct = default);
    Task<CalendarAppointmentDto?> UpdateAppointmentAsync(Guid id, CreateOrUpdateAppointmentRequest req, CancellationToken ct = default);
    Task<bool> DeleteAppointmentAsync(Guid id, CancellationToken ct = default);
}
