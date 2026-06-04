using Pettle.Domain.Common;

namespace Pettle.Domain.Inventory;

public class SkuCategory : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public Guid? ParentId { get; set; }
    public SkuCategory? Parent { get; set; }
    public ICollection<SkuCategory> Children { get; set; } = new List<SkuCategory>();
}

public class Sku : SoftDeletableTenantEntity
{
    public string? LegacySkuId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? CategoryId { get; set; }
    public SkuCategory? Category { get; set; }
    public string Unit { get; set; } = "ea";
    public decimal MrpPrice { get; set; }
    public decimal SellingPrice { get; set; }
    public decimal CostPrice { get; set; }
    public decimal TaxPercent { get; set; }
    public string? HsnSacCode { get; set; }
    public int ReorderLevel { get; set; }
    public int StockOnHand { get; set; }
    public bool TrackExpiry { get; set; }
    public DateOnly? NearestExpiry { get; set; }
    public bool IsActive { get; set; } = true;
    /// <summary>Whether this SKU is published to the parent-facing in-app store.</summary>
    public bool IsListedInApp { get; set; }
    public string? AppImageUrl { get; set; }
}

public class Vendor : SoftDeletableTenantEntity
{
    public string Name { get; set; } = string.Empty;
    public string? ContactPerson { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? Gstin { get; set; }
    public int CreditDays { get; set; }
    public decimal CreditLimit { get; set; }
    public bool IsActive { get; set; } = true;
}

public class PurchaseOrder : SoftDeletableTenantEntity
{
    public string? LegacyPoNumber { get; set; }
    public string PoNumber { get; set; } = string.Empty;
    public Guid VendorId { get; set; }
    public Vendor? Vendor { get; set; }
    public PoStatus Status { get; set; } = PoStatus.Draft;
    public DateOnly PurchaseDate { get; set; }
    public string? VendorInvoiceNumber { get; set; }
    public PoPaymentStatus PaymentStatus { get; set; } = PoPaymentStatus.Unpaid;
    public int NumberOfItems { get; set; }
    public decimal SubTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal Adjustment { get; set; }
    public decimal Total { get; set; }
    public decimal Paid { get; set; }
    public decimal Due { get; set; }
    public string? Notes { get; set; }
    public ICollection<PurchaseOrderLine> Lines { get; set; } = new List<PurchaseOrderLine>();
}

public enum PoStatus { Draft = 0, Sent = 1, Received = 2, PartiallyReceived = 3, Closed = 4, Cancelled = 5 }
public enum PoPaymentStatus { Unpaid = 0, PartiallyPaid = 1, Paid = 2 }

public class PurchaseOrderLine : TenantEntity
{
    public Guid PurchaseOrderId { get; set; }
    public PurchaseOrder? PurchaseOrder { get; set; }
    public Guid? SkuId { get; set; }
    public Sku? Sku { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal ReceivedQuantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TaxPercent { get; set; }
    public decimal LineTotal { get; set; }
    public DateOnly? ExpiryDate { get; set; }
    public string? BatchNumber { get; set; }
}

public class StockMovement : TenantEntity
{
    public Guid SkuId { get; set; }
    public Sku? Sku { get; set; }
    public StockMovementReason Reason { get; set; }
    public int QuantityChange { get; set; }
    public int StockAfter { get; set; }
    public Guid? RelatedPurchaseOrderId { get; set; }
    public Guid? RelatedInvoiceId { get; set; }
    public string? Note { get; set; }
}

public enum StockMovementReason { Sale = 0, PoReceipt = 1, Adjustment = 2, Return = 3, Wastage = 4, Transfer = 5 }
