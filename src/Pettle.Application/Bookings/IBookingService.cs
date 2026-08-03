namespace Pettle.Application.Bookings;

using Pettle.Application.Clients;

public interface IBookingService
{
    Task<PagedResult<BookingListItem>> ListAsync(BookingListQuery query, CancellationToken ct = default);
    Task<BookingDetail?> GetAsync(Guid id, CancellationToken ct = default);
    Task<BookingDetail> CreateAsync(CreateBookingRequest req, CancellationToken ct = default);
    Task<bool> ChangeStatusAsync(Guid bookingServiceId, BookingStateChangeRequest req, CancellationToken ct = default);
    // Moves every service on the booking to the same status in one step — a booking now has one
    // shared status rather than each service tracking its own, so staff change it once instead of
    // per service line.
    Task<bool> ChangeBookingStatusAsync(Guid bookingId, BookingStateChangeRequest req, CancellationToken ct = default);
    Task<bool> CancelAsync(Guid id, string? reason, CancellationToken ct = default);
    /// <summary>Removes a booking and everything it created — its invoice and payments, the stock
    /// it deducted, and any subscription session it consumed. Cancel is the normal way to close a
    /// booking that won't happen; this is for one that shouldn't exist at all (a test row, a
    /// duplicate). A booking that already took money needs a reason, which is kept on the audit
    /// entry, because deleting it removes that money from the reports.</summary>
    Task<bool> DeleteAsync(Guid id, string? reason, CancellationToken ct = default);
    Task<bool> ApplyDiscountAsync(Guid bookingId, decimal discountPercent, CancellationToken ct = default);

    Task<IReadOnlyList<BookingEstimateLineDto>?> SaveEstimateAsync(Guid bookingId, SaveEstimateRequest req, CancellationToken ct = default);
    Task<BookingChangeRequestDto?> AddChangeRequestAsync(Guid bookingId, CreateChangeRequestRequest req, CancellationToken ct = default);
    Task<BookingChangeRequestDto?> ResolveChangeRequestAsync(Guid bookingId, Guid changeRequestId, ResolveChangeRequestRequest req, CancellationToken ct = default);
}
