using Pettle.Application.Clients;
using Pettle.Domain.Bookings;

namespace Pettle.Application.BookingRequests;

public record BookingRequestRow(
    Guid Id,
    string? LegacyRequestId,
    Guid? PetParentId,
    string ParentName,
    string Phone,
    string? Email,
    string? PetName,
    BookingServiceType RequestedServiceType,
    DateOnly RequestedDate,
    BookingRequestStatus Status,
    string? Notes,
    string? RejectionReason,
    Guid? ConvertedBookingId,
    DateTimeOffset CreatedAt
);

public record BookingRequestCounts(int Requested, int Accepted, int Rejected, int Converted);
public record BookingRequestBoard(BookingRequestCounts Counts, PagedResult<BookingRequestRow> Page);

public record CreateBookingRequestRequest(
    string ParentName,
    string Phone,
    string? Email,
    string? PetName,
    BookingServiceType RequestedServiceType,
    DateOnly RequestedDate,
    string? Notes,
    Guid? PetParentId  // when source app has matched to a known client
);

public record RejectBookingRequestRequest(string Reason);
public record LinkBookingRequest(Guid BookingId);

public interface IBookingRequestService
{
    Task<BookingRequestBoard> ListAsync(string? tab, string? search, int page, int pageSize, CancellationToken ct = default);
    Task<BookingRequestRow> CreateAsync(CreateBookingRequestRequest req, CancellationToken ct = default);
    Task<bool> ApproveAsync(Guid id, CancellationToken ct = default);
    Task<bool> RejectAsync(Guid id, RejectBookingRequestRequest req, CancellationToken ct = default);
    Task<bool> MarkConvertedAsync(Guid id, Guid bookingId, CancellationToken ct = default);
}
