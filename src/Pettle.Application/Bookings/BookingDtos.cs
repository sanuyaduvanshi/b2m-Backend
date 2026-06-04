using Pettle.Application.Clients;
using Pettle.Domain.Bookings;

namespace Pettle.Application.Bookings;

public record BookingListItem(
    Guid Id,
    string? LegacyBookingId,
    DateOnly BookingDate,
    string ParentName,
    string Phone,
    string ServiceTypes,
    BookingPaymentStatus PaymentStatus,
    decimal TotalBillingAmount,
    string? InvoiceNumber,
    BookingSource Source,
    BookingStatus AggregateStatus
);

public record BookingDetail(
    Guid Id,
    string? LegacyBookingId,
    DateOnly BookingDate,
    Guid PetParentId,
    string ParentName,
    string Phone,
    string? Email,
    BookingSource Source,
    BookingPaymentStatus PaymentStatus,
    decimal TotalBillingAmount,
    string? InvoiceNumber,
    string? Notes,
    string? AdditionalInstruction,
    IReadOnlyList<BookingServiceLine> Services,
    IReadOnlyList<BookingAddOnLine> AddOns
);

public record BookingServiceLine(
    Guid Id,
    BookingServiceType ServiceType,
    BookingStatus Status,
    Guid PetId,
    string PetName,
    string? ServiceName,
    decimal FinalAmount,
    string? Notes,
    BookingSubDetail? Sub
);

public record BookingSubDetail(
    DateOnly? CheckIn,
    DateOnly? CheckOut,
    TimeOnly? StartTime,
    TimeOnly? EndTime,
    string? StaffName,
    string? KennelLabel,
    string? BoardingType,
    string? Companion
);

public record BookingAddOnLine(Guid Id, string AddOnService, int Count, decimal? Distance, int? Days, decimal FinalAmount);

public record BookingListQuery(
    string? Tab = "upcoming",      // upcoming | active | past | noshow | requested
    string? ServiceType = null,     // boarding | grooming | vet | daycare
    string? Search = null,
    DateOnly? FromDate = null,
    DateOnly? ToDate = null,
    int Page = 1,
    int PageSize = 50
);

public record CreateBookingRequest(
    Guid PetParentId,
    DateOnly BookingDate,
    BookingSource Source,
    string? Notes,
    string? AdditionalInstruction,
    List<CreateBookingServiceLine> Services
);

public record CreateBookingServiceLine(
    BookingServiceType ServiceType,
    Guid PetId,
    string ServiceName,
    decimal FinalAmount,
    DateOnly? CheckIn,
    DateOnly? CheckOut,
    TimeOnly? StartTime,
    TimeOnly? EndTime,
    Guid? KennelId,
    string? Notes
);

public record BookingStateChangeRequest(BookingStatus NewStatus, string? Reason);
