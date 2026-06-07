using Microsoft.EntityFrameworkCore;
using Pettle.Application.Clients;
using Pettle.Application.Common;
using Pettle.Application.Common.Errors;
using Pettle.Application.Inventory;
using Pettle.Domain.Inventory;
using Pettle.Infrastructure.Persistence;

namespace Pettle.Infrastructure.Inventory;

public class InventoryService : IInventoryService
{
    private readonly PettleDbContext _db;
    private readonly ICurrentUser _user;

    public InventoryService(PettleDbContext db, ICurrentUser user) { _db = db; _user = user; }

    public async Task<PagedResult<SkuListItem>> ListSkusAsync(string? search, bool? lowStock, bool? inAppStore, int page, int pageSize, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return Empty<SkuListItem>(page, pageSize);
        var q = _db.Skus.AsNoTracking().Include(s => s.Category).Where(s => s.TenantId == _user.TenantId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            q = q.Where(x => x.Name.ToLower().Contains(s) || x.Code.ToLower().Contains(s));
        }
        if (lowStock == true) q = q.Where(x => x.StockOnHand <= x.ReorderLevel);
        if (inAppStore == true) q = q.Where(x => x.IsListedInApp);

        var total = await q.CountAsync(ct);
        var p = Math.Max(page, 1); var sz = Math.Clamp(pageSize, 1, 200);
        var items = await q.OrderBy(x => x.Name).Skip((p - 1) * sz).Take(sz)
            .Select(x => new SkuListItem(x.Id, x.Code, x.Name, x.Category!.Name, x.Unit, x.SellingPrice, x.CostPrice, x.TaxPercent,
                x.StockOnHand, x.ReorderLevel, x.NearestExpiry, x.IsActive, x.IsListedInApp, x.AppImageUrl)).ToListAsync(ct);
        return new PagedResult<SkuListItem>(items, total, p, sz);
    }

    public async Task<SkuListItem?> GetSkuAsync(Guid id, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return null;
        return await _db.Skus.AsNoTracking().Include(s => s.Category)
            .Where(s => s.Id == id && s.TenantId == _user.TenantId)
            .Select(x => new SkuListItem(x.Id, x.Code, x.Name, x.Category!.Name, x.Unit, x.SellingPrice, x.CostPrice, x.TaxPercent,
                x.StockOnHand, x.ReorderLevel, x.NearestExpiry, x.IsActive, x.IsListedInApp, x.AppImageUrl))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<SkuListItem?> UpdateSkuListingAsync(Guid id, UpdateSkuListingRequest req, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return null;
        var sku = await _db.Skus.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == _user.TenantId, ct);
        if (sku is null) return null;
        sku.IsListedInApp = req.IsListedInApp;
        sku.AppImageUrl = req.AppImageUrl;
        await _db.SaveChangesAsync(ct);
        return await GetSkuAsync(id, ct);
    }

    public async Task<SkuListItem> CreateSkuAsync(CreateOrUpdateSkuRequest req, CancellationToken ct = default)
    {
        if (_user.TenantId is null) throw AppException.Forbidden();
        var code = req.Code.Trim();
        var dup = await _db.Skus.IgnoreQueryFilters()
            .AnyAsync(s => s.TenantId == _user.TenantId && s.Code == code, ct);
        if (dup) throw AppException.Conflict($"SKU code '{code}' is already in use.");

        var sku = new Sku
        {
            Code = code, Name = req.Name.Trim(), Description = req.Description, CategoryId = req.CategoryId,
            Unit = req.Unit, MrpPrice = req.MrpPrice, SellingPrice = req.SellingPrice, CostPrice = req.CostPrice,
            TaxPercent = req.TaxPercent, HsnSacCode = req.HsnSacCode, ReorderLevel = req.ReorderLevel,
            TrackExpiry = req.TrackExpiry, IsActive = req.IsActive
        };
        _db.Skus.Add(sku);
        await _db.SaveChangesAsync(ct);
        return (await GetSkuAsync(sku.Id, ct))!;
    }

