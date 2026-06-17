namespace Pettle.Application.Bookings;

using Pettle.Application.Clients;

public interface IBookingService
{
    Task<PagedResult<BookingListItem>> ListAsync(BookingListQuery query, CancellationToken ct = default);
    Task<BookingDetail?> GetAsync(Guid id, CancellationToken ct = default);
    Task<BookingDetail> CreateAsync(CreateBookingRequest req, CancellationToken ct = default);
    Task<bool> ChangeStatusAsync(Guid bookingServiceId, BookingStateChangeRequest req, CancellationToken ct = default);
    Task<bool> CancelAsync(Guid id, string? reason, CancellationToken ct = default);

    Task<IReadOnlyList<BookingEstimateLineDto>?> SaveEstimateAsync(Guid bookingId, SaveEstimateRequest req, CancellationToken ct = default);
    Task<BookingChangeRequestDto?> AddChangeRequestAsync(Guid bookingId, CreateChangeRequestRequest req, CancellationToken ct = default);
    Task<BookingChangeRequestDto?> ResolveChangeRequestAsync(Guid bookingId, Guid changeRequestId, ResolveChangeRequestRequest req, CancellationToken ct = default);
}
