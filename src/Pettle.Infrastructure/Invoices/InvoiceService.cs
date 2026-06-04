using Microsoft.EntityFrameworkCore;
using Pettle.Application.Clients;
using Pettle.Application.Common;
using Pettle.Application.Common.Errors;
using Pettle.Application.Invoices;
using Pettle.Domain.Invoices;
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
        if (query.Type.HasValue) q = q.Where(i => i.InvoiceType == query.Type.Value);
        if (query.Status.HasValue) q = q.Where(i => i.PaymentStatus == query.Status.Value);
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
                i.ParentNameSnapshot, i.PhoneSnapshot, i.PetNameSnapshot, i.Revenue, i.Paid, i.Due, i.PaymentStatus))
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

        return new InvoiceDetail(
            i.Id, i.InvoiceNumber, i.InvoiceType, i.InvoiceDate, i.PetParentId,
            i.ParentNameSnapshot, i.PhoneSnapshot, i.PetNameSnapshot,
            i.BaseAmount, i.AddOnAmount, i.DiscountAmount,
            i.IgstAmount, i.CgstAmount, i.SgstAmount,
            i.Revenue, i.Paid, i.Due, i.PaymentStatus,
            i.Lines.Select(l => new InvoiceLineDto(l.Id, l.BillItemName, l.Category, l.Quantity, l.UnitAmount, l.Discount, l.Subtotal, l.Total)).ToList(),
            i.Payments.OrderByDescending(p => p.PaymentTime).Select(p => new PaymentDto(p.Id, p.PaymentTime, p.Amount, p.Mode, p.Source, p.TransactionId)).ToList()
        );
    }

    public async Task<PaymentDto?> RecordPaymentAsync(Guid invoiceId, RecordPaymentRequest req, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return null;
        var invoice = await _db.Invoices.Include(i => i.Payments).FirstOrDefaultAsync(i => i.Id == invoiceId && i.TenantId == _user.TenantId, ct);
        if (invoice is null) return null;

        if (invoice.PaymentStatus == InvoicePaymentStatus.Cancelled || invoice.PaymentStatus == InvoicePaymentStatus.Refunded)
            throw AppException.BusinessRule($"Cannot record a payment on a {invoice.PaymentStatus} invoice.");
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
            TransactionId = req.TransactionId,
            Notes = req.Notes
        };
        _db.Payments.Add(payment);
        invoice.Paid += req.Amount;
        invoice.Due = Math.Max(0, invoice.Revenue - invoice.Paid);
        invoice.PaymentStatus = invoice.Due == 0
            ? InvoicePaymentStatus.Paid
            : invoice.Paid > 0 ? InvoicePaymentStatus.PartiallyPaid : InvoicePaymentStatus.Pending;
        await _db.SaveChangesAsync(ct);
        return new PaymentDto(payment.Id, payment.PaymentTime, payment.Amount, payment.Mode, payment.Source, payment.TransactionId);
    }

    public async Task<bool> RefundAsync(Guid invoiceId, RefundRequest req, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return false;
        var invoice = await _db.Invoices.FirstOrDefaultAsync(i => i.Id == invoiceId && i.TenantId == _user.TenantId, ct);
        if (invoice is null) return false;
        if (invoice.PaymentStatus == InvoicePaymentStatus.Cancelled)
            throw AppException.BusinessRule("Cannot refund a cancelled invoice.");
        if (req.Amount > invoice.Paid + 0.01m)
            throw AppException.Validation("Refund exceeds amount paid",
                new Dictionary<string, string[]> { ["amount"] = new[] { $"Refund ₹{req.Amount:F2} exceeds amount paid ₹{invoice.Paid:F2}." } });
        invoice.Paid = Math.Max(0, invoice.Paid - req.Amount);
        invoice.Due = Math.Max(0, invoice.Revenue - invoice.Paid);
        invoice.PaymentStatus = InvoicePaymentStatus.Refunded;
        invoice.Notes = (invoice.Notes is null ? "" : invoice.Notes + " | ") + $"Refund {req.Amount}: {req.Reason}";
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
