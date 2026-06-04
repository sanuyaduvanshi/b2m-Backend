namespace Pettle.Application.Bookings;

using Pettle.Application.Clients;

public interface IBookingService
{
    Task<PagedResult<BookingListItem>> ListAsync(BookingListQuery query, CancellationToken ct = default);
    Task<BookingDetail?> GetAsync(Guid id, CancellationToken ct = default);
    Task<BookingDetail> CreateAsync(CreateBookingRequest req, CancellationToken ct = default);
    Task<bool> ChangeStatusAsync(Guid bookingServiceId, BookingStateChangeRequest req, CancellationToken ct = default);
    Task<bool> CancelAsync(Guid id, string? reason, CancellationToken ct = default);
}
