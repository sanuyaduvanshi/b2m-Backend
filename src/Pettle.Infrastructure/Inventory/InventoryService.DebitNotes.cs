using Microsoft.EntityFrameworkCore;
using Pettle.Application.Clients;
using Pettle.Application.Common.Errors;
using Pettle.Application.Inventory;
using Pettle.Domain.Inventory;

namespace Pettle.Infrastructure.Inventory;

/// <summary>Debit notes — goods going back to the supplier.
///
/// A debit note is the purchase bill in reverse, so it lives in the same table and reuses the same
/// totals maths; what differs is that stock leaves instead of arriving, and the document reduces
/// what the supplier is owed rather than adding to it. Kept in its own file so the purchase path
/// stays readable.
/// </summary>
public partial class InventoryService
{
    public async Task<PagedResult<PoListItem>> ListDebitNotesAsync(string? search, int page, int pageSize, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return new PagedResult<PoListItem>(Array.Empty<PoListItem>(), 0, Math.Max(page, 1), pageSize);
        var q = DebitNoteQuery(search);
        var total = await q.CountAsync(ct);
        var p = Math.Max(page, 1);
        var size = Math.Clamp(pageSize, 1, 200);
        var rows = await q.OrderByDescending(x => x.PurchaseDate).ThenByDescending(x => x.CreatedAt)
            .Skip((p - 1) * size).Take(size)
            .Select(x => new PoListItem(
                x.Id, x.LegacyPoNumber, x.PoNumber, x.Vendor != null ? x.Vendor.Name : "—",
                x.Status, x.PurchaseDate, x.VendorInvoiceNumber, x.PaymentStatus,
                x.NumberOfItems, x.Total, x.Paid, x.Due,
                x.DocType, x.ReturnReason,
                x.ReturnAgainstPurchaseOrder != null ? x.ReturnAgainstPurchaseOrder.PoNumber : null))
            .ToListAsync(ct);
        return new PagedResult<PoListItem>(rows, total, p, size);
    }

    public async Task<PoDetail?> GetDebitNoteAsync(Guid id, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return null;
        var exists = await _db.PurchaseOrders.AsNoTracking()
            .AnyAsync(p => p.Id == id && p.TenantId == _user.TenantId && p.DocType == PurchaseDocType.DebitNote, ct);
        return exists ? await GetPoAsync(id, ct) : null;
    }

