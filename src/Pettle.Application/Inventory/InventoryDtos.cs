using Pettle.Application.Clients;
using Pettle.Domain.Inventory;

namespace Pettle.Application.Inventory;

public record SkuListItem(
    Guid Id, string Code, string Name, string? CategoryName,
    string Unit, decimal SellingPrice, decimal CostPrice, decimal TaxPercent,
    int StockOnHand, int ReorderLevel, DateOnly? NearestExpiry, bool IsActive,
    bool IsListedInApp, string? AppImageUrl,
    string? Description = null, Guid? CategoryId = null, decimal MrpPrice = 0,
    string? HsnSacCode = null, bool TrackExpiry = false
);

public record CreateOrUpdateSkuRequest(
    string Code, string Name, string? Description, Guid? CategoryId, string Unit,
    decimal MrpPrice, decimal SellingPrice, decimal CostPrice, decimal TaxPercent,
    string? HsnSacCode, int ReorderLevel, bool TrackExpiry, bool IsActive
);

public record UpdateSkuListingRequest(bool IsListedInApp, string? AppImageUrl);

public record SkuCategoryDto(Guid Id, string Name, Guid? ParentId, string? ParentName, int SkuCount);
public record CreateOrUpdateCategoryRequest(string Name, Guid? ParentId);

public record VendorListItem(Guid Id, string Name, string? Phone, string? Email, string? Gstin, int CreditDays, decimal CreditLimit, bool IsActive, string? Address = null, string? ContactPerson = null);
public record CreateOrUpdateVendorRequest(string Name, string? ContactPerson, string? Phone, string? Email, string? Address, string? Gstin, int CreditDays, decimal CreditLimit, bool IsActive);

public record PoListItem(
    Guid Id, string? LegacyPoNumber, string PoNumber, string VendorName,
    PoStatus Status, DateOnly PurchaseDate, string? VendorInvoiceNumber,
    PoPaymentStatus PaymentStatus, int NumberOfItems, decimal Total, decimal Paid, decimal Due
);

public record PoLineDto(
    Guid Id, Guid? SkuId, string? ItemCode, string ItemName, string? Unit,
    decimal Quantity, decimal FreeQuantity, decimal ReceivedQuantity, decimal UnitCost,
    decimal Mrp, decimal SellingPrice, decimal PurDisc1Percent, decimal PurDisc2Percent,
    decimal TaxPercent, decimal TaxableAmount, decimal TaxAmount, decimal LandingCost, decimal LineTotal,
    DateOnly? ExpiryDate, string? BatchNumber
);

public record PoDetail(
    Guid Id, string PoNumber, Guid VendorId, string VendorName, PoStatus Status,
    DateOnly PurchaseDate, string? VendorInvoiceNumber, string? ReferenceBillNumber, string? MaterialInwardNo,
    string? PaymentTerm, DateOnly? DueDate, DateOnly? ShippingDate, bool ReverseCharge, bool ExportSez,
    string? TaxType, string? AccountLedger, PoPaymentStatus PaymentStatus,
    decimal SubTotal, decimal GrossAmount, decimal FlatDiscountPercent, decimal FlatDiscountAmount,
    decimal DiscountAmount, decimal TaxableAmount, decimal TaxAmount, decimal AdditionalCharges,
    decimal Adjustment, decimal RoundOff, decimal Total, decimal Paid, decimal Due,
    string? Notes, IReadOnlyList<PoLineDto> Lines
);

public record CreatePoRequest(
    Guid VendorId, DateOnly PurchaseDate, string? VendorInvoiceNumber, string? ReferenceBillNumber,
    string? MaterialInwardNo, string? PaymentTerm, DateOnly? DueDate, DateOnly? ShippingDate,
    bool ReverseCharge, bool ExportSez, string? TaxType, string? AccountLedger,
    decimal FlatDiscountPercent, decimal AdditionalCharges, decimal Adjustment, string? Notes,
    List<CreatePoLine> Lines
);

public record CreatePoLine(
    Guid? SkuId, string? ItemCode, string ItemName, string? Unit,
    decimal Quantity, decimal FreeQuantity, decimal UnitCost, decimal Mrp, decimal SellingPrice,
    decimal PurDisc1Percent, decimal PurDisc2Percent, decimal TaxPercent,
    DateOnly? ExpiryDate, string? BatchNumber
);

public record RecordPoPaymentRequest(decimal Amount, string Mode, string? Notes);

public record ReceivePoRequest(List<ReceivePoLine> Lines);
public record ReceivePoLine(Guid LineId, decimal ReceivedQuantity);
