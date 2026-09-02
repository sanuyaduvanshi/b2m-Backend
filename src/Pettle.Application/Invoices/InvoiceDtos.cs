using Pettle.Application.Clients;
using Pettle.Domain.Invoices;

namespace Pettle.Application.Invoices;

// Time included so the Invoices list can show each payment's own date next to the invoice's Date
// column — without it, a booking billed on one day and settled days later (an advance up front,
// balance at checkout) looked no different from one paid the same day it was raised.
public record PaymentBrief(PaymentMode Mode, decimal Amount, PaymentRecordStatus Status, DateTimeOffset Time);

public record InvoiceListItem(
    Guid Id,
    string? LegacyInvoiceNo,
    string InvoiceNumber,
    InvoiceType InvoiceType,
    DateOnly InvoiceDate,
    string ParentName,
    string Phone,
    string? PetName,
    decimal Revenue,
    decimal Paid,
    decimal Due,
    InvoicePaymentStatus PaymentStatus,
    IReadOnlyList<PaymentBrief> Payments
);

public record InvoiceLineDto(
    Guid Id,
    string BillItemName,
    string? Category,
    string? Description,
    decimal Quantity,
    decimal UnitAmount,
    decimal Discount,
    decimal Subtotal,
    decimal Total,
    string? BatchNumber = null
);

public record PaymentDto(
    Guid Id,
    DateTimeOffset Time,
    decimal Amount,
    PaymentMode Mode,
    PaymentSource Source,
    string? TransactionId,
    PaymentType Type = PaymentType.Balance,
    PaymentRecordStatus Status = PaymentRecordStatus.Success,
    string? Notes = null,
    // Which subscription package auto-paid this specific payment row, if any.
    string? SubscriptionPackageName = null
);

public record InvoiceDetail(
    Guid Id,
    string InvoiceNumber,
    InvoiceType InvoiceType,
    DateOnly InvoiceDate,
    Guid? PetParentId,
    string ParentName,
    string Phone,
    string? PetNameSnapshot,
    decimal BaseAmount,
    decimal AddOnAmount,
    decimal AdditionalAmount,
    decimal DiscountAmount,
    decimal IgstAmount,
    decimal CgstAmount,
    decimal SgstAmount,
    /// <summary>Paise dropped or added to reach a whole-rupee total; printed as its own line so
    /// the bill adds up.</summary>
    decimal RoundOff,
    decimal Revenue,
    decimal Paid,
    decimal Due,
    InvoicePaymentStatus PaymentStatus,
    IReadOnlyList<InvoiceLineDto> Lines,
    IReadOnlyList<PaymentDto> Payments,
    string? Notes = null,
    string? AdditionalChargesReason = null,
    Guid? BookingId = null,
    // Set when any payment on this invoice was auto-debited from a subscription.
    string? SubscriptionPackageName = null,
    // The plan that settled this bill, and what it has left afterwards — printed on the invoice so
    // the customer can see the charge came off their package rather than out of their pocket.
    InvoiceSubscriptionInfo? Subscription = null,
    // Set when a cash refund (not a credit note) was issued against this invoice — lets the detail
    // page show what was refunded, when, and why instead of leaving staff to infer it from the
    // "Refunded" status badge alone.
    decimal? RefundedAmount = null,
    DateTimeOffset? RefundedAt = null,
    string? RefundReason = null
);

public record InvoiceSubscriptionInfo(
    string PackageName,
    decimal CoveredAmount,
    int RemainingSessions,
    int TotalSessions,
    DateOnly ValidUntil,
    decimal RemainingBalance,
    string Status,
    /// <summary>Which pet the plan was sold for, when it was sold for one. The customer's own
    /// question on seeing a deduction is "off which pet's plan?", and with two pets the package
    /// name alone doesn't answer it.</summary>
    string? PetName = null);

public record InvoiceListQuery(
    string? Search = null,
    InvoiceType? Type = null,
    InvoicePaymentStatus? Status = null,
    PaymentMode? Mode = null,
    DateOnly? FromDate = null,
    DateOnly? ToDate = null,
    /// <summary>Scopes to one client — used by the POS "last bill" lookup for the customer
    /// currently selected in a new sale, since Search alone can't target an exact client.</summary>
    Guid? PetParentId = null,
    // Drill-down for the dashboard's "+₹X today from earlier bookings" note: invoices billed
    // before SettledEarlierFrom that received a payment within [SettledEarlierFrom, SettledEarlierTo].
    // Mirrors ReportsService.PeriodSummaryAsync's revenueFromEarlierBills definition exactly, so the
    // itemised list this returns always reconciles with the number that linked here.
    DateOnly? SettledEarlierFrom = null,
    DateOnly? SettledEarlierTo = null,
    int Page = 1,
    int PageSize = 50
);

public record RecordPaymentRequest(
    decimal Amount,
    PaymentMode Mode,
    PaymentSource Source = PaymentSource.WalkIn,
    string? TransactionId = null,
    string? Notes = null,
    DateTimeOffset? PaymentTime = null,
    PaymentType Type = PaymentType.Balance,
    PaymentRecordStatus Status = PaymentRecordStatus.Success
);

/// <summary>AsCreditNote=false ("Refund"): cash leaves the business — subtracts from Paid/Due,
/// no credit note. AsCreditNote=true ("Return"): the customer gets store credit instead of cash —
/// a Credit Note is issued for Amount and the original invoice's Paid/Due are untouched (it stays
/// settled; the credit note is the new liability). ReturnToStock is independent of either — it's
/// about whether the physical goods come back to inventory.</summary>
public record RefundRequest(decimal Amount, string Reason, bool ReturnToStock = false, bool AsCreditNote = false);

public record CreditNoteLookup(Guid Id, string InvoiceNumber, decimal RemainingCreditAmount, Guid? PetParentId, string ParentName);

// --- POS / counter sale ---

public record CreateSaleLine(
    Guid? SkuId,
    string ItemName,
    decimal Quantity,
    decimal UnitAmount,           // per-unit MRP (GST-inclusive retail rate)
    decimal DiscountPercent,      // line discount %
    decimal AddDiscountPercent,   // additional line discount % (applied after the first)
    decimal TaxPercent
);

public record CreateSalePayment(
    PaymentMode Mode,
    decimal Amount,
    string? TransactionId = null
);

public record UpdateInvoiceLine(
    string ItemName,
    decimal Quantity,
    decimal UnitAmount,
    decimal DiscountPercent,
    decimal TaxPercent
);

public record UpdateInvoiceRequest(
    DateOnly InvoiceDate,
    string ParentName,
    string Phone,
    string? PetName,
    string? Notes,
    decimal FlatDiscountPercent,
    decimal AdditionalCharges,
    IReadOnlyList<UpdateInvoiceLine> Lines,
    string? AdditionalChargesReason = null
);

public record CreateSaleRequest(
    DateOnly InvoiceDate,
    Guid? PetParentId,
    string ParentName,
    string Phone,
    string? PetName,
    bool IsDelivery,
    decimal FlatDiscountPercent,  // bill-level discount %
    decimal AdditionalCharges,    // freight/packing etc. (₹)
    string? Notes,
    IReadOnlyList<CreateSaleLine> Lines,
    IReadOnlyList<CreateSalePayment> Payments,
    string? AdditionalChargesReason = null,
    Guid? RedeemCreditNoteId = null,
    decimal RedeemCreditNoteAmount = 0
);