    public async Task<SkuListItem?> UpdateSkuAsync(Guid id, CreateOrUpdateSkuRequest req, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return null;
        var sku = await _db.Skus.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == _user.TenantId, ct);
        if (sku is null) return null;
        sku.Code = req.Code; sku.Name = req.Name; sku.Description = req.Description; sku.CategoryId = req.CategoryId;
        sku.Unit = req.Unit; sku.MrpPrice = req.MrpPrice; sku.SellingPrice = req.SellingPrice; sku.CostPrice = req.CostPrice;
        sku.TaxPercent = req.TaxPercent; sku.HsnSacCode = req.HsnSacCode; sku.ReorderLevel = req.ReorderLevel;
        sku.TrackExpiry = req.TrackExpiry; sku.IsActive = req.IsActive;
        await _db.SaveChangesAsync(ct);
        return await GetSkuAsync(id, ct);
    }

    public async Task<PagedResult<VendorListItem>> ListVendorsAsync(string? search, int page, int pageSize, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return Empty<VendorListItem>(page, pageSize);
        var q = _db.Vendors.AsNoTracking().Where(v => v.TenantId == _user.TenantId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            q = q.Where(x => x.Name.ToLower().Contains(s) || (x.Phone != null && x.Phone.Contains(s)));
        }
        var total = await q.CountAsync(ct);
        var p = Math.Max(page, 1); var sz = Math.Clamp(pageSize, 1, 200);
        var items = await q.OrderBy(x => x.Name).Skip((p - 1) * sz).Take(sz)
            .Select(x => new VendorListItem(x.Id, x.Name, x.Phone, x.Email, x.Gstin, x.CreditDays, x.CreditLimit, x.IsActive))
            .ToListAsync(ct);
        return new PagedResult<VendorListItem>(items, total, p, sz);
    }

    public async Task<VendorListItem> CreateVendorAsync(CreateOrUpdateVendorRequest req, CancellationToken ct = default)
    {
        var v = new Vendor
        {
            Name = req.Name, ContactPerson = req.ContactPerson, Phone = req.Phone, Email = req.Email,
            Address = req.Address, Gstin = req.Gstin, CreditDays = req.CreditDays, CreditLimit = req.CreditLimit, IsActive = req.IsActive
        };
        _db.Vendors.Add(v);
        await _db.SaveChangesAsync(ct);
        return new VendorListItem(v.Id, v.Name, v.Phone, v.Email, v.Gstin, v.CreditDays, v.CreditLimit, v.IsActive);
    }

    public async Task<VendorListItem?> UpdateVendorAsync(Guid id, CreateOrUpdateVendorRequest req, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return null;
        var v = await _db.Vendors.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == _user.TenantId, ct);
        if (v is null) return null;
        v.Name = req.Name; v.ContactPerson = req.ContactPerson; v.Phone = req.Phone; v.Email = req.Email;
        v.Address = req.Address; v.Gstin = req.Gstin; v.CreditDays = req.CreditDays; v.CreditLimit = req.CreditLimit; v.IsActive = req.IsActive;
        await _db.SaveChangesAsync(ct);
        return new VendorListItem(v.Id, v.Name, v.Phone, v.Email, v.Gstin, v.CreditDays, v.CreditLimit, v.IsActive);
    }

    public async Task<PagedResult<PoListItem>> ListPosAsync(string? search, int page, int pageSize, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return Empty<PoListItem>(page, pageSize);
        var q = _db.PurchaseOrders.AsNoTracking().Include(p => p.Vendor).Where(p => p.TenantId == _user.TenantId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            q = q.Where(x => x.PoNumber.ToLower().Contains(s) || x.Vendor!.Name.ToLower().Contains(s));
        }
        var total = await q.CountAsync(ct);
        var pg = Math.Max(page, 1); var sz = Math.Clamp(pageSize, 1, 200);
        var items = await q.OrderByDescending(x => x.PurchaseDate).Skip((pg - 1) * sz).Take(sz)
            .Select(x => new PoListItem(x.Id, x.LegacyPoNumber, x.PoNumber, x.Vendor!.Name, x.Status, x.PurchaseDate,
                x.VendorInvoiceNumber, x.PaymentStatus, x.NumberOfItems, x.Total, x.Paid, x.Due)).ToListAsync(ct);
        return new PagedResult<PoListItem>(items, total, pg, sz);
    }

    public async Task<PoDetail?> GetPoAsync(Guid id, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return null;
        var po = await _db.PurchaseOrders.AsNoTracking()
            .Include(p => p.Vendor).Include(p => p.Lines)
            .FirstOrDefaultAsync(p => p.Id == id && p.TenantId == _user.TenantId, ct);
        if (po is null) return null;
        return new PoDetail(
            po.Id, po.PoNumber, po.VendorId, po.Vendor!.Name, po.Status, po.PurchaseDate,
            po.VendorInvoiceNumber, po.ReferenceBillNumber, po.MaterialInwardNo,
            po.PaymentTerm, po.DueDate, po.ShippingDate, po.ReverseCharge, po.ExportSez,
            po.TaxType, po.AccountLedger, po.PaymentStatus,
            po.SubTotal, po.GrossAmount, po.FlatDiscountPercent, po.FlatDiscountAmount,
            po.DiscountAmount, po.TaxableAmount, po.TaxAmount, po.AdditionalCharges,
            po.Adjustment, po.RoundOff, po.Total, po.Paid, po.Due,
            po.Notes,
            po.Lines.Select(l => new PoLineDto(
                l.Id, l.SkuId, l.ItemCode, l.ItemName, l.Unit,
                l.Quantity, l.FreeQuantity, l.ReceivedQuantity, l.UnitCost,
                l.Mrp, l.SellingPrice, l.PurDisc1Percent, l.PurDisc2Percent,
                l.TaxPercent, l.TaxableAmount, l.TaxAmount, l.LandingCost, l.LineTotal,
                l.ExpiryDate, l.BatchNumber)).ToList());
    }

    public async Task<PoDetail> CreatePoAsync(CreatePoRequest req, CancellationToken ct = default)
    {
        var po = new PurchaseOrder
        {
            PoNumber = await NextPoNumberAsync(ct),
            VendorId = req.VendorId,
            PurchaseDate = req.PurchaseDate,
            VendorInvoiceNumber = req.VendorInvoiceNumber,
            ReferenceBillNumber = req.ReferenceBillNumber,
            MaterialInwardNo = req.MaterialInwardNo,
            PaymentTerm = req.PaymentTerm,
            DueDate = req.DueDate,
            ShippingDate = req.ShippingDate,
            ReverseCharge = req.ReverseCharge,
            ExportSez = req.ExportSez,
            TaxType = req.TaxType,
            AccountLedger = req.AccountLedger,
            Adjustment = req.Adjustment,
            AdditionalCharges = req.AdditionalCharges,
            Notes = req.Notes,
            Status = PoStatus.Draft
        };

        // "Inclusive" => the supplier's unit cost already contains tax; otherwise tax is added on top.
        var inclusiveTax = string.Equals(req.TaxType, "Inclusive", StringComparison.OrdinalIgnoreCase);

        decimal grossAmount = 0, lineDiscountTotal = 0, sumTaxable = 0, sumTax = 0;
        foreach (var line in req.Lines)
        {
            var gross = line.Quantity * line.UnitCost;
            var disc1 = gross * (line.PurDisc1Percent / 100m);
            var disc2 = (gross - disc1) * (line.PurDisc2Percent / 100m);
            var net = gross - disc1 - disc2;

            decimal taxable, taxAmt;
            if (inclusiveTax)
            {
                taxable = net / (1 + line.TaxPercent / 100m);
                taxAmt = net - taxable;
            }
            else
            {
                taxable = net;
                taxAmt = net * (line.TaxPercent / 100m);
            }
            var lineTotal = taxable + taxAmt;

            // Landed cost is spread across all units actually received, including free quantity.
            var units = line.Quantity + line.FreeQuantity;
            var landingCost = units > 0 ? lineTotal / units : 0m;

            po.Lines.Add(new PurchaseOrderLine
            {
                SkuId = line.SkuId, ItemCode = line.ItemCode, ItemName = line.ItemName, Unit = line.Unit,
                Quantity = line.Quantity, FreeQuantity = line.FreeQuantity, UnitCost = line.UnitCost,
                Mrp = line.Mrp, SellingPrice = line.SellingPrice,
                PurDisc1Percent = line.PurDisc1Percent, PurDisc2Percent = line.PurDisc2Percent,
                TaxPercent = line.TaxPercent, TaxableAmount = R(taxable), TaxAmount = R(taxAmt),
                LandingCost = R(landingCost), LineTotal = R(lineTotal),
                ExpiryDate = line.ExpiryDate, BatchNumber = line.BatchNumber
            });

            grossAmount += gross;
            lineDiscountTotal += disc1 + disc2;
            sumTaxable += taxable;
            sumTax += taxAmt;
        }

        // Flat (bill-level) discount applies on the post-line-discount taxable value; tax scales proportionally.
        var flatDiscountAmount = sumTaxable * (req.FlatDiscountPercent / 100m);
        var finalTaxable = sumTaxable - flatDiscountAmount;
        var finalTax = sumTaxable > 0 ? sumTax * (finalTaxable / sumTaxable) : 0m;

        var rawTotal = finalTaxable + finalTax + req.AdditionalCharges + req.Adjustment;
        var rounded = Math.Round(rawTotal, MidpointRounding.AwayFromZero);

        po.GrossAmount = R(grossAmount);
        po.FlatDiscountPercent = req.FlatDiscountPercent;
        po.FlatDiscountAmount = R(flatDiscountAmount);
        po.DiscountAmount = R(lineDiscountTotal + flatDiscountAmount);
        po.SubTotal = R(sumTaxable);
        po.TaxableAmount = R(finalTaxable);
        po.TaxAmount = R(finalTax);
        po.RoundOff = R(rounded - rawTotal);
        po.Total = R(rounded);
        po.Due = po.Total;
        po.NumberOfItems = po.Lines.Count;
        _db.PurchaseOrders.Add(po);
        await _db.SaveChangesAsync(ct);
        return (await GetPoAsync(po.Id, ct))!;
    }

    private static decimal R(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    public async Task<bool> ReceivePoAsync(Guid id, ReceivePoRequest req, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return false;
        var po = await _db.PurchaseOrders.Include(p => p.Lines).FirstOrDefaultAsync(p => p.Id == id && p.TenantId == _user.TenantId, ct);
        if (po is null) return false;

        foreach (var lineUpd in req.Lines)
        {
            var line = po.Lines.FirstOrDefault(l => l.Id == lineUpd.LineId);
            if (line is null)
                throw AppException.Validation("Unknown PO line",
                    new Dictionary<string, string[]> { ["lines"] = new[] { $"Line {lineUpd.LineId} does not belong to this PO." } });
            if (lineUpd.ReceivedQuantity > line.Quantity + 0.001m)
                throw AppException.Validation("Received quantity exceeds ordered",
                    new Dictionary<string, string[]> { ["lines"] = new[] { $"Received {lineUpd.ReceivedQuantity} > ordered {line.Quantity} for '{line.ItemName}'." } });
            var delta = lineUpd.ReceivedQuantity - line.ReceivedQuantity;
            line.ReceivedQuantity = lineUpd.ReceivedQuantity;
            if (line.SkuId.HasValue && delta != 0)
            {
                var sku = await _db.Skus.FirstOrDefaultAsync(s => s.Id == line.SkuId && s.TenantId == _user.TenantId, ct);
                if (sku is not null)
                {
                    sku.StockOnHand += (int)delta;
                    // On inward receipt, refresh the SKU's cost price to the latest landed cost from this bill.
                    if (delta > 0 && line.LandingCost > 0)
                        sku.CostPrice = line.LandingCost;
                    _db.StockMovements.Add(new StockMovement
                    {
                        SkuId = sku.Id, Reason = StockMovementReason.PoReceipt,
                        QuantityChange = (int)delta, StockAfter = sku.StockOnHand,
                        RelatedPurchaseOrderId = po.Id, Note = $"PO {po.PoNumber} receipt"
                    });
                }
            }
        }
        var totalQty = po.Lines.Sum(l => l.Quantity);
        var receivedQty = po.Lines.Sum(l => l.ReceivedQuantity);
        po.Status = receivedQty >= totalQty ? PoStatus.Received : PoStatus.PartiallyReceived;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DeleteSkuAsync(Guid id, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return false;
        var sku = await _db.Skus.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == _user.TenantId, ct);
        if (sku is null) return false;
        if (sku.StockOnHand > 0)
            throw AppException.Conflict($"Cannot delete '{sku.Name}' — it still has {sku.StockOnHand} in stock. Adjust stock to zero first.");
        _db.Skus.Remove(sku);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DeleteVendorAsync(Guid id, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return false;
        var vendor = await _db.Vendors.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == _user.TenantId, ct);
        if (vendor is null) return false;
        // A soft-deleted vendor would be filtered out and break PO listings (Vendor!.Name), so block while referenced.
        var hasPos = await _db.PurchaseOrders.AnyAsync(p => p.VendorId == id && p.TenantId == _user.TenantId, ct);
        if (hasPos)
            throw AppException.Conflict($"Cannot delete '{vendor.Name}' — it is linked to one or more purchase orders.");
        _db.Vendors.Remove(vendor);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DeletePoAsync(Guid id, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return false;
        var po = await _db.PurchaseOrders.FirstOrDefaultAsync(p => p.Id == id && p.TenantId == _user.TenantId, ct);
        if (po is null) return false;
        if (po.Status is PoStatus.Received or PoStatus.PartiallyReceived)
            throw AppException.Conflict($"Cannot delete {po.PoNumber} — stock has already been received against it.");
        _db.PurchaseOrders.Remove(po);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private async Task<string> NextPoNumberAsync(CancellationToken ct)
    {
        var year = DateTime.UtcNow.Year;
        var count = await _db.PurchaseOrders.IgnoreQueryFilters()
            .Where(p => p.TenantId == _user.TenantId && p.CreatedAt.Year == year).CountAsync(ct);
        return $"PO-{year}-{(count + 1).ToString().PadLeft(5, '0')}";
    }

    private static PagedResult<T> Empty<T>(int page, int pageSize) => new(Array.Empty<T>(), 0, Math.Max(page, 1), Math.Clamp(pageSize, 1, 200));
}
