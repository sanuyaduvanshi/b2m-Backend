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
        if (_user.TenantId is null) return new RevenueReport(0, 0, 0, Array.Empty<RevenuePoint>(), new Dictionary<string, decimal>(), Array.Empty<ExpenseSlice>(), Array.Empty<ExpenseSlice>());
        var tid = _user.TenantId.Value;
        var (from, to) = RangeAsUtc(range);

        var invoices = await _db.Invoices.AsNoTracking()
            .Where(i => i.TenantId == tid && i.InvoiceDate >= range.From && i.InvoiceDate <= range.To)
            .Select(i => new { i.Revenue, i.Paid, i.Due, i.InvoiceType, i.PaymentStatus })
            .ToListAsync(ct);

        var byType = invoices.GroupBy(i => i.InvoiceType.ToString())
            .Select(g => new ExpenseSlice(g.Key, g.Sum(x => x.Revenue)))
            .OrderByDescending(s => s.Amount).ToList();
        var byStatus = invoices.GroupBy(i => i.PaymentStatus.ToString())
            .Select(g => new ExpenseSlice(g.Key, g.Count()))
            .OrderByDescending(s => s.Amount).ToList();

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
            byMode,
            byType,
            byStatus);
    }

    public async Task<IReadOnlyList<MonthlyPoint>> MonthlyAsync(DateRange range, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return Array.Empty<MonthlyPoint>();
        var tid = _user.TenantId.Value;
        var (from, to) = RangeAsUtc(range);

        var rawPayments = await _db.Payments.AsNoTracking()
            .Where(p => p.TenantId == tid && p.PaymentTime >= from && p.PaymentTime <= to)
            .Select(p => new { p.PaymentTime, p.Amount })
            .ToListAsync(ct);
        var rawExpenses = await _db.Expenses.AsNoTracking()
            .Where(e => e.TenantId == tid && e.Time >= from && e.Time <= to)
            .Select(e => new { e.Time, e.AmountIncTax })
            .ToListAsync(ct);

        var revenueByMonth = rawPayments
            .GroupBy(p => p.PaymentTime.UtcDateTime.ToString("yyyy-MM"))
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));
        var expensesByMonth = rawExpenses
            .GroupBy(e => e.Time.UtcDateTime.ToString("yyyy-MM"))
            .ToDictionary(g => g.Key, g => g.Sum(x => x.AmountIncTax));

        var months = revenueByMonth.Keys.Union(expensesByMonth.Keys).OrderBy(m => m).ToList();
        return months.Select(m =>
        {
            var rev = revenueByMonth.GetValueOrDefault(m, 0m);
            var exp = expensesByMonth.GetValueOrDefault(m, 0m);
            return new MonthlyPoint(m, rev, exp, rev - exp);
        }).ToList();
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

    public async Task<ExpensesReport> ExpensesAsync(DateRange range, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return new ExpensesReport(0, 0, Array.Empty<ExpenseSlice>(), Array.Empty<ExpenseSlice>());
        var tid = _user.TenantId.Value;
        var (from, to) = RangeAsUtc(range);

        var rows = await _db.Expenses.AsNoTracking()
            .Where(e => e.TenantId == tid && e.Time >= from && e.Time <= to)
            .Select(e => new { e.AmountIncTax, e.PaymentMode, Category = e.CategoryName ?? (e.Category != null ? e.Category.Name : null) })
            .ToListAsync(ct);

        var byCategory = rows.GroupBy(r => string.IsNullOrWhiteSpace(r.Category) ? "Uncategorised" : r.Category!)
            .Select(g => new ExpenseSlice(g.Key, g.Sum(x => x.AmountIncTax)))
            .OrderByDescending(s => s.Amount).ToList();
        var byMode = rows.GroupBy(r => string.IsNullOrWhiteSpace(r.PaymentMode) ? "Cash" : r.PaymentMode)
            .Select(g => new ExpenseSlice(g.Key, g.Sum(x => x.AmountIncTax)))
            .OrderByDescending(s => s.Amount).ToList();

        return new ExpensesReport(rows.Sum(r => r.AmountIncTax), rows.Count, byCategory, byMode);
    }

    public async Task<ProfitReport> ProfitAsync(DateRange range, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return new ProfitReport(0, 0, 0);
        var tid = _user.TenantId.Value;
        var (from, to) = RangeAsUtc(range);

        var collected = await _db.Payments.Where(p => p.TenantId == tid && p.PaymentTime >= from && p.PaymentTime <= to)
            .SumAsync(p => (decimal?)p.Amount, ct) ?? 0m;
        var expenses = await _db.Expenses.Where(e => e.TenantId == tid && e.Time >= from && e.Time <= to)
            .SumAsync(e => (decimal?)e.AmountIncTax, ct) ?? 0m;
        return new ProfitReport(collected, expenses, collected - expenses);
    }

    public async Task<InventoryReport> InventoryAsync(CancellationToken ct = default)
    {
        if (_user.TenantId is null) return new InventoryReport(0, 0, 0, 0, Array.Empty<ExpenseSlice>());
        var tid = _user.TenantId.Value;
        var soon = BusinessClock.TodayIst().AddDays(30);

        var total = await _db.Skus.CountAsync(s => s.TenantId == tid, ct);
        var low = await _db.Skus.CountAsync(s => s.TenantId == tid && s.IsActive && s.ReorderLevel > 0 && s.StockOnHand <= s.ReorderLevel, ct);
        var expiring = await _db.Skus.CountAsync(s => s.TenantId == tid && s.TrackExpiry && s.NearestExpiry != null && s.NearestExpiry <= soon, ct);
        var value = await _db.Skus.Where(s => s.TenantId == tid).SumAsync(s => (decimal?)(s.StockOnHand * s.CostPrice), ct) ?? 0m;

        var rawSkus = await _db.Skus.AsNoTracking()
            .Where(s => s.TenantId == tid)
            .Select(s => new { CategoryName = s.Category != null ? s.Category.Name : null, s.StockOnHand, s.CostPrice })
            .ToListAsync(ct);
        var byCategory = rawSkus
            .GroupBy(s => string.IsNullOrWhiteSpace(s.CategoryName) ? "Uncategorised" : s.CategoryName!)
            .Select(g => new ExpenseSlice(g.Key, g.Sum(x => x.StockOnHand * x.CostPrice)))
            .OrderByDescending(s => s.Amount).ToList();

        return new InventoryReport(total, low, expiring, value, byCategory);
    }
}
