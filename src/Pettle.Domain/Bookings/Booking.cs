using Pettle.Domain.Clients;
using Pettle.Domain.Common;

namespace Pettle.Domain.Bookings;

public class Booking : SoftDeletableTenantEntity
{
    public string? LegacyBookingId { get; set; }
    public Guid? PetParentId { get; set; }
    public PetParent? PetParent { get; set; }
    public string? GuestName { get; set; }
    public string? GuestPhone { get; set; }

    public DateOnly BookingDate { get; set; }
    public BookingSource Source { get; set; } = BookingSource.WalkIn;
    public BookingPaymentStatus PaymentStatus { get; set; } = BookingPaymentStatus.Pending;
    public decimal TotalBillingAmount { get; set; }
    /// <summary>Sum of service line prices before discount — set once at creation, used to recompute TotalBillingAmount whenever DiscountPercent changes.</summary>
    public decimal GrossBillingAmount { get; set; }
    public decimal DiscountPercent { get; set; }
    public string? InvoiceNumber { get; set; }
    public string? PhoneSnapshot { get; set; }
    public string? EmailSnapshot { get; set; }

    public string? Notes { get; set; }
    public string? AdditionalInstruction { get; set; }

    public ICollection<BookingService> Services { get; set; } = new List<BookingService>();
    public ICollection<BoardingDetail> BoardingDetails { get; set; } = new List<BoardingDetail>();
    public ICollection<GroomingDetail> GroomingDetails { get; set; } = new List<GroomingDetail>();
    public ICollection<VetDetail> VetDetails { get; set; } = new List<VetDetail>();
    public ICollection<DayCareDetail> DayCareDetails { get; set; } = new List<DayCareDetail>();
    public ICollection<BookingAddOn> AddOns { get; set; } = new List<BookingAddOn>();
    public ICollection<BookingInventoryItem> InventoryItems { get; set; } = new List<BookingInventoryItem>();
    public ICollection<BookingEstimateLine> EstimateLines { get; set; } = new List<BookingEstimateLine>();
    public ICollection<BookingChangeRequest> ChangeRequests { get; set; } = new List<BookingChangeRequest>();
}

