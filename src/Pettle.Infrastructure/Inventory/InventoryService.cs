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

    public async Task<PagedResult<SkuListItem>> ListSkusAsync(string? search, bool? lowStock, bool? inAppStore, Guid? categoryId, int page, int pageSize, bool withVariants = false, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return Empty<SkuListItem>(page, pageSize);
        var q = _db.Skus.AsNoTracking().Include(s => s.Category).Include(s => s.Brand).Where(s => s.TenantId == _user.TenantId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            q = q.Where(x => x.Name.ToLower().Contains(s) || x.Code.ToLower().Contains(s));
        }
        if (lowStock == true) q = q.Where(x => x.ReorderLevel > 0 && x.StockOnHand <= x.ReorderLevel);
        if (inAppStore == true) q = q.Where(x => x.IsListedInApp);
        if (categoryId.HasValue) q = q.Where(x => x.CategoryId == categoryId.Value);

        var total = await q.CountAsync(ct);
        var p = Math.Max(page, 1); var sz = Math.Clamp(pageSize, 1, 200);
        var items = await q.OrderBy(x => x.Name).Skip((p - 1) * sz).Take(sz)
            .Select(x => new SkuListItem(x.Id, x.Code, x.Name, x.Category != null ? x.Category.Name : null, x.Unit, x.SellingPrice, x.CostPrice, x.TaxPercent,
                x.StockOnHand, x.ReorderLevel, x.NearestExpiry, x.IsActive, x.IsListedInApp, x.AppImageUrl,
                x.Description, x.CategoryId, x.MrpPrice, x.HsnSacCode, x.TrackExpiry,
                x.BrandId, x.Brand != null ? x.Brand.Name : null, null)).ToListAsync(ct);

        if (withVariants && items.Count > 0)
        {
            var ids = items.Select(i => i.Id).ToList();
            var variantsBySku = await _db.SkuBatches.AsNoTracking()
                .Where(b => ids.Contains(b.SkuId) && b.TenantId == _user.TenantId && b.QtyRemaining > 0 && b.Mrp != null && b.Mrp > 0)
                .GroupBy(b => new { b.SkuId, b.Mrp })
                .Select(g => new { g.Key.SkuId, Mrp = g.Key.Mrp!.Value, Qty = g.Sum(b => b.QtyRemaining) })
                .ToListAsync(ct);
            var bySku = variantsBySku.GroupBy(v => v.SkuId)
                .ToDictionary(g => g.Key, g => g.Select(v => new SkuPriceVariantDto(v.Mrp, v.Qty)).ToList());
            items = items.Select(i => bySku.TryGetValue(i.Id, out var variants) && variants.Count > 1
                ? i with { PriceVariants = variants }
                : i).ToList();
        }

        return new PagedResult<SkuListItem>(items, total, p, sz);
    }

    public async Task<SkuListItem?> GetSkuAsync(Guid id, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return null;
        return await _db.Skus.AsNoTracking().Include(s => s.Category).Include(s => s.Brand)
            .Where(s => s.Id == id && s.TenantId == _user.TenantId)
            .Select(x => new SkuListItem(x.Id, x.Code, x.Name, x.Category != null ? x.Category.Name : null, x.Unit, x.SellingPrice, x.CostPrice, x.TaxPercent,
                x.StockOnHand, x.ReorderLevel, x.NearestExpiry, x.IsActive, x.IsListedInApp, x.AppImageUrl,
                x.Description, x.CategoryId, x.MrpPrice, x.HsnSacCode, x.TrackExpiry,
                x.BrandId, x.Brand != null ? x.Brand.Name : null, null))
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
            BrandId = req.BrandId,
            Unit = req.Unit, MrpPrice = req.MrpPrice, SellingPrice = req.SellingPrice, CostPrice = req.CostPrice,
            TaxPercent = req.TaxPercent, HsnSacCode = req.HsnSacCode, ReorderLevel = req.ReorderLevel,
            TrackExpiry = req.TrackExpiry, IsActive = req.IsActive,
            AppImageUrl = req.ImageUrl
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
        sku.Code = req.Code; sku.Name = req.Name; sku.Description = req.Description;
        sku.CategoryId = req.CategoryId; sku.BrandId = req.BrandId;
        sku.Unit = req.Unit; sku.MrpPrice = req.MrpPrice; sku.SellingPrice = req.SellingPrice; sku.CostPrice = req.CostPrice;
        sku.TaxPercent = req.TaxPercent; sku.HsnSacCode = req.HsnSacCode; sku.ReorderLevel = req.ReorderLevel;
        sku.TrackExpiry = req.TrackExpiry; sku.IsActive = req.IsActive;
        sku.AppImageUrl = req.ImageUrl;
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
            .Select(x => new VendorListItem(x.Id, x.Name, x.Phone, x.Email, x.Gstin, x.CreditDays, x.CreditLimit, x.IsActive, x.Address, x.ContactPerson))
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
        return new VendorListItem(v.Id, v.Name, v.Phone, v.Email, v.Gstin, v.CreditDays, v.CreditLimit, v.IsActive, v.Address, v.ContactPerson);
    }

    public async Task<VendorListItem?> UpdateVendorAsync(Guid id, CreateOrUpdateVendorRequest req, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return null;
        var v = await _db.Vendors.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == _user.TenantId, ct);
        if (v is null) return null;
        v.Name = req.Name; v.ContactPerson = req.ContactPerson; v.Phone = req.Phone; v.Email = req.Email;
        v.Address = req.Address; v.Gstin = req.Gstin; v.CreditDays = req.CreditDays; v.CreditLimit = req.CreditLimit; v.IsActive = req.IsActive;
        await _db.SaveChangesAsync(ct);
        return new VendorListItem(v.Id, v.Name, v.Phone, v.Email, v.Gstin, v.CreditDays, v.CreditLimit, v.IsActive, v.Address, v.ContactPerson);
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

    public async Task<PoDetail?> UpdatePoAsync(Guid id, CreatePoRequest req, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return null;
        var po = await _db.PurchaseOrders
            .FirstOrDefaultAsync(p => p.Id == id && p.TenantId == _user.TenantId, ct);
        if (po is null) return null;
        if (po.Status != PoStatus.Draft)
            throw AppException.BusinessRule("Only Draft purchase orders can be edited. Once items are received the bill is locked.");

        // Update header
        po.VendorId = req.VendorId;
        po.PurchaseDate = req.PurchaseDate;
        po.VendorInvoiceNumber = req.VendorInvoiceNumber;
        po.ReferenceBillNumber = req.ReferenceBillNumber;
        po.MaterialInwardNo = req.MaterialInwardNo;
        po.PaymentTerm = req.PaymentTerm;
        po.DueDate = req.DueDate;
        po.ShippingDate = req.ShippingDate;
        po.ReverseCharge = req.ReverseCharge;
        po.ExportSez = req.ExportSez;
        po.TaxType = req.TaxType;
        po.AccountLedger = req.AccountLedger;
        po.AdditionalCharges = req.AdditionalCharges;
        po.Adjustment = req.Adjustment;
        po.Notes = req.Notes;

        // Replace lines (ExecuteDelete bypasses change tracker; safe before SaveChanges)
        await _db.PurchaseOrderLines.Where(l => l.PurchaseOrderId == id).ExecuteDeleteAsync(ct);

        var inclusiveTax = string.Equals(req.TaxType, "Inclusive", StringComparison.OrdinalIgnoreCase);
        decimal grossAmount = 0, lineDiscountTotal = 0, sumTaxable = 0, sumTax = 0;
        foreach (var line in req.Lines)
        {
            var gross = line.Quantity * line.UnitCost;
            var disc1 = gross * (line.PurDisc1Percent / 100m);
            var disc2 = (gross - disc1) * (line.PurDisc2Percent / 100m);
            var net = gross - disc1 - disc2;
            decimal taxable, taxAmt;
            if (inclusiveTax) { taxable = net / (1 + line.TaxPercent / 100m); taxAmt = net - taxable; }
            else               { taxable = net; taxAmt = net * (line.TaxPercent / 100m); }
            var lineTotal = taxable + taxAmt;
            var units = line.Quantity + line.FreeQuantity;
            var landingCost = units > 0 ? lineTotal / units : 0m;

            _db.PurchaseOrderLines.Add(new PurchaseOrderLine
            {
                PurchaseOrderId = id,
                TenantId = _user.TenantId.Value,
                SkuId = line.SkuId, ItemCode = line.ItemCode, ItemName = line.ItemName, Unit = line.Unit,
                Quantity = line.Quantity, FreeQuantity = line.FreeQuantity, UnitCost = line.UnitCost,
                Mrp = line.Mrp, SellingPrice = line.SellingPrice,
                PurDisc1Percent = line.PurDisc1Percent, PurDisc2Percent = line.PurDisc2Percent,
                TaxPercent = line.TaxPercent, TaxableAmount = R(taxable), TaxAmount = R(taxAmt),
                LandingCost = R(landingCost), LineTotal = R(lineTotal),
                ExpiryDate = line.ExpiryDate, BatchNumber = line.BatchNumber,
            });

            grossAmount += gross; lineDiscountTotal += disc1 + disc2; sumTaxable += taxable; sumTax += taxAmt;
        }

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
        po.Due = Math.Max(0, R(rounded) - po.Paid);
        po.NumberOfItems = req.Lines.Count;

        await _db.SaveChangesAsync(ct);
        return await GetPoAsync(id, ct);
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
                    if (delta > 0 && line.LandingCost > 0)
                        sku.CostPrice = line.LandingCost;

                    _db.StockMovements.Add(new StockMovement
                    {
                        SkuId = sku.Id, Reason = StockMovementReason.PoReceipt,
                        QuantityChange = (int)delta, StockAfter = sku.StockOnHand,
                        RelatedPurchaseOrderId = po.Id, Note = $"PO {po.PoNumber} receipt"
                    });

                    if (delta > 0)
                    {
                        // Auto-generate batch number if user left it blank
                        var lineIdx = po.Lines.ToList().IndexOf(line) + 1;
                        var autoBatch = string.IsNullOrWhiteSpace(line.BatchNumber)
                            ? $"{po.PoNumber}-L{lineIdx:D2}"
                            : line.BatchNumber.Trim();
                        line.BatchNumber = autoBatch; // persist back to PO line for visibility

                        _db.SkuBatches.Add(new SkuBatch
                        {
                            TenantId = _user.TenantId.Value,
                            SkuId = sku.Id,
                            BatchNumber = autoBatch,
                            ExpiryDate = line.ExpiryDate,
                            QtyRemaining = delta,
                            LandingCost = line.LandingCost > 0 ? line.LandingCost : sku.CostPrice,
                            Mrp = line.Mrp > 0 ? line.Mrp : null,
                            SellingPrice = line.SellingPrice > 0 ? line.SellingPrice : null,
                            PurchaseOrderId = po.Id,
                            Source = "PoReceipt",
                            ReceivedAt = DateTimeOffset.UtcNow,
                        });
                        sku.NearestExpiry = await GetNearestExpiryAsync(sku.Id, _user.TenantId.Value, ct);
                    }
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

    public async Task<bool> RecordPoPaymentAsync(Guid id, RecordPoPaymentRequest req, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return false;
        var po = await _db.PurchaseOrders.FirstOrDefaultAsync(p => p.Id == id && p.TenantId == _user.TenantId, ct);
        if (po is null) return false;
        if (req.Amount <= 0)
            throw AppException.Validation("Invalid amount",
                new Dictionary<string, string[]> { ["amount"] = new[] { "Amount must be greater than zero." } });
        if (req.Amount > po.Due + 0.01m)
            throw AppException.Validation("Payment exceeds due",
                new Dictionary<string, string[]> { ["amount"] = new[] { $"Payment ₹{req.Amount:F2} exceeds amount due ₹{po.Due:F2}." } });
        po.Paid += req.Amount;
        po.Due = Math.Max(0, po.Total - po.Paid);
        po.PaymentStatus = po.Due == 0 ? PoPaymentStatus.Paid : PoPaymentStatus.PartiallyPaid;
        if (!string.IsNullOrWhiteSpace(req.Notes))
            po.Notes = (po.Notes is null ? "" : po.Notes + " | ") + $"Paid ₹{req.Amount:F2} via {req.Mode}: {req.Notes}";
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DeletePoAsync(Guid id, bool force = false, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return false;

        // Load PO without navigation properties — avoids EF tracking conflict with ExecuteDeleteAsync later
        var po = await _db.PurchaseOrders
            .FirstOrDefaultAsync(p => p.Id == id && p.TenantId == _user.TenantId, ct);
        if (po is null) return false;

        if (po.Status is PoStatus.Received or PoStatus.PartiallyReceived)
        {
            if (!force)
                throw AppException.Conflict($"Cannot delete {po.PoNumber} — stock has already been received against it.");

            // Reverse the exact batches this PO created (tracked) rather than reducing
            // StockOnHand by the originally-received quantity — some of that stock may already
            // have been sold since receipt, in which case only what's still sitting in the batch
            // (QtyRemaining) should come back off StockOnHand, and the reversal must be logged so
            // the batch ledger and StockOnHand never silently drift apart.
            var poBatches = await _db.SkuBatches
                .Where(b => b.PurchaseOrderId == po.Id && b.TenantId == _user.TenantId && b.QtyRemaining > 0)
                .ToListAsync(ct);

            if (poBatches.Count > 0)
            {
                var skuIds = poBatches.Select(b => b.SkuId).Distinct().ToList();
                var skus = await _db.Skus.Where(s => skuIds.Contains(s.Id)).ToListAsync(ct);
                var skuMap = skus.ToDictionary(s => s.Id);
                foreach (var batch in poBatches)
                {
                    if (!skuMap.TryGetValue(batch.SkuId, out var sku)) continue;
                    var qty = (int)Math.Round(batch.QtyRemaining);
                    sku.StockOnHand = Math.Max(0, sku.StockOnHand - qty);
                    _db.StockMovements.Add(new StockMovement
                    {
                        TenantId = _user.TenantId!.Value,
                        SkuId = sku.Id,
                        Reason = StockMovementReason.Adjustment,
                        QuantityChange = -qty,
                        StockAfter = sku.StockOnHand,
                        Note = $"PO {po.PoNumber} force-deleted — batch {batch.BatchNumber ?? batch.Id.ToString()} reversed",
                    });
                    batch.QtyRemaining = 0;
                }
                await _db.SaveChangesAsync(ct); // persist stock reversal before deleting lines
            }
        }

        // Hard-delete lines (soft-delete interceptor won't cascade to children)
        await _db.PurchaseOrderLines
            .Where(l => l.PurchaseOrderId == po.Id)
            .ExecuteDeleteAsync(ct);

        _db.PurchaseOrders.Remove(po);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<SkuCategoryDto>> ListCategoriesAsync(CancellationToken ct = default)
    {
        if (_user.TenantId is null) return Array.Empty<SkuCategoryDto>();

        var cats = await _db.SkuCategories
            .Where(c => c.TenantId == _user.TenantId)
            .Include(c => c.Parent)
            .OrderBy(c => c.ParentId == null ? 0 : 1).ThenBy(c => c.Name)
            .ToListAsync(ct);

        var skuCounts = await _db.Skus
            .Where(s => s.TenantId == _user.TenantId && s.CategoryId != null)
            .GroupBy(s => s.CategoryId!)
            .Select(g => new { CategoryId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.CategoryId, x => x.Count, ct);

        return cats.Select(c => new SkuCategoryDto(
            c.Id, c.Name, c.ParentId, c.Parent?.Name,
            skuCounts.TryGetValue(c.Id, out var cnt) ? cnt : 0
        )).ToList();
    }

    public async Task<SkuCategoryDto> CreateCategoryAsync(CreateOrUpdateCategoryRequest req, CancellationToken ct = default)
    {
        if (_user.TenantId is null) throw AppException.Forbidden();
        var name = req.Name.Trim();
        var dup = await _db.SkuCategories.AnyAsync(c => c.TenantId == _user.TenantId && c.ParentId == req.ParentId && c.Name == name, ct);
        if (dup) throw AppException.Conflict($"Category '{name}' already exists at this level.");

        var cat = new SkuCategory { TenantId = _user.TenantId.Value, Name = name, ParentId = req.ParentId };
        _db.SkuCategories.Add(cat);
        await _db.SaveChangesAsync(ct);

        string? parentName = null;
        if (req.ParentId.HasValue)
            parentName = await _db.SkuCategories.Where(c => c.Id == req.ParentId.Value).Select(c => c.Name).FirstOrDefaultAsync(ct);

        return new SkuCategoryDto(cat.Id, cat.Name, cat.ParentId, parentName, 0);
    }

    public async Task<SkuCategoryDto?> UpdateCategoryAsync(Guid id, CreateOrUpdateCategoryRequest req, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return null;
        var cat = await _db.SkuCategories.FirstOrDefaultAsync(c => c.Id == id && c.TenantId == _user.TenantId, ct);
        if (cat is null) return null;

        var name = req.Name.Trim();
        if (req.ParentId == id) throw AppException.BusinessRule("A category cannot be its own parent.");

        var dup = await _db.SkuCategories.AnyAsync(c => c.TenantId == _user.TenantId && c.ParentId == req.ParentId && c.Name == name && c.Id != id, ct);
        if (dup) throw AppException.Conflict($"Category '{name}' already exists at this level.");

        cat.Name = name;
        cat.ParentId = req.ParentId;
        await _db.SaveChangesAsync(ct);

        var skuCount = await _db.Skus.CountAsync(s => s.TenantId == _user.TenantId && s.CategoryId == id, ct);
        string? parentName = null;
        if (req.ParentId.HasValue)
            parentName = await _db.SkuCategories.Where(c => c.Id == req.ParentId.Value).Select(c => c.Name).FirstOrDefaultAsync(ct);

        return new SkuCategoryDto(cat.Id, cat.Name, cat.ParentId, parentName, skuCount);
    }

    public async Task<bool> DeleteCategoryAsync(Guid id, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return false;
        var cat = await _db.SkuCategories.Include(c => c.Children)
            .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == _user.TenantId, ct);
        if (cat is null) return false;

        if (cat.Children.Count > 0)
            throw AppException.BusinessRule("Cannot delete a category that has sub-categories. Remove the sub-categories first.");

        // Block if any ACTIVE SKUs are still assigned to this category.
        var inUse = await _db.Skus.AnyAsync(s => s.TenantId == _user.TenantId && s.CategoryId == id, ct);
        if (inUse)
            throw AppException.BusinessRule("Cannot delete a category that has SKUs assigned to it. Reassign or delete the SKUs first.");

        // Soft-deleted SKUs still hold the FK in the DB even though they're invisible to queries.
        // Null out their CategoryId so the hard-delete of the category row doesn't hit a FK violation.
        await _db.Skus.IgnoreQueryFilters()
            .Where(s => s.TenantId == _user.TenantId && s.CategoryId == id)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.CategoryId, (Guid?)null), ct);

        _db.SkuCategories.Remove(cat);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<SkuBrandDto>> ListBrandsAsync(CancellationToken ct = default)
    {
        if (_user.TenantId is null) return Array.Empty<SkuBrandDto>();
        return await _db.SkuBrands
            .Where(b => b.TenantId == _user.TenantId)
            .OrderBy(b => b.Name)
            .Select(b => new SkuBrandDto(b.Id, b.Name))
            .ToListAsync(ct);
    }

    public async Task<SkuBrandDto> CreateBrandAsync(CreateOrUpdateBrandRequest req, CancellationToken ct = default)
    {
        if (_user.TenantId is null) throw AppException.Forbidden();
        var name = req.Name.Trim();
        var dup = await _db.SkuBrands.AnyAsync(b => b.TenantId == _user.TenantId && b.Name == name, ct);
        if (dup) throw AppException.Conflict($"Brand '{name}' already exists.");
        var brand = new SkuBrand { TenantId = _user.TenantId.Value, Name = name };
        _db.SkuBrands.Add(brand);
        await _db.SaveChangesAsync(ct);
        return new SkuBrandDto(brand.Id, brand.Name);
    }

    public async Task<SkuBrandDto?> UpdateBrandAsync(Guid id, CreateOrUpdateBrandRequest req, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return null;
        var brand = await _db.SkuBrands.FirstOrDefaultAsync(b => b.Id == id && b.TenantId == _user.TenantId, ct);
        if (brand is null) return null;
        brand.Name = req.Name.Trim();
        await _db.SaveChangesAsync(ct);
        return new SkuBrandDto(brand.Id, brand.Name);
    }

    public async Task<bool> DeleteBrandAsync(Guid id, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return false;
        var brand = await _db.SkuBrands.FirstOrDefaultAsync(b => b.Id == id && b.TenantId == _user.TenantId, ct);
        if (brand is null) return false;
        var inUse = await _db.Skus.AnyAsync(s => s.TenantId == _user.TenantId && s.BrandId == id, ct);
        if (inUse) throw AppException.BusinessRule("Cannot delete a brand that has SKUs assigned to it.");
        await _db.Skus.IgnoreQueryFilters()
            .Where(s => s.TenantId == _user.TenantId && s.BrandId == id)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.BrandId, (Guid?)null), ct);
        _db.SkuBrands.Remove(brand);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> CreateStockAdjustmentAsync(CreateStockAdjustmentRequest req, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return false;
        if (req.Lines.Count == 0) return false;

        var skuIds = req.Lines.Select(l => l.SkuId).Distinct().ToList();
        var skus = await _db.Skus
            .Where(s => s.TenantId == _user.TenantId && skuIds.Contains(s.Id))
            .ToListAsync(ct);

        var reason = req.AdjustmentType switch
        {
            ManualAdjustmentType.Procurement => StockMovementReason.Procurement,
            ManualAdjustmentType.SelfConsumption => StockMovementReason.SelfConsumption,
            ManualAdjustmentType.Damage => StockMovementReason.Damage,
            _ => StockMovementReason.Adjustment,
        };

        foreach (var line in req.Lines)
        {
            var sku = skus.FirstOrDefault(s => s.Id == line.SkuId);
            if (sku is null) continue;

            // Procurement always adds; SelfConsumption/Damage always deduct (user enters positive qty);
            // Adjustment uses the sign the user provided (positive = add, negative = reduce).
            var delta = req.AdjustmentType switch
            {
                ManualAdjustmentType.Procurement    =>  line.Quantity,
                ManualAdjustmentType.SelfConsumption => -Math.Abs(line.Quantity),
                ManualAdjustmentType.Damage          => -Math.Abs(line.Quantity),
                _                                    =>  line.Quantity, // Adjustment: honour sign
            };

            if (delta > 0)
            {
                _db.SkuBatches.Add(new SkuBatch
                {
                    TenantId = _user.TenantId.Value,
                    SkuId = sku.Id,
                    QtyRemaining = delta,
                    LandingCost = sku.CostPrice,
                    Source = req.AdjustmentType == ManualAdjustmentType.Procurement ? "Procurement" : "Adjustment",
                    ReceivedAt = DateTimeOffset.UtcNow,
                });
            }
            else if (delta < 0)
            {
                // FIFO best-effort deduction (adjustment can override, so we don't throw on shortfall)
                await FifoBatchDeductor.DeductAsync(_db, sku.Id, _user.TenantId.Value, Math.Abs(delta), ct);
            }

            sku.StockOnHand = Math.Max(0, sku.StockOnHand + delta);
            sku.NearestExpiry = await GetNearestExpiryAsync(sku.Id, _user.TenantId.Value, ct);

            _db.StockMovements.Add(new StockMovement
            {
                TenantId = _user.TenantId.Value,
                SkuId = sku.Id,
                Reason = reason,
                QuantityChange = delta,
                StockAfter = sku.StockOnHand,
                Note = string.IsNullOrWhiteSpace(line.LineNote)
                    ? req.Notes
                    : (string.IsNullOrWhiteSpace(req.Notes) ? line.LineNote : $"{req.Notes} | {line.LineNote}"),
            });
        }

        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<PagedResult<StockMovementDto>> ListMovementsAsync(Guid skuId, string? reason, int page, int pageSize, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return Empty<StockMovementDto>(page, pageSize);
        page = Math.Max(page, 1); pageSize = Math.Clamp(pageSize, 1, 200);

        var q = _db.StockMovements
            .Include(m => m.Sku)
            .Where(m => m.TenantId == _user.TenantId && m.SkuId == skuId)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(reason) &&
            Enum.TryParse<StockMovementReason>(reason, ignoreCase: true, out var reasonEnum))
            q = q.Where(m => m.Reason == reasonEnum);

        var ordered = q.OrderByDescending(m => m.CreatedAt);
        var total = await ordered.CountAsync(ct);
        var items = await ordered.Skip((page - 1) * pageSize).Take(pageSize)
            .Select(m => new StockMovementDto(
                m.Id, m.Sku!.Name, m.Sku.Code, m.Reason.ToString(),
                m.QuantityChange, m.StockAfter, m.CreatedAt, m.Note))
            .ToListAsync(ct);

        return new PagedResult<StockMovementDto>(items, total, page, pageSize);
    }

    public async Task<IReadOnlyList<SkuBatchDto>> ListBatchesAsync(Guid skuId, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return Array.Empty<SkuBatchDto>();
        var batches = await _db.SkuBatches
            .Where(b => b.SkuId == skuId && b.TenantId == _user.TenantId)
            .OrderBy(b => b.ReceivedAt)
            .ToListAsync(ct);
        return batches.Select(b => new SkuBatchDto(
            b.Id, b.BatchNumber, b.ExpiryDate, b.QtyRemaining, b.LandingCost, b.Source, b.ReceivedAt,
            b.Mrp, b.SellingPrice)).ToList();
    }

    private async Task<DateOnly?> GetNearestExpiryAsync(Guid skuId, Guid tenantId, CancellationToken ct)
        => await _db.SkuBatches
            .Where(b => b.SkuId == skuId && b.TenantId == tenantId && b.QtyRemaining > 0 && b.ExpiryDate != null)
            .OrderBy(b => b.ExpiryDate)
            .Select(b => b.ExpiryDate)
            .FirstOrDefaultAsync(ct);

    private async Task<string> NextPoNumberAsync(CancellationToken ct)
    {
        var year = DateTime.UtcNow.Year;
        var count = await _db.PurchaseOrders.IgnoreQueryFilters()
            .Where(p => p.TenantId == _user.TenantId && p.CreatedAt.Year == year).CountAsync(ct);
        return $"PO-{year}-{(count + 1).ToString().PadLeft(5, '0')}";
    }

    private static PagedResult<T> Empty<T>(int page, int pageSize) => new(Array.Empty<T>(), 0, Math.Max(page, 1), Math.Clamp(pageSize, 1, 200));
}
