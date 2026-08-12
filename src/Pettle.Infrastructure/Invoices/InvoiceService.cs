using Microsoft.EntityFrameworkCore;
using Pettle.Application.Clients;
using Pettle.Application.Common;
using Pettle.Application.Common.Errors;
using Pettle.Application.Invoices;
using Pettle.Domain.Bookings;
using Pettle.Domain.Inventory;
using Pettle.Domain.Invoices;
using Pettle.Infrastructure.Inventory;
using Pettle.Infrastructure.Persistence;

namespace Pettle.Infrastructure.Invoices;

public class InvoiceService : IInvoiceService
{
    private readonly PettleDbContext _db;
    private readonly ICurrentUser _user;

    public InvoiceService(PettleDbContext db, ICurrentUser user) { _db = db; _user = user; }

    public async Task<PagedResult<InvoiceListItem>> ListAsync(InvoiceListQuery query, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return new PagedResult<InvoiceListItem>(Array.Empty<InvoiceListItem>(), 0, 1, query.PageSize);

        var q = _db.Invoices.AsNoTracking().Where(i => i.TenantId == _user.TenantId);
        // Receptionist-type roles only see invoices/payments they personally created.
        if (_user.RestrictToOwnRecords) q = q.Where(i => i.CreatedById == _user.UserId);
        if (query.Type.HasValue) q = q.Where(i => i.InvoiceType == query.Type.Value);
        if (query.Status.HasValue) q = q.Where(i => i.PaymentStatus == query.Status.Value);
        if (query.Mode.HasValue) q = q.Where(i => i.Payments.Any(p => p.Mode == query.Mode.Value));
        if (query.FromDate is { } f) q = q.Where(i => i.InvoiceDate >= f);
        if (query.ToDate is { } t) q = q.Where(i => i.InvoiceDate <= t);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim().ToLower();
            q = q.Where(i => i.InvoiceNumber.ToLower().Contains(s)
                || i.ParentNameSnapshot.ToLower().Contains(s)
                || i.PhoneSnapshot.Contains(s));
        }

        var total = await q.CountAsync(ct);
        var page = Math.Max(query.Page, 1);
        var size = Math.Clamp(query.PageSize, 1, 200);

        var items = await q.OrderByDescending(i => i.InvoiceDate).ThenByDescending(i => i.CreatedAt)
            .Skip((page - 1) * size).Take(size)
            .Select(i => new InvoiceListItem(
                i.Id, i.LegacyInvoiceNo, i.InvoiceNumber, i.InvoiceType, i.InvoiceDate,
                i.ParentNameSnapshot, i.PhoneSnapshot, i.PetNameSnapshot, i.Revenue, i.Paid, i.Due, i.PaymentStatus,
                i.Payments.OrderByDescending(p => p.PaymentTime)
                    .Select(p => new PaymentBrief(p.Mode, p.Amount, p.Status)).ToList()))
            .ToListAsync(ct);

