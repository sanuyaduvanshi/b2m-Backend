using Pettle.Domain.Clients;
using Pettle.Domain.Common;

namespace Pettle.Domain.Bookings;

public class Booking : SoftDeletableTenantEntity
{
    public string? LegacyBookingId { get; set; }
    public Guid PetParentId { get; set; }
    public PetParent? PetParent { get; set; }

    public DateOnly BookingDate { get; set; }
    public BookingSource Source { get; set; } = BookingSource.WalkIn;
    public BookingPaymentStatus PaymentStatus { get; set; } = BookingPaymentStatus.Pending;
    public decimal TotalBillingAmount { get; set; }
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
}

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
    public Guid PetId { get; set; }
    public Pet? Pet { get; set; }
    public BookingStatus Status { get; set; } = BookingStatus.Upcoming;
    public string? ServiceName { get; set; }
    public decimal FinalAmount { get; set; }
    public string? Notes { get; set; }
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
