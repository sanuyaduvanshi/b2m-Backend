using Microsoft.EntityFrameworkCore;
using Pettle.Application.Common;
using Pettle.Application.Reports;
using Pettle.Domain.Bookings;
using Pettle.Domain.Clients;
using Pettle.Domain.Invoices;
using Pettle.Infrastructure.Persistence;

namespace Pettle.Infrastructure.Reports;

public class ReportsService : IReportsService
{
    private readonly PettleDbContext _db;
    private readonly ICurrentUser _user;
    public ReportsService(PettleDbContext db, ICurrentUser user) { _db = db; _user = user; }

    private (DateTimeOffset from, DateTimeOffset to) RangeAsUtc(DateRange r)
        => (r.From.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc), r.To.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc));

    public async Task<ReportsOverview> OverviewAsync(DateRange range, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return new ReportsOverview(0, 0, 0, 0);
        var tid = _user.TenantId.Value;
        var (from, to) = RangeAsUtc(range);

        var revenue = await _db.Payments.Where(p => p.TenantId == tid && p.PaymentTime >= from && p.PaymentTime <= to)
            .SumAsync(p => (decimal?)p.Amount, ct) ?? 0m;
        var bookings = await _db.Bookings.CountAsync(b => b.TenantId == tid && b.BookingDate >= range.From && b.BookingDate <= range.To, ct);
        var newClients = await _db.PetParents.CountAsync(p => p.TenantId == tid && p.OnboardingDate >= range.From && p.OnboardingDate <= range.To, ct);
        var invoices = await _db.Invoices.CountAsync(i => i.TenantId == tid && i.InvoiceDate >= range.From && i.InvoiceDate <= range.To, ct);
        return new ReportsOverview(revenue, bookings, newClients, invoices);
    }

    public async Task<RevenueReport> RevenueAsync(DateRange range, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return new RevenueReport(0, 0, 0, Array.Empty<RevenuePoint>(), new Dictionary<string, decimal>());
        var tid = _user.TenantId.Value;
        var (from, to) = RangeAsUtc(range);

        var invoices = await _db.Invoices.AsNoTracking()
            .Where(i => i.TenantId == tid && i.InvoiceDate >= range.From && i.InvoiceDate <= range.To)
            .Select(i => new { i.Revenue, i.Paid, i.Due })
            .ToListAsync(ct);

        // Pull raw rows then group in-memory: DateOnly.FromDateTime() doesn't translate to PostgreSQL.
        var rawPayments = await _db.Payments.AsNoTracking()
            .Where(p => p.TenantId == tid && p.PaymentTime >= from && p.PaymentTime <= to)
            .Select(p => new { p.PaymentTime, p.Amount, p.Mode })
            .ToListAsync(ct);

        var daily = rawPayments
            .GroupBy(p => DateOnly.FromDateTime(p.PaymentTime.UtcDateTime))
            .Select(g => new RevenuePoint(g.Key, g.Sum(x => x.Amount)))
            .OrderBy(p => p.Date)
            .ToList();

        var byMode = rawPayments
            .GroupBy(p => p.Mode)
            .ToDictionary(g => g.Key.ToString(), g => g.Sum(x => x.Amount));

        return new RevenueReport(
            invoices.Sum(i => i.Revenue),
            invoices.Sum(i => i.Paid),
            invoices.Sum(i => i.Due),
            daily,
            byMode);
    }

    public async Task<BookingsReport> BookingsAsync(DateRange range, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return new BookingsReport(0, 0, 0, 0, Array.Empty<BookingsBreakdown>());
        var tid = _user.TenantId.Value;

        var services = await _db.BookingServices.AsNoTracking()
            .Where(s => s.TenantId == tid && s.Booking!.BookingDate >= range.From && s.Booking.BookingDate <= range.To)
            .ToListAsync(ct);

        var bookingsCount = services.Select(s => s.BookingId).Distinct().Count();
        var completed = services.Count(s => s.Status == BookingStatus.CheckedOut);
        var cancelled = services.Count(s => s.Status == BookingStatus.Cancelled);
        var noshow = services.Count(s => s.Status == BookingStatus.NoShow);

        var breakdown = services.GroupBy(s => s.ServiceType)
            .Select(g => new BookingsBreakdown(g.Key.ToString(), g.Count(), g.Sum(x => x.FinalAmount)))
            .ToList();

        return new BookingsReport(bookingsCount, completed, cancelled, noshow, breakdown);
    }

    public async Task<ClientsReport> ClientsAsync(DateRange range, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return new ClientsReport(0, 0, 0, Array.Empty<TopClient>());
        var tid = _user.TenantId.Value;

        var active = await _db.PetParents.CountAsync(p => p.TenantId == tid && p.Status == ClientStatus.Active, ct);
        var archived = await _db.PetParents.IgnoreQueryFilters().CountAsync(p => p.TenantId == tid && p.Status == ClientStatus.Archived, ct);
        var newClients = await _db.PetParents.CountAsync(p => p.TenantId == tid && p.OnboardingDate >= range.From && p.OnboardingDate <= range.To, ct);

        // Pull rows then aggregate in-memory: the GroupBy + nullable-unwrap projection
        // doesn't translate to PostgreSQL cleanly.
        var rawInvoices = await _db.Invoices.AsNoTracking()
            .Where(i => i.TenantId == tid && i.PetParentId.HasValue
                        && i.InvoiceDate >= range.From && i.InvoiceDate <= range.To)
            .Select(i => new { ParentId = i.PetParentId!.Value, i.ParentNameSnapshot, i.PhoneSnapshot, i.Revenue })
            .ToListAsync(ct);

        var top = rawInvoices
            .GroupBy(i => new { i.ParentId, i.ParentNameSnapshot, i.PhoneSnapshot })
            .Select(g => new TopClient(g.Key.ParentId, g.Key.ParentNameSnapshot, g.Key.PhoneSnapshot, g.Count(), g.Sum(x => x.Revenue)))
            .OrderByDescending(x => x.Spend)
            .Take(10)
            .ToList();

        return new ClientsReport(active, archived, newClients, top);
    }

    public async Task<InventoryReport> InventoryAsync(CancellationToken ct = default)
    {
        if (_user.TenantId is null) return new InventoryReport(0, 0, 0, 0);
        var tid = _user.TenantId.Value;
        var soon = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30);

        var total = await _db.Skus.CountAsync(s => s.TenantId == tid, ct);
        var low = await _db.Skus.CountAsync(s => s.TenantId == tid && s.IsActive && s.StockOnHand <= s.ReorderLevel, ct);
        var expiring = await _db.Skus.CountAsync(s => s.TenantId == tid && s.TrackExpiry && s.NearestExpiry != null && s.NearestExpiry <= soon, ct);
        var value = await _db.Skus.Where(s => s.TenantId == tid).SumAsync(s => (decimal?)(s.StockOnHand * s.CostPrice), ct) ?? 0m;

        return new InventoryReport(total, low, expiring, value);
    }
}