        return new PagedResult<InvoiceListItem>(items, total, page, size);
    }

    public async Task<InvoiceDetail?> GetAsync(Guid id, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return null;
        var i = await _db.Invoices.AsNoTracking()
            .Include(x => x.Lines)
            .Include(x => x.Payments)
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == _user.TenantId, ct);
        if (i is null) return null;

        // Which subscription (if any) paid for which payment row — resolved as one small
        // follow-up query rather than per-row, since a payment only carries the subscription's id.
        var subIds = i.Payments.Where(p => p.IssuedSubscriptionId.HasValue).Select(p => p.IssuedSubscriptionId!.Value).Distinct().ToList();
        var subRows = subIds.Count == 0 ? new List<IssuedSubscriptionSummaryRow>() : await _db.IssuedSubscriptions.AsNoTracking()
            .Where(s => subIds.Contains(s.Id))
            .Select(s => new IssuedSubscriptionSummaryRow(
                s.Id, s.Package!.Name, s.RemainingSessions, s.TotalSessions,
                s.ValidUntil, s.Package!.Price - s.BalanceUsed, s.Status.ToString(),
                s.Pet != null ? s.Pet.Name : null))
            .ToListAsync(ct);
        var subNames = subRows.ToDictionary(s => s.Id, s => s.PackageName);

        // What the plan actually absorbed on this bill, and where it stands afterwards — the
        // invoice prints both so "Paid" doesn't read as cash the customer handed over.
        var coveredBySub = i.Payments
            .Where(p => p.IssuedSubscriptionId.HasValue && p.Status == PaymentRecordStatus.Success)
            .Sum(p => p.Amount);
        var primarySub = subRows.FirstOrDefault();
        var subscriptionInfo = primarySub is null || coveredBySub <= 0 ? null : new InvoiceSubscriptionInfo(
            primarySub.PackageName, coveredBySub, primarySub.RemainingSessions, primarySub.TotalSessions,
            primarySub.ValidUntil, Math.Max(0, primarySub.RemainingBalance), primarySub.Status, primarySub.PetName);

        return new InvoiceDetail(
            i.Id, i.InvoiceNumber, i.InvoiceType, i.InvoiceDate, i.PetParentId,
            i.ParentNameSnapshot, i.PhoneSnapshot, i.PetNameSnapshot,
            i.BaseAmount, i.AddOnAmount, i.AdditionalAmount, i.DiscountAmount,
            i.IgstAmount, i.CgstAmount, i.SgstAmount, i.RoundOff,
            i.Revenue, i.Paid, i.Due, i.PaymentStatus,
            i.Lines.Select(l => new InvoiceLineDto(l.Id, l.BillItemName, l.Category, l.Description, l.Quantity, l.UnitAmount, l.Discount, l.Subtotal, l.Total, l.BatchNumber)).ToList(),
            i.Payments.OrderByDescending(p => p.PaymentTime).Select(p => new PaymentDto(p.Id, p.PaymentTime, p.Amount, p.Mode, p.Source, p.TransactionId, p.Type, p.Status, p.Notes,
                p.IssuedSubscriptionId.HasValue && subNames.TryGetValue(p.IssuedSubscriptionId.Value, out var pn) ? pn : null)).ToList(),
            i.Notes,
            i.AdditionalChargesReason,
            i.BookingId,
            subIds.Count > 0 ? subNames.Values.FirstOrDefault() : null,
            subscriptionInfo,
            i.RefundedAmount,
            i.RefundedAt,
            i.RefundReason
        );
    }

    /// <summary>Flat shape for the subscription lookup — EF can project into it, and it keeps the
    /// anonymous type out of the surrounding logic.</summary>
    private sealed record IssuedSubscriptionSummaryRow(
        Guid Id, string PackageName, int RemainingSessions, int TotalSessions,
        DateOnly ValidUntil, decimal RemainingBalance, string Status, string? PetName);

    public async Task<byte[]?> GeneratePdfAsync(Guid id, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return null;
        var invoice = await GetAsync(id, ct);
        if (invoice is null) return null;

        var tenant = await _db.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == _user.TenantId, ct);

        return InvoicePdfRenderer.Render(invoice, tenant?.Name ?? "Invoice", tenant?.LogoUrl);
    }

    public async Task<InvoiceDetail> CreateSaleAsync(CreateSaleRequest req, CancellationToken ct = default)
    {
        if (_user.TenantId is null)
            throw AppException.BusinessRule("No active tenant context.");

        if (req.Lines is null || req.Lines.Count == 0)
            throw AppException.Validation("Empty sale",
                new Dictionary<string, string[]> { ["lines"] = new[] { "Add at least one item to the sale." } });

        // The money taken at the till belongs to the day the bill is dated. Stamping payments with
        // "now" instead meant a bill entered a day late put its sale on yesterday (Revenue reports
        // group by invoice date) but its cash on today (Dashboard and Overview group by payment
        // time) — the same day answering two different numbers. Today's sales keep the real clock
        // time so the till order stays readable; a backdated one lands at the start of its own
        // business day in IST.
        // Midday rather than midnight: the reports convert back to the IST date either way, but a
        // payment stamped 00:00 IST is 18:30 the previous day in UTC, so anything rendering the
        // raw timestamp (the payments list, an export opened abroad) would show the day before.
        var saleTime = req.InvoiceDate == BusinessClock.TodayIst()
            ? DateTimeOffset.UtcNow
            : BusinessClock.StartOfDayUtc(req.InvoiceDate).AddHours(12);

        var invoice = new Invoice
        {
            InvoiceNumber = await NextSaleInvoiceNumberAsync(ct),
            InvoiceType = InvoiceType.Sale,
            InvoiceDate = req.InvoiceDate,
            PetParentId = req.PetParentId,
            ParentNameSnapshot = string.IsNullOrWhiteSpace(req.ParentName) ? "Walk-in Customer" : req.ParentName.Trim(),
            PhoneSnapshot = req.Phone?.Trim() ?? string.Empty,
            PetNameSnapshot = string.IsNullOrWhiteSpace(req.PetName) ? null : req.PetName.Trim(),
            Notes = req.IsDelivery ? Prefix("Delivery", req.Notes) : req.Notes,
            PaymentStatus = InvoicePaymentStatus.Pending,
        };

        // Batch-load SKU descriptions for lines that reference a SKU
        var skuIds = req.Lines.Where(l => l.SkuId.HasValue).Select(l => l.SkuId!.Value).Distinct().ToList();
        var skuDescriptions = skuIds.Count > 0
            ? await _db.Skus.Where(s => skuIds.Contains(s.Id) && s.TenantId == _user.TenantId)
                            .Select(s => new { s.Id, s.Description })
                            .ToDictionaryAsync(s => s.Id, s => s.Description, ct)
            : new Dictionary<Guid, string?>();

        // Retail rates are GST-inclusive: extract tax out of the net (mirrors the on-screen totals).
        decimal sumGross = 0, sumLineDiscount = 0, sumTaxable = 0, sumTax = 0;
        var reqLineToInvoiceLine = new Dictionary<int, InvoiceLineItem>(); // index → line for FIFO batch link
        for (int lineIdx = 0; lineIdx < req.Lines.Count; lineIdx++)
        {
            var line = req.Lines[lineIdx];
            if (line.Quantity <= 0)
                throw AppException.Validation("Invalid quantity",
                    new Dictionary<string, string[]> { ["lines"] = new[] { $"Quantity must be greater than zero for '{line.ItemName}'." } });

            var gross = line.Quantity * line.UnitAmount;
            var disc1 = gross * (line.DiscountPercent / 100m);
            var disc2 = (gross - disc1) * (line.AddDiscountPercent / 100m);
            var net = gross - disc1 - disc2;                      // GST-inclusive line total
            var taxable = net / (1 + line.TaxPercent / 100m);
            var taxAmt = net - taxable;

            var invoiceLine = new InvoiceLineItem
            {
                BillItemName = line.ItemName,
                SkuName = line.SkuId.HasValue ? line.ItemName : null,
                Description = line.SkuId.HasValue && skuDescriptions.TryGetValue(line.SkuId.Value, out var desc) ? desc : null,
                Quantity = line.Quantity,
                UnitAmount = line.UnitAmount,
                Discount = R(disc1 + disc2),
                Subtotal = R(taxable),
                CgstAmount = R(taxAmt / 2m),
                SgstAmount = R(taxAmt / 2m),
                Total = R(net),
            };
            invoice.Lines.Add(invoiceLine);
            if (line.SkuId.HasValue) reqLineToInvoiceLine[lineIdx] = invoiceLine;

            sumGross += gross;
            sumLineDiscount += disc1 + disc2;
            sumTaxable += taxable;
            sumTax += taxAmt;
        }

        // Bill-level flat discount applies on the post-line-discount taxable value; tax scales proportionally.
        var flatDiscountAmount = sumTaxable * (req.FlatDiscountPercent / 100m);
        var finalTaxable = sumTaxable - flatDiscountAmount;
        var finalTax = sumTaxable > 0 ? sumTax * (finalTaxable / sumTaxable) : 0m;

        var rawTotal = finalTaxable + finalTax + req.AdditionalCharges;
        var (rounded, saleRoundOff) = BillRounding.ToWholeRupee(rawTotal);

        invoice.BaseAmount = R(sumTaxable);
        invoice.DiscountAmount = R(sumLineDiscount + flatDiscountAmount);
        invoice.AdditionalAmount = R(req.AdditionalCharges);
        invoice.AdditionalChargesReason = req.AdditionalCharges > 0 ? req.AdditionalChargesReason : null;
        invoice.CgstAmount = R(finalTax / 2m);
        invoice.SgstAmount = R(finalTax / 2m);
        invoice.IgstAmount = 0;
        invoice.RoundOff = saleRoundOff;
        invoice.Revenue = R(rounded);

        // Credit-note redemption — applied like a payment (Mode=Credit), tied back to the
        // credit note by its invoice number for traceability.
        Invoice? redeemedCreditNote = null;
        decimal creditRedeemed = 0;
        if (req.RedeemCreditNoteId is { } cnId && req.RedeemCreditNoteAmount > 0)
        {
            redeemedCreditNote = await _db.Invoices.IgnoreQueryFilters()
                .FirstOrDefaultAsync(i => i.Id == cnId && i.TenantId == _user.TenantId && i.InvoiceType == InvoiceType.CreditNote && !i.IsDeleted, ct);
            if (redeemedCreditNote is null)
                throw AppException.Validation("Invalid credit note",
                    new Dictionary<string, string[]> { ["redeemCreditNoteId"] = new[] { "Credit note not found." } });
            if (req.RedeemCreditNoteAmount > redeemedCreditNote.RemainingCreditAmount + 0.01m)
                throw AppException.Validation("Credit note amount too high",
                    new Dictionary<string, string[]> { ["redeemCreditNoteAmount"] = new[] { $"Only ₹{redeemedCreditNote.RemainingCreditAmount:F2} remains on credit note {redeemedCreditNote.InvoiceNumber}." } });
            creditRedeemed = R(req.RedeemCreditNoteAmount);
            invoice.Payments.Add(new Payment
            {
                PaymentTime = saleTime,
                Amount = creditRedeemed,
                Mode = PaymentMode.Credit,
                Source = PaymentSource.WalkIn,
                TransactionId = redeemedCreditNote.InvoiceNumber,
                Notes = $"Redeemed from credit note {redeemedCreditNote.InvoiceNumber}",
            });
        }

        // Payments
        decimal paid = creditRedeemed;
        foreach (var p in req.Payments ?? Array.Empty<CreateSalePayment>())
        {
            if (p.Amount <= 0) continue;
            invoice.Payments.Add(new Payment
            {
                PaymentTime = saleTime,
                Amount = p.Amount,
                Mode = p.Mode,
                Source = PaymentSource.WalkIn,
                TransactionId = p.TransactionId,
            });
            paid += p.Amount;
        }
        // The POS screen independently recomputes this same whole-rupee total in JS (double
        // precision) from the raw line items so it can show it live before checkout; this
        // recomputes it again in C# decimal. The two are mathematically the same formula, but a
        // rawTotal that lands within a fraction of a paisa of a .50 boundary can round to the
        // *adjacent* rupee on one side and not the other (e.g. JS rounds 864.500001 up to 865
        // while decimal math here lands on 864.499999 and rounds down to 864) - a real customer
        // hit exactly this paying the "₹865" the screen offered against a server total of ₹864.
        // A whole-rupee tolerance absorbs that single-boundary divergence without opening the
        // door to any real underpayment (POS totals are already rounded to the nearest rupee).
        if (paid > invoice.Revenue + 1.01m)
            throw AppException.Validation("Payment exceeds total",
                new Dictionary<string, string[]> { ["payments"] = new[] { $"Payments ₹{paid:F2} exceed the bill total ₹{invoice.Revenue:F2}." } });

        invoice.Paid = R(paid);
        invoice.Due = Math.Max(0, R(invoice.Revenue - paid));
        invoice.PaymentStatus = invoice.Due == 0 && paid > 0
            ? InvoicePaymentStatus.Paid
            : paid > 0 ? InvoicePaymentStatus.PartiallyPaid : InvoicePaymentStatus.Pending;

        _db.Invoices.Add(invoice);

        // Deduct stock for product (SKU-linked) lines.
        for (int lineIdx = 0; lineIdx < req.Lines.Count; lineIdx++)
        {
            var line = req.Lines[lineIdx];
            if (!line.SkuId.HasValue) continue;

            var sku = await _db.Skus.FirstOrDefaultAsync(s => s.Id == line.SkuId && s.TenantId == _user.TenantId, ct);
            if (sku is null)
                throw AppException.Validation("Unknown product",
                    new Dictionary<string, string[]> { ["lines"] = new[] { $"Product '{line.ItemName}' no longer exists." } });

            var qty = (int)Math.Ceiling(line.Quantity);
            if (sku.StockOnHand < qty)
                throw AppException.Conflict($"Not enough stock for '{sku.Name}' — {sku.StockOnHand} on hand, {qty} requested.");

            // FIFO: deduct from the oldest-received batch first.
            var firstBatch = await FifoBatchDeductor.DeductAsync(_db, sku.Id, _user.TenantId!.Value, qty, ct);

            // Link the FIFO batch number back to the invoice line for full traceability.
            if (firstBatch is not null && reqLineToInvoiceLine.TryGetValue(lineIdx, out var invLine))
                invLine.BatchNumber = firstBatch;

            sku.StockOnHand -= qty;
            sku.NearestExpiry = await GetNearestExpiryAsync(sku.Id, _user.TenantId!.Value, ct);

            _db.StockMovements.Add(new StockMovement
            {
                SkuId = sku.Id,
                Reason = StockMovementReason.Sale,
                QuantityChange = -qty,
                StockAfter = sku.StockOnHand,
                RelatedInvoiceId = invoice.Id,
                Note = $"Sale {invoice.InvoiceNumber} | Batch: {firstBatch ?? "N/A"}",
            });
        }

        if (redeemedCreditNote is not null)
            redeemedCreditNote.RemainingCreditAmount = Math.Max(0, redeemedCreditNote.RemainingCreditAmount - creditRedeemed);

        await _db.SaveChangesAsync(ct);
        return (await GetAsync(invoice.Id, ct))!;
    }

    private static string Prefix(string tag, string? notes) =>
        string.IsNullOrWhiteSpace(notes) ? tag : $"{tag} | {notes}";

    private static decimal R(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    // COUNT-based numbering broke booking invoices the moment any invoice in the sequence was
    // deleted (soft-delete leaves the row - and its InvoiceNumber - in place, so "count+1" recomputes
    // a number an existing row already has, a duplicate-key 500 on the next sale) — this hit
    // production for BKG- numbers earlier and was fixed there; Sale invoices had the exact same bug
    // and are just as deletable (DeleteAsync allows it whenever Paid is still 0). Deriving the next
    // number from the highest number actually in use is immune to gaps from deletions.
    private async Task<string> NextSaleInvoiceNumberAsync(CancellationToken ct)
    {
        var year = DateTime.UtcNow.Year;
        var prefix = $"SALE-{year}-";
        var existingNumbers = await _db.Invoices.IgnoreQueryFilters()
            .Where(i => i.TenantId == _user.TenantId && i.InvoiceType == InvoiceType.Sale
                        && i.InvoiceNumber != null && i.InvoiceNumber.StartsWith(prefix))
            .Select(i => i.InvoiceNumber!)
            .ToListAsync(ct);
        var maxNum = existingNumbers
            .Select(n => int.TryParse(n.AsSpan(prefix.Length), out var v) ? v : 0)
            .DefaultIfEmpty(0)
            .Max();
        return $"{prefix}{(maxNum + 1).ToString().PadLeft(5, '0')}";
    }

    public async Task<PaymentDto?> RecordPaymentAsync(Guid invoiceId, RecordPaymentRequest req, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return null;
        var invoice = await _db.Invoices.Include(i => i.Payments).FirstOrDefaultAsync(i => i.Id == invoiceId && i.TenantId == _user.TenantId, ct);
        if (invoice is null) return null;

        if (invoice.PaymentStatus == InvoicePaymentStatus.Cancelled || invoice.PaymentStatus == InvoicePaymentStatus.Refunded)
            throw AppException.BusinessRule($"Cannot record a payment on an invoice that's already {invoice.PaymentStatus.Humanize().ToLower()}.");
        if (req.Amount <= 0)
            throw AppException.Validation("Invalid amount",
                new Dictionary<string, string[]> { ["amount"] = new[] { "Payment amount must be greater than zero." } });
        if (req.Amount > invoice.Due + 0.01m)
            throw AppException.Validation("Payment exceeds amount due",
                new Dictionary<string, string[]> { ["amount"] = new[] { $"Payment ₹{req.Amount:F2} exceeds amount due ₹{invoice.Due:F2}." } });

        var payment = new Payment
        {
            InvoiceId = invoice.Id,
            PaymentTime = req.PaymentTime ?? DateTimeOffset.UtcNow,
            Amount = req.Amount,
            Mode = req.Mode,
            Source = req.Source,
            Type = req.Type,
            Status = req.Status,
            TransactionId = req.TransactionId,
            Notes = req.Notes
        };
        _db.Payments.Add(payment);
        // Only successful payments move the invoice balance; Pending/Failed are logged but don't settle.
        if (payment.Status == PaymentRecordStatus.Success)
        {
            invoice.Paid += req.Amount;
            invoice.Due = Math.Max(0, invoice.Revenue - invoice.Paid);
            invoice.PaymentStatus = invoice.Due == 0
                ? InvoicePaymentStatus.Paid
                : invoice.Paid > 0 ? InvoicePaymentStatus.PartiallyPaid : InvoicePaymentStatus.Pending;
        }
        await SyncBookingStatusAsync(invoice, ct);
        await _db.SaveChangesAsync(ct);
        return new PaymentDto(payment.Id, payment.PaymentTime, payment.Amount, payment.Mode, payment.Source, payment.TransactionId, payment.Type, payment.Status, payment.Notes);
    }

    public async Task<PaymentDto?> UpdatePaymentAsync(Guid invoiceId, Guid paymentId, RecordPaymentRequest req, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return null;
        var invoice = await _db.Invoices.Include(i => i.Payments).FirstOrDefaultAsync(i => i.Id == invoiceId && i.TenantId == _user.TenantId, ct);
        if (invoice is null) return null;
        var payment = invoice.Payments.FirstOrDefault(p => p.Id == paymentId);
        if (payment is null) return null;
        if (req.Amount <= 0)
            throw AppException.Validation("Invalid amount",
                new Dictionary<string, string[]> { ["amount"] = new[] { "Payment amount must be greater than zero." } });

        // Keep the source subscription's balance in sync with whatever this payment settles to
        // now — without this, editing the amount on an auto-debited payment left the subscription
        // still holding its old (now wrong) BalanceUsed, drifting out of sync with the invoice.
        if (payment.IssuedSubscriptionId is { } subId && req.Amount != payment.Amount)
        {
            var sub = await _db.IssuedSubscriptions.FirstOrDefaultAsync(s => s.Id == subId && s.TenantId == _user.TenantId, ct);
            if (sub is not null) sub.BalanceUsed = Math.Max(0, sub.BalanceUsed - payment.Amount + req.Amount);
        }

        payment.Amount = req.Amount; payment.Mode = req.Mode; payment.Source = req.Source;
        payment.Type = req.Type; payment.Status = req.Status;
        payment.TransactionId = req.TransactionId; payment.Notes = req.Notes;
        if (req.PaymentTime is { } t) payment.PaymentTime = t;
        RecomputeInvoiceBalance(invoice);
        if (invoice.Paid > invoice.Revenue + 0.01m)
            throw AppException.Validation("Payments exceed total",
                new Dictionary<string, string[]> { ["amount"] = new[] { $"Total payments ₹{invoice.Paid:F2} would exceed the invoice total ₹{invoice.Revenue:F2}." } });
        await SyncBookingStatusAsync(invoice, ct);
        await _db.SaveChangesAsync(ct);
        return new PaymentDto(payment.Id, payment.PaymentTime, payment.Amount, payment.Mode, payment.Source, payment.TransactionId, payment.Type, payment.Status, payment.Notes);
    }

    public async Task<bool> DeletePaymentAsync(Guid invoiceId, Guid paymentId, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return false;
        var invoice = await _db.Invoices.Include(i => i.Payments).FirstOrDefaultAsync(i => i.Id == invoiceId && i.TenantId == _user.TenantId, ct);
        if (invoice is null) return false;
        var payment = invoice.Payments.FirstOrDefault(p => p.Id == paymentId);
        if (payment is null) return false;

        // Deleting an auto-debited payment must give the session/balance back to the subscription
        // it came from — otherwise the subscription stays "spent" forever while the booking/invoice
        // it paid for goes back to unpaid, and the client's remaining balance silently understates
        // what they actually have left.
        if (payment.IssuedSubscriptionId is { } subId)
        {
            var sub = await _db.IssuedSubscriptions.FirstOrDefaultAsync(s => s.Id == subId && s.TenantId == _user.TenantId, ct);
            if (sub is not null)
            {
                sub.BalanceUsed = Math.Max(0, sub.BalanceUsed - payment.Amount);
                sub.RemainingSessions = Math.Min(sub.TotalSessions, sub.RemainingSessions + 1);
            }
        }

        _db.Payments.Remove(payment);
        invoice.Payments.Remove(payment);
        RecomputeInvoiceBalance(invoice);
        await SyncBookingStatusAsync(invoice, ct);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private static void RecomputeInvoiceBalance(Invoice invoice)
    {
        invoice.Paid = invoice.Payments.Where(p => p.Status == PaymentRecordStatus.Success).Sum(p => p.Amount);
        invoice.Due = Math.Max(0, invoice.Revenue - invoice.Paid);
        invoice.PaymentStatus = invoice.Due == 0 && invoice.Paid > 0
            ? InvoicePaymentStatus.Paid
            : invoice.Paid > 0 ? InvoicePaymentStatus.PartiallyPaid : InvoicePaymentStatus.Pending;
    }

    private async Task SyncBookingStatusAsync(Invoice invoice, CancellationToken ct)
    {
        if (invoice.BookingId is not { } bookingId) return;
        var booking = await _db.Bookings.FirstOrDefaultAsync(b => b.Id == bookingId && b.TenantId == _user.TenantId, ct);
        if (booking is null) return;
        booking.PaymentStatus = invoice.PaymentStatus switch
        {
            InvoicePaymentStatus.Paid         => BookingPaymentStatus.Paid,
            InvoicePaymentStatus.PartiallyPaid => BookingPaymentStatus.PartiallyPaid,
            InvoicePaymentStatus.Refunded      => BookingPaymentStatus.Refunded,
            _                                  => BookingPaymentStatus.Pending,
        };
        // The booking list/detail views show Booking.TotalBillingAmount as a cached snapshot of
        // the invoice total rather than joining live — keep it in sync whenever the invoice's
        // total changes (editing lines/discount/additional charges), not just its payment state.
        booking.TotalBillingAmount = invoice.Revenue;

        // GrossBillingAmount is the pre-discount basis every later discount is computed from
        // (BookingService.ApplyDiscount does Gross × (1 − pct)). It was written only at booking
        // creation, so adding a line to the invoice afterwards left it frozen at the original
        // figure — and a discount applied after that recomputed the bill from a number far
        // smaller than what the customer was actually billed, collapsing the total. A ₹9,696
        // booking whose Gross still read ₹1,196 would drop to ₹1,076 on a 10% discount instead
        // of ₹8,726. Derive it back out of the invoice so the two can't drift again:
        // Revenue = (Gross − Discount) + RoundOff, so Gross = Revenue − RoundOff + Discount.
        booking.GrossBillingAmount = Math.Round(
            invoice.Revenue - invoice.RoundOff + invoice.DiscountAmount, 2, MidpointRounding.AwayFromZero);
    }

    public async Task<bool> UpdateAsync(Guid id, UpdateInvoiceRequest req, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return false;
        var invoice = await _db.Invoices
            .FirstOrDefaultAsync(i => i.Id == id && i.TenantId == _user.TenantId, ct);
        if (invoice is null) return false;
        if (invoice.PaymentStatus == InvoicePaymentStatus.Cancelled)
            throw AppException.BusinessRule("Cannot edit a cancelled invoice.");
        if (req.Lines is null || req.Lines.Count == 0)
            throw AppException.Validation("Empty invoice",
                new Dictionary<string, string[]> { ["lines"] = new[] { "Add at least one line item." } });

        // Compute the new totals first, purely in memory - validated (including against money
        // already collected) before anything is written, so a rejected edit can't leave the
        // invoice's old line items hard-deleted with the new ones never having been saved.
        decimal sumGross = 0, sumLineDiscount = 0, sumTaxable = 0, sumTax = 0;
        foreach (var line in req.Lines)
        {
            if (line.Quantity <= 0)
                throw AppException.Validation("Invalid quantity",
                    new Dictionary<string, string[]> { ["lines"] = new[] { $"Quantity must be > 0 for '{line.ItemName}'." } });

            var gross = line.Quantity * line.UnitAmount;
            var disc = gross * (line.DiscountPercent / 100m);
            var net = gross - disc;
            var taxable = line.TaxPercent > 0 ? net / (1 + line.TaxPercent / 100m) : net;
            var taxAmt = net - taxable;

            sumGross += gross;
            sumLineDiscount += disc;
            sumTaxable += taxable;
            sumTax += taxAmt;
        }

        // Bill-level flat discount
        var flatDiscAmt = sumTaxable * (req.FlatDiscountPercent / 100m);
        var finalTaxable = sumTaxable - flatDiscAmt;
        var finalTax = sumTaxable > 0 ? sumTax * (finalTaxable / sumTaxable) : 0m;
        var rawTotal = finalTaxable + finalTax + req.AdditionalCharges;
        var (rounded, saleRoundOff) = BillRounding.ToWholeRupee(rawTotal);

        // Editing a line/discount can lower the total below what's already been collected - without
        // this check, Due just clamped to 0 and the invoice silently showed "Paid" while quietly
        // holding an overpayment with no refund, credit note, or any record of where that money went.
        if (invoice.Paid > R(rounded) + 0.01m)
            throw AppException.Validation("Edit would overpay the invoice",
                new Dictionary<string, string[]> { ["lines"] = new[] {
                    $"₹{invoice.Paid:F2} is already paid, but this edit brings the total to ₹{R(rounded):F2}. Refund the difference first, then edit." } });

        // Header
        invoice.InvoiceDate = req.InvoiceDate;
        invoice.ParentNameSnapshot = req.ParentName.Trim();
        invoice.PhoneSnapshot = req.Phone.Trim();
        invoice.PetNameSnapshot = string.IsNullOrWhiteSpace(req.PetName) ? null : req.PetName.Trim();
        invoice.Notes = string.IsNullOrWhiteSpace(req.Notes) ? null : req.Notes.Trim();

        // Replace lines
        await _db.InvoiceLineItems
            .Where(l => l.InvoiceId == id && l.TenantId == _user.TenantId.Value)
            .ExecuteDeleteAsync(ct);
        foreach (var line in req.Lines)
        {
            var gross = line.Quantity * line.UnitAmount;
            var disc = gross * (line.DiscountPercent / 100m);
            var net = gross - disc;
            var taxable = line.TaxPercent > 0 ? net / (1 + line.TaxPercent / 100m) : net;
            var taxAmt = net - taxable;

            _db.InvoiceLineItems.Add(new InvoiceLineItem
            {
                InvoiceId = id,
                TenantId = _user.TenantId.Value,
                BillItemName = line.ItemName.Trim(),
                Quantity = line.Quantity,
                UnitAmount = line.UnitAmount,
                Discount = R(disc),
                Subtotal = R(taxable),
                CgstAmount = R(taxAmt / 2m),
                SgstAmount = R(taxAmt / 2m),
                Total = R(net),
            });
        }

        invoice.BaseAmount = R(sumTaxable);
        invoice.DiscountAmount = R(sumLineDiscount + flatDiscAmt);
        invoice.AdditionalAmount = R(req.AdditionalCharges);
        invoice.AdditionalChargesReason = req.AdditionalCharges > 0 ? req.AdditionalChargesReason : null;
        invoice.CgstAmount = R(finalTax / 2m);
        invoice.SgstAmount = R(finalTax / 2m);
        invoice.IgstAmount = 0;
        invoice.RoundOff = saleRoundOff;
        invoice.Revenue = R(rounded);
        invoice.Due = Math.Max(0, R(rounded) - invoice.Paid);
        invoice.PaymentStatus = invoice.Due == 0 && invoice.Paid > 0
            ? InvoicePaymentStatus.Paid
            : invoice.Paid > 0 ? InvoicePaymentStatus.PartiallyPaid : InvoicePaymentStatus.Pending;

        await SyncBookingStatusAsync(invoice, ct);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return false;
        var invoice = await _db.Invoices
            .Include(i => i.Lines)
            .FirstOrDefaultAsync(i => i.Id == id && i.TenantId == _user.TenantId, ct);
        if (invoice is null) return false;
        if (invoice.Paid > 0)
            throw AppException.BusinessRule("Cannot delete an invoice that has recorded payments. Issue a refund first.");
        if (invoice.PaymentStatus == InvoicePaymentStatus.Cancelled)
            throw AppException.BusinessRule("Invoice is already cancelled.");
        if (invoice.PaymentStatus == InvoicePaymentStatus.Refunded)
            throw AppException.BusinessRule("Cannot delete a refunded invoice.");

        // Restore stock for Sale invoices with SKU-linked lines
        if (invoice.InvoiceType == InvoiceType.Sale && invoice.Lines.Count > 0)
        {
            var skuNames = invoice.Lines.Where(l => !string.IsNullOrEmpty(l.SkuName)).Select(l => l.SkuName!).Distinct().ToList();
            if (skuNames.Count > 0)
            {
                var skus = await _db.Skus.Where(s => s.TenantId == _user.TenantId && skuNames.Contains(s.Name)).ToListAsync(ct);
                foreach (var line in invoice.Lines.Where(l => !string.IsNullOrEmpty(l.SkuName)))
                {
                    var sku = skus.FirstOrDefault(s => s.Name == line.SkuName);
                    if (sku is not null)
                    {
                        var qty = (int)Math.Round(line.Quantity);
                        sku.StockOnHand += qty;
                        _db.SkuBatches.Add(new SkuBatch
                        {
                            TenantId = sku.TenantId,
                            SkuId = sku.Id,
                            QtyRemaining = qty,
                            LandingCost = sku.CostPrice,
                            Source = "Return",
                            ReceivedAt = DateTimeOffset.UtcNow,
                        });
                        sku.NearestExpiry = await GetNearestExpiryAsync(sku.Id, sku.TenantId, ct);
                        _db.StockMovements.Add(new StockMovement
                        {
                            SkuId = sku.Id,
                            Reason = StockMovementReason.Return,
                            QuantityChange = qty,
                            StockAfter = sku.StockOnHand,
                            RelatedInvoiceId = invoice.Id,
                            Note = $"Deleted invoice {invoice.InvoiceNumber}",
                        });
                    }
                }
            }
        }

        // Deleting a booking's invoice deletes the booking itself too — a booking with no invoice
        // was previously left behind as an orphaned "No bill" row, which is confusing clutter, not
        // a real booking anyone still needs. Booking is soft-deletable but its children aren't, so
        // removing the Booking entity alone wouldn't cascade to them (the soft-delete interceptor
        // converts that Remove into an UPDATE, which never fires the DB's ON DELETE CASCADE) — each
        // child table is deleted explicitly here instead.
        if (invoice.BookingId is { } bookingId)
        {
            var booking = await _db.Bookings
                .Include(b => b.Services)
                .Include(b => b.AddOns)
                .Include(b => b.InventoryItems)
                .Include(b => b.EstimateLines)
                .Include(b => b.ChangeRequests)
                .FirstOrDefaultAsync(b => b.Id == bookingId && b.TenantId == _user.TenantId, ct);
            if (booking is not null)
            {
                // Restore stock this booking deducted — both service-linked SKUs and independent
                // booking inventory items — mirroring the Sale-invoice restoration above.
                var skuIds = booking.Services.Where(s => s.SkuId.HasValue).Select(s => s.SkuId!.Value)
                    .Concat(booking.InventoryItems.Select(i => i.SkuId))
                    .Distinct().ToList();
                if (skuIds.Count > 0)
                {
                    var skus = await _db.Skus.Where(s => skuIds.Contains(s.Id) && s.TenantId == _user.TenantId).ToListAsync(ct);
                    void Restore(Guid skuId, int qty, string note)
                    {
                        var sku = skus.FirstOrDefault(s => s.Id == skuId);
                        if (sku is null || qty <= 0) return;
                        sku.StockOnHand += qty;
                        _db.SkuBatches.Add(new SkuBatch
                        {
                            TenantId = sku.TenantId, SkuId = sku.Id, QtyRemaining = qty,
                            LandingCost = sku.CostPrice, Source = "Return", ReceivedAt = DateTimeOffset.UtcNow,
                        });
                        _db.StockMovements.Add(new StockMovement
                        {
                            SkuId = sku.Id, Reason = StockMovementReason.Return,
                            QuantityChange = qty, StockAfter = sku.StockOnHand,
                            RelatedInvoiceId = invoice.Id, Note = note,
                        });
                    }
                    foreach (var s in booking.Services.Where(s => s.SkuId.HasValue))
                        Restore(s.SkuId!.Value, s.SkuQuantity, $"Deleted invoice {invoice.InvoiceNumber} (booking service)");
                    foreach (var i in booking.InventoryItems)
                        Restore(i.SkuId, i.Quantity, $"Deleted invoice {invoice.InvoiceNumber} (booking inventory item)");
                    foreach (var sku in skus)
                        sku.NearestExpiry = await GetNearestExpiryAsync(sku.Id, sku.TenantId, ct);
                }

                var serviceIds = booking.Services.Select(s => s.Id).ToList();
                if (serviceIds.Count > 0)
                {
                    _db.BoardingDetails.RemoveRange(await _db.BoardingDetails.Where(d => serviceIds.Contains(d.BookingServiceId)).ToListAsync(ct));
                    _db.GroomingDetails.RemoveRange(await _db.GroomingDetails.Where(d => serviceIds.Contains(d.BookingServiceId)).ToListAsync(ct));
                    _db.VetDetails.RemoveRange(await _db.VetDetails.Where(d => serviceIds.Contains(d.BookingServiceId)).ToListAsync(ct));
                    _db.DayCareDetails.RemoveRange(await _db.DayCareDetails.Where(d => serviceIds.Contains(d.BookingServiceId)).ToListAsync(ct));
                    _db.BookingServiceAddOns.RemoveRange(await _db.BookingServiceAddOns.Where(a => serviceIds.Contains(a.BookingServiceId)).ToListAsync(ct));
                }
                _db.BookingAddOns.RemoveRange(booking.AddOns);
                _db.BookingInventoryItems.RemoveRange(booking.InventoryItems);
                _db.BookingEstimateLines.RemoveRange(booking.EstimateLines);
                _db.BookingChangeRequests.RemoveRange(booking.ChangeRequests);
                _db.BookingServices.RemoveRange(booking.Services);
                _db.Bookings.Remove(booking); // soft-delete interceptor sets IsDeleted = true
            }
        }

        _db.Invoices.Remove(invoice); // soft-delete interceptor sets IsDeleted = true
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> RefundAsync(Guid invoiceId, RefundRequest req, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return false;
        var invoice = await _db.Invoices
            .Include(i => i.Lines)
            .FirstOrDefaultAsync(i => i.Id == invoiceId && i.TenantId == _user.TenantId, ct);
        if (invoice is null) return false;
        if (invoice.PaymentStatus == InvoicePaymentStatus.Cancelled)
            throw AppException.BusinessRule("Cannot refund a cancelled invoice.");
        if (req.Amount > invoice.Paid + 0.01m)
            throw AppException.Validation("Refund exceeds amount paid",
                new Dictionary<string, string[]> { ["amount"] = new[] { $"Refund ₹{req.Amount:F2} exceeds amount paid ₹{invoice.Paid:F2}." } });

        // 1. Cash refund (not a credit note) reduces the original invoice's balance — money left
        // the business. A credit note leaves the original invoice's Paid/Due untouched: it stays
        // settled, and the credit note tracks a separate redeemable liability instead.
        if (!req.AsCreditNote)
        {
            invoice.Paid = Math.Max(0, invoice.Paid - req.Amount);
            // NOT Revenue - Paid: a refunded invoice is closed, not awaiting collection (record-payment
            // is already blocked once PaymentStatus is Refunded — see AddPaymentAsync). The old formula
            // re-inflated Due back up to the refunded amount, showing e.g. "Due ₹200" in red right next
            // to a "Refunded" badge, and that same wrong Due fed every Outstanding total downstream
            // (Reports, the Invoices list KPI, Bookings' balance column) since they all just sum this field.
            invoice.Due = 0;
            invoice.PaymentStatus = InvoicePaymentStatus.Refunded;
            invoice.Notes = (invoice.Notes is null ? "" : invoice.Notes + " | ") + $"Refund ₹{req.Amount:F2}: {req.Reason}";
            invoice.RefundedAmount = (invoice.RefundedAmount ?? 0) + req.Amount;
            invoice.RefundedAt = DateTimeOffset.UtcNow;
            invoice.RefundReason = req.Reason;
            await SyncBookingStatusAsync(invoice, ct);
        }
        else
        {
            invoice.Notes = (invoice.Notes is null ? "" : invoice.Notes + " | ") + $"Return ₹{req.Amount:F2} (credit note issued): {req.Reason}";
        }

        // 2. Return stock to inventory for Sale invoices (INV-5)
        if (req.ReturnToStock && invoice.InvoiceType == InvoiceType.Sale && invoice.Lines.Count > 0)
        {
            var skuNames = invoice.Lines
                .Where(l => !string.IsNullOrEmpty(l.SkuName))
                .Select(l => l.SkuName!)
                .Distinct()
                .ToList();
            if (skuNames.Count > 0)
            {
                var skus = await _db.Skus
                    .Where(s => s.TenantId == _user.TenantId && skuNames.Contains(s.Name))
                    .ToListAsync(ct);
                foreach (var line in invoice.Lines.Where(l => !string.IsNullOrEmpty(l.SkuName)))
                {
                    var sku = skus.FirstOrDefault(s => s.Name == line.SkuName);
                    if (sku != null)
                    {
                        var qty = (int)Math.Round(line.Quantity);
                        sku.StockOnHand += qty;
                        _db.SkuBatches.Add(new SkuBatch
                        {
                            TenantId = sku.TenantId,
                            SkuId = sku.Id,
                            QtyRemaining = qty,
                            LandingCost = sku.CostPrice,
                            Source = "Return",
                            ReceivedAt = DateTimeOffset.UtcNow,
                        });
                        sku.NearestExpiry = await GetNearestExpiryAsync(sku.Id, sku.TenantId, ct);
                    }
                }
            }
        }

        // 3. Generate Credit Note (INV-7) — only for a Return, not a plain cash Refund
        if (req.AsCreditNote)
        {
            var year = DateTime.UtcNow.Year;
            var cnPrefix = $"CN-{year}-";
            // Same MAX-based fix as Sale/Booking invoice numbering — COUNT-based breaks the moment
            // any credit note row is ever removed (soft-delete leaves it in place either way, but
            // COUNT is fragile against any future deletion path too).
            var cnExisting = await _db.Invoices.IgnoreQueryFilters()
                .Where(i => i.TenantId == _user.TenantId && i.InvoiceType == InvoiceType.CreditNote
                            && i.InvoiceNumber != null && i.InvoiceNumber.StartsWith(cnPrefix))
                .Select(i => i.InvoiceNumber!)
                .ToListAsync(ct);
            var cnMaxNum = cnExisting
                .Select(n => int.TryParse(n.AsSpan(cnPrefix.Length), out var v) ? v : 0)
                .DefaultIfEmpty(0)
                .Max();
            var creditNote = new Invoice
            {
                TenantId = _user.TenantId.Value,
                InvoiceNumber = $"{cnPrefix}{(cnMaxNum + 1).ToString().PadLeft(4, '0')}",
                InvoiceType = InvoiceType.CreditNote,
                InvoiceDate = DateOnly.FromDateTime(DateTime.UtcNow),
                PetParentId = invoice.PetParentId,
                ParentNameSnapshot = invoice.ParentNameSnapshot,
                PhoneSnapshot = invoice.PhoneSnapshot,
                Notes = $"Credit note for {invoice.InvoiceNumber} — {req.Reason}",
                PaymentStatus = InvoicePaymentStatus.Paid,
                Revenue = req.Amount,
                Paid = req.Amount,
                Due = 0,
                RemainingCreditAmount = req.Amount,
            };
            _db.Invoices.Add(creditNote);
        }

        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<CreditNoteLookup?> LookupCreditNoteAsync(string code, CancellationToken ct = default)
    {
        if (_user.TenantId is null || string.IsNullOrWhiteSpace(code)) return null;
        var trimmed = code.Trim();
        var cn = await _db.Invoices.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(i => i.TenantId == _user.TenantId && i.InvoiceType == InvoiceType.CreditNote
                && !i.IsDeleted && i.InvoiceNumber == trimmed, ct);
        if (cn is null) return null;
        if (cn.RemainingCreditAmount <= 0)
            throw AppException.BusinessRule("This credit note has already been fully redeemed.");
        return new CreditNoteLookup(cn.Id, cn.InvoiceNumber, cn.RemainingCreditAmount, cn.PetParentId, cn.ParentNameSnapshot);
    }

    private async Task<DateOnly?> GetNearestExpiryAsync(Guid skuId, Guid tenantId, CancellationToken ct)
        => await _db.SkuBatches
            .Where(b => b.SkuId == skuId && b.TenantId == tenantId && b.QtyRemaining > 0 && b.ExpiryDate != null)
            .OrderBy(b => b.ExpiryDate)
            .Select(b => b.ExpiryDate)
            .FirstOrDefaultAsync(ct);
}