/// <summary>A quoted line in a booking's cost estimate, shown to the client before confirmation.</summary>
public class BookingEstimateLine : TenantEntity
{
    public Guid BookingId { get; set; }
    public Booking? Booking { get; set; }
    public string Label { get; set; } = string.Empty;
    public decimal Quantity { get; set; } = 1;
    public decimal UnitAmount { get; set; }
    public decimal Amount { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>A change a client/staff requested against a booking (date shift, add service, etc.).</summary>
public class BookingChangeRequest : TenantEntity
{
    public Guid BookingId { get; set; }
    public Booking? Booking { get; set; }
    public string Description { get; set; } = string.Empty;
    public ChangeRequestStatus Status { get; set; } = ChangeRequestStatus.Pending;
    public DateTimeOffset RequestedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? RequestedBy { get; set; }
    public string? ResolutionNote { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
}

public enum ChangeRequestStatus { Pending = 0, Approved = 1, Rejected = 2 }

public enum BookingSource { WalkIn = 0, Web = 1, ParentApp = 2, Phone = 3, ThirdParty = 4 }
public enum BookingPaymentStatus { Pending = 0, PartiallyPaid = 1, Paid = 2, Refunded = 3 }

public enum BookingServiceType { Boarding = 0, Grooming = 1, Vet = 2, DayCare = 3 }

public enum BookingStatus
{
    Requested = 0,
    Accepted = 1,
    Rejected = 2,
    Upcoming = 3,
    CheckedIn = 4,
    Active = 5,
    CheckedOut = 6,
    NoShow = 7,
    Cancelled = 8
}

public class BookingService : TenantEntity
{
    public Guid BookingId { get; set; }
    public Booking? Booking { get; set; }
    public BookingServiceType ServiceType { get; set; }
    public Guid? PetId { get; set; }
    public Pet? Pet { get; set; }
    public string? PetNameSnapshot { get; set; }
    public BookingStatus Status { get; set; } = BookingStatus.Upcoming;
    public string? ServiceName { get; set; }
    public decimal FinalAmount { get; set; }
    public string? Notes { get; set; }
    public Guid? SkuId { get; set; }
    public int SkuQuantity { get; set; } = 1;
    public ICollection<BookingServiceAddOn> AddOns { get; set; } = new List<BookingServiceAddOn>();
}

public class BookingServiceAddOn : TenantEntity
{
    public Guid BookingServiceId { get; set; }
    public BookingService? BookingService { get; set; }
    public Guid? CatalogueItemId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
}

public class BoardingDetail : TenantEntity
{
    public Guid BookingServiceId { get; set; }
    public BookingService? BookingService { get; set; }
    public string? BoardingType { get; set; }
    public DateOnly CheckInDate { get; set; }
    public DateOnly CheckOutDate { get; set; }
    public TimeOnly? CheckInTime { get; set; }
    public TimeOnly? CheckOutTime { get; set; }
    public string? CheckInSlot { get; set; }
    public string? CheckOutSlot { get; set; }
    public decimal? Weight { get; set; }
    public decimal? CheckOutWeight { get; set; }
    public string? MealType { get; set; }
    public Guid? KennelId { get; set; }
    public string? KennelLabel { get; set; }
    public decimal LateCheckoutFees { get; set; }
    public decimal RefundAmount { get; set; }
    public string? RefundReason { get; set; }
    public string? CompanionName { get; set; }
    public string? CompanionPhone { get; set; }
}

public class GroomingDetail : TenantEntity
{
    public Guid BookingServiceId { get; set; }
    public BookingService? BookingService { get; set; }
    public DateOnly ServiceDate { get; set; }
    public TimeOnly? StartTime { get; set; }
    public TimeOnly? EndTime { get; set; }
    public string? StaffName { get; set; }
    public Guid? StaffId { get; set; }
    public string? ServicesText { get; set; }
}

public class VetDetail : TenantEntity
{
    public Guid BookingServiceId { get; set; }
    public BookingService? BookingService { get; set; }
    public DateOnly ServiceDate { get; set; }
    public TimeOnly? StartTime { get; set; }
    public TimeOnly? EndTime { get; set; }
    public string? StaffName { get; set; }
    public Guid? StaffId { get; set; }
    public string? ServicesText { get; set; }
}

public class DayCareDetail : TenantEntity
{
    public Guid BookingServiceId { get; set; }
    public BookingService? BookingService { get; set; }
    public DateOnly ServiceDate { get; set; }
    public TimeOnly? StartTime { get; set; }
    public TimeOnly? EndTime { get; set; }
    public string? StaffName { get; set; }
    public Guid? StaffId { get; set; }
    public string? ServicesText { get; set; }
}

public class BookingAddOn : TenantEntity
{
    public Guid BookingId { get; set; }
    public Booking? Booking { get; set; }
    public string AddOnService { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal? Distance { get; set; }
    public int? Days { get; set; }
    public decimal FinalAmount { get; set; }
}

/// <summary>A SKU sold as part of a booking, independent of any one service line — e.g. a shampoo
/// sold alongside a mixed Boarding+Grooming visit. Deducts stock the same way a per-service SKU
/// sale does, just without being tied to which service line "used" it.</summary>
public class BookingInventoryItem : TenantEntity
{
    public Guid BookingId { get; set; }
    public Booking? Booking { get; set; }
    public Guid SkuId { get; set; }
    public string SkuNameSnapshot { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public decimal FinalAmount { get; set; }
}

public class BookingRequest : TenantEntity
{
    public string? LegacyRequestId { get; set; }
    public Guid? PetParentId { get; set; }
    public string ParentName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? PetName { get; set; }
    public string? Notes { get; set; }
    public BookingServiceType RequestedServiceType { get; set; }
    public DateOnly RequestedDate { get; set; }
    public BookingRequestStatus Status { get; set; } = BookingRequestStatus.Requested;
    public string? RejectionReason { get; set; }
    public Guid? ConvertedBookingId { get; set; }
}

public enum BookingRequestStatus { Requested = 0, Accepted = 1, Rejected = 2, Converted = 3 }