    private IQueryable<PurchaseOrder> DebitNoteQuery(string? search)
    {
        var q = _db.PurchaseOrders.AsNoTracking()
            .Where(p => p.TenantId == _user.TenantId && p.DocType == PurchaseDocType.DebitNote);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(p => EF.Functions.ILike(p.PoNumber, $"%{s}%")
                             || (p.ReferenceBillNumber != null && EF.Functions.ILike(p.ReferenceBillNumber, $"%{s}%"))
                             || (p.Vendor != null && EF.Functions.ILike(p.Vendor.Name, $"%{s}%")));
        }
        return q;
    }

    public async Task<IReadOnlyList<ReturnablePurchase>> ListReturnablePurchasesAsync(Guid vendorId, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return Array.Empty<ReturnablePurchase>();
        var tid = _user.TenantId.Value;

        // Only what actually arrived can go back, so drafts and never-received bills are excluded.
        var bills = await _db.PurchaseOrders.AsNoTracking()
            .Include(p => p.Lines)
            .Where(p => p.TenantId == tid && p.VendorId == vendorId
                        && p.DocType == PurchaseDocType.Purchase
                        && p.Lines.Any(l => l.ReceivedQuantity > 0))
            .OrderByDescending(p => p.PurchaseDate)
            .Take(50)
            .ToListAsync(ct);
        if (bills.Count == 0) return Array.Empty<ReturnablePurchase>();

        var returned = await ReturnedQuantitiesAsync(bills.Select(b => b.Id).ToList(), ct);

        var skuIds = bills.SelectMany(b => b.Lines).Where(l => l.SkuId.HasValue).Select(l => l.SkuId!.Value).Distinct().ToList();
        var stock = skuIds.Count == 0 ? new Dictionary<Guid, int>() : await _db.Skus.AsNoTracking()
            .Where(s => s.TenantId == tid && skuIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.StockOnHand, ct);

        var result = new List<ReturnablePurchase>();
        foreach (var b in bills)
        {
            var lines = new List<ReturnableLine>();
            foreach (var l in b.Lines.Where(l => l.ReceivedQuantity > 0))
            {
                var already = returned.GetValueOrDefault((b.Id, ReturnKey(l)), 0m);
                var left = Math.Max(0, l.ReceivedQuantity - already);
                lines.Add(new ReturnableLine(
                    l.SkuId, l.ItemCode, l.ItemName, l.Unit,
                    l.ReceivedQuantity, already, left,
                    l.UnitCost, l.Mrp, l.SellingPrice,
                    l.PurDisc1Percent, l.PurDisc2Percent, l.TaxPercent,
                    l.ExpiryDate, l.BatchNumber,
                    l.SkuId.HasValue ? stock.GetValueOrDefault(l.SkuId.Value, 0) : 0));
            }
            // A bill with nothing left to send back would only clutter the picker.
            if (lines.Any(l => l.ReturnableQuantity > 0))
                result.Add(new ReturnablePurchase(b.Id, b.PoNumber, b.PurchaseDate, b.VendorInvoiceNumber,
                    b.ReferenceBillNumber, b.Total, lines));
        }
        return result;
    }

    /// <summary>Identifies a line for return-tracking. SKU where there is one, item name otherwise —
    /// the same pairing the debit note writes back, so quantities line up across documents.</summary>
    private static string ReturnKey(PurchaseOrderLine l) =>
        l.SkuId.HasValue ? l.SkuId.Value.ToString() : l.ItemName.Trim().ToLowerInvariant();

    private static string ReturnKey(Guid? skuId, string itemName) =>
        skuId.HasValue ? skuId.Value.ToString() : itemName.Trim().ToLowerInvariant();

    /// <summary>How much has already gone back per (bill, line), so a second debit note can't
    /// return the same goods twice.</summary>
    private async Task<Dictionary<(Guid, string), decimal>> ReturnedQuantitiesAsync(List<Guid> billIds, CancellationToken ct)
    {
        var rows = await _db.PurchaseOrders.AsNoTracking()
            .Where(p => p.TenantId == _user.TenantId
                        && p.DocType == PurchaseDocType.DebitNote
                        && p.ReturnAgainstPurchaseOrderId != null
                        && billIds.Contains(p.ReturnAgainstPurchaseOrderId.Value))
            .SelectMany(p => p.Lines.Select(l => new
            {
                BillId = p.ReturnAgainstPurchaseOrderId!.Value,
                l.SkuId,
                l.ItemName,
                l.Quantity,
            }))
            .ToListAsync(ct);

        return rows
            .GroupBy(r => (r.BillId, ReturnKey(r.SkuId, r.ItemName)))
            .ToDictionary(g => g.Key, g => g.Sum(r => r.Quantity));
    }

    public async Task<PoDetail> CreateDebitNoteAsync(CreateDebitNoteRequest req, CancellationToken ct = default)
    {
        if (_user.TenantId is null) throw AppException.Forbidden();
        var tid = _user.TenantId.Value;

        if (req.Lines is null || req.Lines.Count == 0)
            throw AppException.Validation("Empty debit note",
                new Dictionary<string, string[]> { ["lines"] = new[] { "Add at least one item to return." } });
        if (string.IsNullOrWhiteSpace(req.Reason))
            throw AppException.Validation("Reason required",
                new Dictionary<string, string[]> { ["reason"] = new[] { "Say why the goods are going back — damaged, expired, wrong item." } });

        var vendor = await _db.Vendors.FirstOrDefaultAsync(v => v.Id == req.VendorId && v.TenantId == tid, ct);
        if (vendor is null)
            throw AppException.Validation("Invalid supplier",
                new Dictionary<string, string[]> { ["vendorId"] = new[] { "Supplier not found in this business." } });

        PurchaseOrder? against = null;
        if (req.AgainstPurchaseOrderId is { } billId)
        {
            against = await _db.PurchaseOrders.Include(p => p.Lines)
                .FirstOrDefaultAsync(p => p.Id == billId && p.TenantId == tid && p.DocType == PurchaseDocType.Purchase, ct);
            if (against is null)
                throw AppException.Validation("Invalid purchase",
                    new Dictionary<string, string[]> { ["againstPurchaseOrderId"] = new[] { "That purchase bill was not found." } });
            if (against.VendorId != req.VendorId)
                throw AppException.Validation("Supplier mismatch",
                    new Dictionary<string, string[]> { ["againstPurchaseOrderId"] = new[] { $"Bill {against.PoNumber} belongs to a different supplier." } });
        }

        // Every line is checked before anything is written, so a rejected note leaves no half-done
        // stock movements behind.
        var alreadyReturned = against is null
            ? new Dictionary<(Guid, string), decimal>()
            : await ReturnedQuantitiesAsync(new List<Guid> { against.Id }, ct);

        var skuIds = req.Lines.Where(l => l.SkuId.HasValue).Select(l => l.SkuId!.Value).Distinct().ToList();
        var skus = skuIds.Count == 0 ? new Dictionary<Guid, Sku>() : await _db.Skus
            .Where(s => s.TenantId == tid && skuIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, ct);

        foreach (var l in req.Lines)
        {
            if (l.Quantity <= 0)
                throw AppException.Validation("Invalid quantity",
                    new Dictionary<string, string[]> { ["lines"] = new[] { $"Return quantity must be greater than zero for '{l.ItemName}'." } });

            if (against is not null)
            {
                var billLine = against.Lines.FirstOrDefault(bl => ReturnKey(bl) == ReturnKey(l.SkuId, l.ItemName));
                if (billLine is null)
                    throw AppException.Validation("Item not on that bill",
                        new Dictionary<string, string[]> { ["lines"] = new[] { $"'{l.ItemName}' isn't on bill {against.PoNumber}." } });
                var left = billLine.ReceivedQuantity - alreadyReturned.GetValueOrDefault((against.Id, ReturnKey(billLine)), 0m);
                if (l.Quantity > left + 0.001m)
                    throw AppException.Validation("More than was received",
                        new Dictionary<string, string[]>
                        {
                            ["lines"] = new[] { $"Only {left:0.##} of '{l.ItemName}' can still be returned against {against.PoNumber} (received {billLine.ReceivedQuantity:0.##})." },
                        });
            }

            if (l.SkuId is { } sid)
            {
                if (!skus.TryGetValue(sid, out var sku))
                    throw AppException.Validation("Unknown item",
                        new Dictionary<string, string[]> { ["lines"] = new[] { $"'{l.ItemName}' is not an item in this business." } });
                // Can't send back what isn't on the shelf — it has already been sold or used.
                if (l.Quantity > sku.StockOnHand + 0.001m)
                    throw AppException.Validation("Not enough stock to return",
                        new Dictionary<string, string[]>
                        {
                            ["lines"] = new[] { $"Only {sku.StockOnHand} of '{sku.Name}' is in stock — you can't return {l.Quantity:0.##}." },
                        });
            }
        }

        var note = new PurchaseOrder
        {
            TenantId = tid,
            DocType = PurchaseDocType.DebitNote,
            PoNumber = await NextDebitNoteNumberAsync(ct),
            VendorId = req.VendorId,
            PurchaseDate = req.DebitNoteDate,
            ReferenceBillNumber = req.ReferenceBillNumber ?? against?.VendorInvoiceNumber,
            ReturnAgainstPurchaseOrderId = against?.Id,
            ReturnReason = req.Reason.Trim(),
            PaymentTerm = req.PaymentTerm,
            DueDate = req.DueDate,
            ShippingDate = req.ShippingDate,
            ReverseCharge = req.ReverseCharge,
            ExportSez = req.ExportSez,
            TaxType = req.TaxType,
            AccountLedger = string.IsNullOrWhiteSpace(req.AccountLedger) ? "Purchase Return" : req.AccountLedger,
            Adjustment = req.Adjustment,
            AdditionalCharges = req.AdditionalCharges,
            Notes = req.Notes,
            // A return isn't something that gets received later — it's done the moment it's raised.
            Status = PoStatus.Closed,
        };

        BuildLines(note, req.Lines, req.TaxType, req.FlatDiscountPercent, req.AdditionalCharges, req.Adjustment);

        // The supplier owes this back, so it is never "due" from us.
        note.Paid = 0;
        note.Due = 0;
        note.PaymentStatus = PoPaymentStatus.Unpaid;
        note.NumberOfItems = note.Lines.Count;
        _db.PurchaseOrders.Add(note);

        foreach (var line in note.Lines.Where(l => l.SkuId.HasValue))
        {
            var sku = skus[line.SkuId!.Value];
            var qty = (int)Math.Round(line.Quantity);
            if (qty <= 0) continue;

            sku.StockOnHand = Math.Max(0, sku.StockOnHand - qty);
            DeductFromBatches(sku.Id, tid, line.BatchNumber, qty);

            _db.StockMovements.Add(new StockMovement
            {
                TenantId = tid,
                SkuId = sku.Id,
                Reason = StockMovementReason.PurchaseReturn,
                QuantityChange = -qty,
                StockAfter = sku.StockOnHand,
                RelatedPurchaseOrderId = note.Id,
                Note = against is null
                    ? $"Debit note {note.PoNumber} — returned to {vendor.Name}"
                    : $"Debit note {note.PoNumber} against {against.PoNumber} — returned to {vendor.Name}",
            });
        }

        await _db.SaveChangesAsync(ct);
        return (await GetPoAsync(note.Id, ct))!;
    }

    /// <summary>Takes the returned units out of stock batches — the named batch first, since that's
    /// the physical stock going back, then oldest-first for whatever remains.</summary>
    private void DeductFromBatches(Guid skuId, Guid tenantId, string? batchNumber, int qty)
    {
        var tracked = _db.SkuBatches
            .Where(b => b.TenantId == tenantId && b.SkuId == skuId && b.QtyRemaining > 0)
            .OrderBy(b => b.ReceivedAt)
            .ToList();

        IEnumerable<SkuBatch> order = tracked;
        if (!string.IsNullOrWhiteSpace(batchNumber))
        {
            var named = tracked.Where(b => string.Equals(b.BatchNumber, batchNumber, StringComparison.OrdinalIgnoreCase)).ToList();
            order = named.Concat(tracked.Except(named));
        }

        var left = (decimal)qty;
        foreach (var b in order)
        {
            if (left <= 0) break;
            var take = Math.Min(b.QtyRemaining, left);
            b.QtyRemaining -= take;
            left -= take;
        }
    }

    private async Task<string> NextDebitNoteNumberAsync(CancellationToken ct)
    {
        var year = DateTime.UtcNow.Year;
        var prefix = $"DN-{year}-";
        var existing = await _db.PurchaseOrders.IgnoreQueryFilters()
            .Where(p => p.TenantId == _user.TenantId && p.PoNumber.StartsWith(prefix))
            .Select(p => p.PoNumber)
            .ToListAsync(ct);
        var max = existing
            .Select(n => int.TryParse(n.AsSpan(prefix.Length), out var v) ? v : 0)
            .DefaultIfEmpty(0)
            .Max();
        return $"{prefix}{(max + 1).ToString().PadLeft(5, '0')}";
    }

    public async Task<IReadOnlyList<VendorBalance>> VendorBalancesAsync(CancellationToken ct = default)
    {
        if (_user.TenantId is null) return Array.Empty<VendorBalance>();
        var tid = _user.TenantId.Value;

        var rows = await _db.PurchaseOrders.AsNoTracking()
            .Where(p => p.TenantId == tid)
            .GroupBy(p => new { p.VendorId, VendorName = p.Vendor!.Name })
            .Select(g => new
            {
                g.Key.VendorId,
                g.Key.VendorName,
                BillsDue = g.Where(p => p.DocType == PurchaseDocType.Purchase).Sum(p => (decimal?)p.Due) ?? 0m,
                DebitNotes = g.Where(p => p.DocType == PurchaseDocType.DebitNote).Sum(p => (decimal?)p.Total) ?? 0m,
            })
            .ToListAsync(ct);

        return rows
            .Select(r => new VendorBalance(r.VendorId, r.VendorName, r.BillsDue, r.DebitNotes, r.BillsDue - r.DebitNotes))
            .Where(r => r.BillsDue != 0 || r.DebitNotes != 0)
            .OrderByDescending(r => r.NetPayable)
            .ToList();
    }
}
