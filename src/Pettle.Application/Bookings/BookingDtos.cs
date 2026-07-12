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
    Guid? PetParentId,
    string ParentName,
    string Phone,
    string? Email,
    BookingSource Source,
    BookingPaymentStatus PaymentStatus,
    decimal TotalBillingAmount,
    string? InvoiceNumber,
    Guid? InvoiceId,
    decimal InvoicePaid,
    decimal InvoiceDue,
    string? Notes,
    string? AdditionalInstruction,
    IReadOnlyList<BookingServiceLine> Services,
    IReadOnlyList<BookingAddOnLine> AddOns,
    IReadOnlyList<BookingEstimateLineDto> Estimate,
    IReadOnlyList<BookingChangeRequestDto> ChangeRequests
);

public record BookingEstimateLineDto(Guid Id, string Label, decimal Quantity, decimal UnitAmount, decimal Amount, int SortOrder);
public record EstimateLineInput(string Label, decimal Quantity, decimal UnitAmount);
public record SaveEstimateRequest(IReadOnlyList<EstimateLineInput> Lines);

public record BookingChangeRequestDto(Guid Id, string Description, ChangeRequestStatus Status, DateTimeOffset RequestedAt, string? RequestedBy, string? ResolutionNote, DateTimeOffset? ResolvedAt);
public record CreateChangeRequestRequest(string Description);
public record ResolveChangeRequestRequest(ChangeRequestStatus Status, string? ResolutionNote);

public record ServiceAddOnLine(Guid Id, string Name, decimal Price, Guid? CatalogueItemId);

public record BookingServiceLine(
    Guid Id,
    BookingServiceType ServiceType,
    BookingStatus Status,
    Guid? PetId,
    string PetName,
    string? ServiceName,
    decimal FinalAmount,
    string? Notes,
    BookingSubDetail? Sub,
    Guid? SkuId = null,
    int SkuQuantity = 1,
    IReadOnlyList<ServiceAddOnLine>? AddOns = null
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
    Guid? PetParentId = null,
    int Page = 1,
    int PageSize = 50
);

public record CreateBookingRequest(
    Guid? PetParentId,
    DateOnly BookingDate,
    BookingSource Source,
    string? Notes,
    string? AdditionalInstruction,
    List<CreateBookingServiceLine> Services,
    string? GuestName = null,
    string? GuestPhone = null,
    Guid? UseSubscriptionId = null
);

public record CreateServiceAddOn(string Name, decimal Price, Guid? CatalogueItemId = null);

public record CreateBookingServiceLine(
    BookingServiceType ServiceType,
    Guid? PetId,
    string ServiceName,
    decimal FinalAmount,
    DateOnly? CheckIn,
    DateOnly? CheckOut,
    TimeOnly? StartTime,
    TimeOnly? EndTime,
    Guid? KennelId,
    string? Notes,
    string? PetNameOverride = null,
    Guid? SkuId = null,
    int SkuQuantity = 1,
    List<CreateServiceAddOn>? AddOns = null
);

public record BookingStateChangeRequest(BookingStatus NewStatus, string? Reason);
