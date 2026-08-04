using Pettle.Application.Clients;
using Pettle.Domain.Inventory;

namespace Pettle.Application.Inventory;

public record SkuListItem(
    Guid Id, string Code, string Name, string? CategoryName,
    string Unit, decimal SellingPrice, decimal CostPrice, decimal TaxPercent,
    int StockOnHand, int ReorderLevel, DateOnly? NearestExpiry, bool IsActive,
    bool IsListedInApp, string? AppImageUrl,
    string? Description = null, Guid? CategoryId = null, decimal MrpPrice = 0,
    string? HsnSacCode = null, bool TrackExpiry = false,
    Guid? BrandId = null, string? BrandName = null,
    IReadOnlyList<SkuPriceVariantDto>? PriceVariants = null
);

/// <summary>One distinct MRP a SKU currently has stock at (aggregated across its batches), with how much is available at that price.</summary>
public record SkuPriceVariantDto(decimal Mrp, decimal QtyAvailable);

public record CreateOrUpdateSkuRequest(
    string Code, string Name, string? Description, Guid? CategoryId, string Unit,
    decimal MrpPrice, decimal SellingPrice, decimal CostPrice, decimal TaxPercent,
    string? HsnSacCode, int ReorderLevel, bool TrackExpiry, bool IsActive,
    string? ImageUrl = null, Guid? BrandId = null
);

public record UpdateSkuListingRequest(bool IsListedInApp, string? AppImageUrl);

public record SkuCategoryDto(Guid Id, string Name, Guid? ParentId, string? ParentName, int SkuCount);
public record CreateOrUpdateCategoryRequest(string Name, Guid? ParentId);

public record SkuBrandDto(Guid Id, string Name);
public record CreateOrUpdateBrandRequest(string Name);

public record VendorListItem(Guid Id, string Name, string? Phone, string? Email, string? Gstin, int CreditDays, decimal CreditLimit, bool IsActive, string? Address = null, string? ContactPerson = null);
public record CreateOrUpdateVendorRequest(string Name, string? ContactPerson, string? Phone, string? Email, string? Address, string? Gstin, int CreditDays, decimal CreditLimit, bool IsActive);

public record PoListItem(
    Guid Id, string? LegacyPoNumber, string PoNumber, string VendorName,
    PoStatus Status, DateOnly PurchaseDate, string? VendorInvoiceNumber,
    PoPaymentStatus PaymentStatus, int NumberOfItems, decimal Total, decimal Paid, decimal Due,
    PurchaseDocType DocType = PurchaseDocType.Purchase,
    string? ReturnReason = null,
    string? AgainstPoNumber = null
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

/// <summary>PaidOn is when the money actually left, which is often not when someone got round to
/// recording it — null means today.</summary>
public record RecordPoPaymentRequest(decimal Amount, string Mode, string? Notes, DateOnly? PaidOn = null);

// --- Debit note (purchase return) ---

/// <summary>Goods going back to the supplier, raised against the bill they came in on.
/// Deliberately mirrors CreatePoRequest field for field — it is the same document in reverse, and
/// the supplier expects to see the same numbers on it — with the return's own extras on the end.</summary>
public record CreateDebitNoteRequest(
    Guid VendorId, DateOnly DebitNoteDate, string? ReferenceBillNumber,
    string? PaymentTerm, DateOnly? DueDate, DateOnly? ShippingDate,
    bool ReverseCharge, bool ExportSez, string? TaxType, string? AccountLedger,
    decimal FlatDiscountPercent, decimal AdditionalCharges, decimal Adjustment, string? Notes,
    string Reason,
    Guid? AgainstPurchaseOrderId,
    List<CreatePoLine> Lines
);

/// <summary>A bill this supplier has, with what each line still has left to return — the picker
/// needs both to stop someone returning more than ever came in.</summary>
public record ReturnablePurchase(
    Guid Id, string PoNumber, DateOnly PurchaseDate, string? VendorInvoiceNumber,
    string? ReferenceBillNumber, decimal Total, IReadOnlyList<ReturnableLine> Lines);

public record ReturnableLine(
    Guid? SkuId, string? ItemCode, string ItemName, string? Unit,
    decimal ReceivedQuantity, decimal AlreadyReturned, decimal ReturnableQuantity,
    decimal UnitCost, decimal Mrp, decimal SellingPrice,
    decimal PurDisc1Percent, decimal PurDisc2Percent, decimal TaxPercent,
    DateOnly? ExpiryDate, string? BatchNumber, decimal StockOnHand);

/// <summary>What a supplier is owed once returns are taken off — bills outstanding minus the debit
/// notes raised against them. Negative means the supplier owes us.</summary>
public record VendorBalance(
    Guid VendorId, string VendorName, decimal BillsDue, decimal DebitNotes, decimal NetPayable);

public record ReceivePoRequest(List<ReceivePoLine> Lines);
public record ReceivePoLine(Guid LineId, decimal ReceivedQuantity);

// --- Manual stock adjustment ---
public enum ManualAdjustmentType { Procurement = 0, SelfConsumption = 1, Damage = 2, Adjustment = 3 }

public record CreateStockAdjustmentRequest(
    ManualAdjustmentType AdjustmentType,
    string? Notes,
    List<StockAdjustmentLine> Lines
);

public record StockAdjustmentLine(
    Guid SkuId,
    int Quantity,
    decimal? UnitPrice,
    string? LineNote
);

public record StockMovementDto(
    Guid Id,
    string SkuName,
    string SkuCode,
    string Reason,
    int QuantityChange,
    int StockAfter,
    DateTimeOffset CreatedAt,
    string? Note
);

public record SkuBatchDto(
    Guid Id,
    string? BatchNumber,
    DateOnly? ExpiryDate,
    decimal QtyRemaining,
    decimal LandingCost,
    string Source,
    DateTimeOffset ReceivedAt,
    decimal? Mrp = null,
    decimal? SellingPrice = null
);

