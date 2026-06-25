using Pettle.Domain.Bookings;
using Pettle.Domain.Clients;
using Pettle.Domain.Common;

namespace Pettle.Domain.Invoices;

public class Invoice : SoftDeletableTenantEntity
{
    public string? LegacyInvoiceNo { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public InvoiceType InvoiceType { get; set; } = InvoiceType.Booking;
    public DateOnly InvoiceDate { get; set; }
    public Guid? BookingId { get; set; }
    public Booking? Booking { get; set; }
    public Guid? PetParentId { get; set; }
    public PetParent? PetParent { get; set; }
    public string ParentNameSnapshot { get; set; } = string.Empty;
    public string PhoneSnapshot { get; set; } = string.Empty;
    public string? PetNameSnapshot { get; set; }

    public decimal BaseAmount { get; set; }
    public decimal AddOnAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal AdditionalAmount { get; set; }
    public decimal AdditionalDiscountAmount { get; set; }
    public decimal IgstAmount { get; set; }
    public decimal CgstAmount { get; set; }
    public decimal SgstAmount { get; set; }
    public decimal Revenue { get; set; }
    public decimal Paid { get; set; }
    public decimal Due { get; set; }

    public InvoicePaymentStatus PaymentStatus { get; set; } = InvoicePaymentStatus.Pending;
    public string? Notes { get; set; }

    public ICollection<InvoiceLineItem> Lines { get; set; } = new List<InvoiceLineItem>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}

public enum InvoiceType { Booking = 0, Sale = 1, Subscription = 2, Adjustment = 3, CreditNote = 4 }
public enum InvoicePaymentStatus { Pending = 0, PartiallyPaid = 1, Paid = 2, Refunded = 3, Cancelled = 4 }

public class InvoiceLineItem : TenantEntity
{
    public Guid InvoiceId { get; set; }
    public Invoice? Invoice { get; set; }

    public string BillItemName { get; set; } = string.Empty;
    public string? BillSection { get; set; }
    public string? ServiceName { get; set; }
    public string? SkuName { get; set; }
    public string? SkuLegacyId { get; set; }
    public string? Description { get; set; }
    public string? Category { get; set; }
    public string? SubCategory { get; set; }
    public string? HsnSacCode { get; set; }

    public decimal Quantity { get; set; } = 1;
    public decimal UnitAmount { get; set; }
    public decimal Discount { get; set; }
    public decimal Subtotal { get; set; }
    public decimal IgstAmount { get; set; }
    public decimal CgstAmount { get; set; }
    public decimal SgstAmount { get; set; }
    public decimal Total { get; set; }
    public bool IsReturn { get; set; }

    public string? BreedSnapshot { get; set; }
    public string? BreedSizeSnapshot { get; set; }
    public string? CoatLengthSnapshot { get; set; }
    public string? StaffName { get; set; }
}

public class Payment : TenantEntity
{
    public string? LegacyPaymentId { get; set; }
    /// <summary>A payment belongs to either an invoice or an issued subscription.</summary>
    public Guid? InvoiceId { get; set; }
    public Invoice? Invoice { get; set; }
    public Guid? IssuedSubscriptionId { get; set; }
    public DateTimeOffset PaymentTime { get; set; }
    public decimal Amount { get; set; }
    public PaymentMode Mode { get; set; } = PaymentMode.Cash;
    public PaymentSource Source { get; set; } = PaymentSource.WalkIn;
    /// <summary>Pre-service deposit (Advance) vs balance settlement (Balance) — Pettle distinction.</summary>
    public PaymentType Type { get; set; } = PaymentType.Balance;
    public PaymentRecordStatus Status { get; set; } = PaymentRecordStatus.Success;
    public string? TransactionId { get; set; }
    public string? Notes { get; set; }
}

public enum PaymentMode { Cash = 0, Card = 1, Upi = 2, NetBanking = 3, Wallet = 4, Cheque = 5, Credit = 6, Other = 99 }
public enum PaymentSource { WalkIn = 0, Online = 1, Gateway = 2, App = 3 }
public enum PaymentType { Advance = 0, Balance = 1 }
public enum PaymentRecordStatus { Success = 0, Pending = 1, Failed = 2 }
