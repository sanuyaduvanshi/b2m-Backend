using Pettle.Application.Clients;
using Pettle.Domain.Inventory;

namespace Pettle.Application.Inventory;

public record SkuListItem(
    Guid Id, string Code, string Name, string? CategoryName,
    string Unit, decimal SellingPrice, decimal CostPrice, decimal TaxPercent,
    int StockOnHand, int ReorderLevel, DateOnly? NearestExpiry, bool IsActive,
    bool IsListedInApp, string? AppImageUrl
);

public record CreateOrUpdateSkuRequest(
    string Code, string Name, string? Description, Guid? CategoryId, string Unit,
    decimal MrpPrice, decimal SellingPrice, decimal CostPrice, decimal TaxPercent,
    string? HsnSacCode, int ReorderLevel, bool TrackExpiry, bool IsActive
);

public record UpdateSkuListingRequest(bool IsListedInApp, string? AppImageUrl);

public record VendorListItem(Guid Id, string Name, string? Phone, string? Email, string? Gstin, int CreditDays, decimal CreditLimit, bool IsActive);
public record CreateOrUpdateVendorRequest(string Name, string? ContactPerson, string? Phone, string? Email, string? Address, string? Gstin, int CreditDays, decimal CreditLimit, bool IsActive);

public record PoListItem(
    Guid Id, string? LegacyPoNumber, string PoNumber, string VendorName,
    PoStatus Status, DateOnly PurchaseDate, string? VendorInvoiceNumber,
    PoPaymentStatus PaymentStatus, int NumberOfItems, decimal Total, decimal Paid, decimal Due
);

public record PoLineDto(Guid Id, Guid? SkuId, string ItemName, decimal Quantity, decimal ReceivedQuantity, decimal UnitCost, decimal TaxPercent, decimal LineTotal, DateOnly? ExpiryDate, string? BatchNumber);

public record PoDetail(
    Guid Id, string PoNumber, Guid VendorId, string VendorName, PoStatus Status,
    DateOnly PurchaseDate, string? VendorInvoiceNumber, PoPaymentStatus PaymentStatus,
    decimal SubTotal, decimal TaxAmount, decimal Adjustment, decimal Total, decimal Paid, decimal Due,
    string? Notes, IReadOnlyList<PoLineDto> Lines
);

public record CreatePoRequest(
    Guid VendorId, DateOnly PurchaseDate, string? VendorInvoiceNumber, decimal Adjustment, string? Notes,
    List<CreatePoLine> Lines
);

public record CreatePoLine(Guid? SkuId, string ItemName, decimal Quantity, decimal UnitCost, decimal TaxPercent, DateOnly? ExpiryDate, string? BatchNumber);

public record ReceivePoRequest(List<ReceivePoLine> Lines);
public record ReceivePoLine(Guid LineId, decimal ReceivedQuantity);
