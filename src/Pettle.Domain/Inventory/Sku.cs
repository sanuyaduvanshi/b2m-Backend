using Pettle.Domain.Common;

namespace Pettle.Domain.Inventory;

public class SkuCategory : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public Guid? ParentId { get; set; }
    public SkuCategory? Parent { get; set; }
    public ICollection<SkuCategory> Children { get; set; } = new List<SkuCategory>();
}

public class SkuBrand : TenantEntity
{
    public string Name { get; set; } = string.Empty;
}

public class Sku : SoftDeletableTenantEntity
{
    public string? LegacySkuId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? CategoryId { get; set; }
    public SkuCategory? Category { get; set; }
    public Guid? BrandId { get; set; }
    public SkuBrand? Brand { get; set; }
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

    /// <summary>Purchase or debit note. A debit note is the same document in reverse — goods going
    /// back to the supplier — so it reuses this table rather than duplicating twenty identical
    /// columns, and is told apart by this one field everywhere it matters (numbering, stock
    /// direction, purchase totals).</summary>
    public PurchaseDocType DocType { get; set; } = PurchaseDocType.Purchase;
    /// <summary>The bill this debit note returns against. Free-text ReferenceBillNumber records
    /// what the supplier calls it; this is the row we can actually check the quantities against.</summary>
    public Guid? ReturnAgainstPurchaseOrderId { get; set; }
    public PurchaseOrder? ReturnAgainstPurchaseOrder { get; set; }
    /// <summary>Why the goods went back — damaged, expired, wrong item. Required on a debit note.</summary>
    public string? ReturnReason { get; set; }
    public Guid VendorId { get; set; }
    public Vendor? Vendor { get; set; }
    public PoStatus Status { get; set; } = PoStatus.Draft;
    public DateOnly PurchaseDate { get; set; }
    /// <summary>Supplier's own bill/invoice number.</summary>
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

    // --- Supplier bill (GST purchase) fields ---
    public string? ReferenceBillNumber { get; set; }
    public string? MaterialInwardNo { get; set; }
    public string? PaymentTerm { get; set; }
    public DateOnly? DueDate { get; set; }
    public DateOnly? ShippingDate { get; set; }
    public bool ReverseCharge { get; set; }
    public bool ExportSez { get; set; }
    /// <summary>Default | Inclusive | Exclusive — how line unit costs treat tax.</summary>
    public string? TaxType { get; set; }
    public string? AccountLedger { get; set; }
    public decimal FlatDiscountPercent { get; set; }
    public decimal FlatDiscountAmount { get; set; }
    public decimal AdditionalCharges { get; set; }
    public decimal GrossAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxableAmount { get; set; }
    public decimal RoundOff { get; set; }

    public ICollection<PurchaseOrderLine> Lines { get; set; } = new List<PurchaseOrderLine>();
}

public enum PoStatus { Draft = 0, Sent = 1, Received = 2, PartiallyReceived = 3, Closed = 4, Cancelled = 5 }
public enum PurchaseDocType { Purchase = 0, DebitNote = 1 }
public enum PoPaymentStatus { Unpaid = 0, PartiallyPaid = 1, Paid = 2 }

public class PurchaseOrderLine : TenantEntity
{
    public Guid PurchaseOrderId { get; set; }
    public PurchaseOrder? PurchaseOrder { get; set; }
    public Guid? SkuId { get; set; }
    public Sku? Sku { get; set; }
    public string? ItemCode { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string? Unit { get; set; }
    public decimal Quantity { get; set; }
    public decimal FreeQuantity { get; set; }
    public decimal ReceivedQuantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal Mrp { get; set; }
    public decimal SellingPrice { get; set; }
    public decimal PurDisc1Percent { get; set; }
    public decimal PurDisc2Percent { get; set; }
    public decimal TaxPercent { get; set; }
    public decimal TaxableAmount { get; set; }
    public decimal TaxAmount { get; set; }
    /// <summary>Effective per-unit landed cost (after discounts + tax), used to update SKU cost price.</summary>
    public decimal LandingCost { get; set; }
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

// Return (3) is stock coming back from a customer, so it adds. PurchaseReturn is stock going the
// other way — back to the supplier — and always removes.
public enum StockMovementReason { Sale = 0, PoReceipt = 1, Adjustment = 2, Return = 3, Wastage = 4, Transfer = 5, SelfConsumption = 6, Procurement = 7, Damage = 8, PurchaseReturn = 9 }

/// <summary>One physical batch of stock received. FIFO deduction uses ReceivedAt (see FifoBatchDeductor).</summary>
public class SkuBatch : TenantEntity
{
    public Guid SkuId { get; set; }
    public Sku? Sku { get; set; }
    public string? BatchNumber { get; set; }
    public DateOnly? ExpiryDate { get; set; }
    public decimal QtyRemaining { get; set; }
    public decimal LandingCost { get; set; }
    /// <summary>MRP from the PO line at time of receipt — used to populate sale MRP per batch.</summary>
    public decimal? Mrp { get; set; }
    /// <summary>Selling price from the PO line at time of receipt.</summary>
    public decimal? SellingPrice { get; set; }
    public Guid? PurchaseOrderId { get; set; }
    /// <summary>Opening | PoReceipt | Adjustment | Procurement | Return</summary>
    public string Source { get; set; } = "Opening";
    public DateTimeOffset ReceivedAt { get; set; } = DateTimeOffset.UtcNow;
}
