using Pettle.Application.Clients;
using Pettle.Domain.Invoices;

namespace Pettle.Application.Invoices;

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
    InvoicePaymentStatus PaymentStatus
);

public record InvoiceLineDto(
    Guid Id,
    string BillItemName,
    string? Category,
    decimal Quantity,
    decimal UnitAmount,
    decimal Discount,
    decimal Subtotal,
    decimal Total
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
    string? Notes = null
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
    decimal DiscountAmount,
    decimal IgstAmount,
    decimal CgstAmount,
    decimal SgstAmount,
    decimal Revenue,
    decimal Paid,
    decimal Due,
    InvoicePaymentStatus PaymentStatus,
    IReadOnlyList<InvoiceLineDto> Lines,
    IReadOnlyList<PaymentDto> Payments
);

public record InvoiceListQuery(
    string? Search = null,
    InvoiceType? Type = null,
    InvoicePaymentStatus? Status = null,
    DateOnly? FromDate = null,
    DateOnly? ToDate = null,
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

public record RefundRequest(decimal Amount, string Reason);

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
    IReadOnlyList<CreateSalePayment> Payments
);
