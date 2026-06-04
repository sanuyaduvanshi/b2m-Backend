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
    string? TransactionId
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
    DateTimeOffset? PaymentTime = null
);

public record RefundRequest(decimal Amount, string Reason);
