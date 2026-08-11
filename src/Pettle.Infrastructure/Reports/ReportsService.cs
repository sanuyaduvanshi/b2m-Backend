using Microsoft.EntityFrameworkCore;
using Pettle.Application.Common;
using Pettle.Application.Reports;
using Pettle.Domain.Bookings;
using Pettle.Domain.Clients;
using Pettle.Domain.Inventory;
using Pettle.Domain.Invoices;
using Pettle.Infrastructure.Persistence;

namespace Pettle.Infrastructure.Reports;

public class ReportsService : IReportsService
{
    private readonly PettleDbContext _db;
    private readonly ICurrentUser _user;
    public ReportsService(PettleDbContext db, ICurrentUser user) { _db = db; _user = user; }

    private (DateTimeOffset from, DateTimeOffset to) RangeAsUtc(DateRange r)
        => (BusinessClock.StartOfDayUtc(r.From), BusinessClock.EndOfDayUtc(r.To));

    /// <summary>Roles flagged <see cref="Pettle.Domain.Identity.Role.RestrictToOwnRecords"/>
    /// (Receptionist) only see records they created — the same scoping the Bookings/Invoices lists
    /// and the dashboard already apply. Mirrored across every money figure here so the KPI cards
    /// can't report tenant-wide takings to someone whose lists show only their own rows.
    /// Client counts are deliberately left tenant-wide: the Client Database itself is not scoped,
    /// so scoping its KPI cards would contradict the list right below them.</summary>
    private (bool own, Guid? uid) Scope => (_user.RestrictToOwnRecords, _user.UserId);

    public async Task<ReportsOverview> OverviewAsync(DateRange range, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return new ReportsOverview(0, 0, 0, 0);
        var tid = _user.TenantId.Value;
        var (from, to) = RangeAsUtc(range);
        var (own, uid) = Scope;

        var revenue = await _db.Payments.RealRevenue().Where(p => p.TenantId == tid && p.PaymentTime >= from && p.PaymentTime <= to
            && (!own || p.CreatedById == uid))
            .SumAsync(p => (decimal?)p.Amount, ct) ?? 0m;
        var bookings = await _db.Bookings.CountAsync(b => b.TenantId == tid && b.BookingDate >= range.From && b.BookingDate <= range.To
            && (!own || b.CreatedById == uid), ct);
        var newClients = await _db.PetParents.CountAsync(p => p.TenantId == tid && p.OnboardingDate >= range.From && p.OnboardingDate <= range.To, ct);
        // Credit notes are a return/liability record, not a new sale — counting them here would
        // inflate "invoices raised" with entries that generated no new revenue.
        var invoices = await _db.Invoices.CountAsync(i => i.TenantId == tid && i.InvoiceDate >= range.From && i.InvoiceDate <= range.To
            && i.InvoiceType != InvoiceType.CreditNote && (!own || i.CreatedById == uid), ct);
        return new ReportsOverview(revenue, bookings, newClients, invoices);
    }

    public async Task<RevenueReport> RevenueAsync(DateRange range, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return new RevenueReport(0, 0, 0, Array.Empty<RevenuePoint>(), new Dictionary<string, decimal>(), Array.Empty<ExpenseSlice>(), Array.Empty<ExpenseSlice>(), 0, new Dictionary<string, int>());
        var tid = _user.TenantId.Value;
        var (from, to) = RangeAsUtc(range);
        var (own, uid) = Scope;

        var invoices = await _db.Invoices.AsNoTracking()
            .Where(i => i.TenantId == tid && i.InvoiceDate >= range.From && i.InvoiceDate <= range.To
                        && (!own || i.CreatedById == uid))
            .Select(i => new { i.Revenue, i.Paid, i.Due, i.InvoiceType, i.PaymentStatus })
            .ToListAsync(ct);

        // A credit note only records a store-credit liability against a return — its Revenue/Paid
        // fields are set just so it displays like a normal settled line item, not because it's a
        // new sale. Summing it into the headline Total/Paid/Due here would double-count money
        // already recognized as revenue on the invoice the credit note was issued against. It stays
        // in `invoices` (and so in byType/byStatus/countByType below) since seeing "how much credit
        // was issued" is legitimate — it just shouldn't inflate the combined totals.
        var revenueInvoices = invoices.Where(i => i.InvoiceType != InvoiceType.CreditNote).ToList();

        var byType = invoices.GroupBy(i => i.InvoiceType.ToString())
            .Select(g => new ExpenseSlice(g.Key, g.Sum(x => x.Revenue)))
            .OrderByDescending(s => s.Amount).ToList();
        var byStatus = invoices.GroupBy(i => i.PaymentStatus.ToString())
            .Select(g => new ExpenseSlice(g.Key, g.Count()))
            .OrderByDescending(s => s.Amount).ToList();
        var countByType = invoices.GroupBy(i => i.InvoiceType.ToString())
            .ToDictionary(g => g.Key, g => g.Count());

        // Pull raw rows then group in-memory: DateOnly.FromDateTime() doesn't translate to PostgreSQL.
        var rawPayments = await _db.Payments.AsNoTracking().RealRevenue()
            .Where(p => p.TenantId == tid && p.PaymentTime >= from && p.PaymentTime <= to
                        && (!own || p.CreatedById == uid))
            .Select(p => new { p.PaymentTime, p.Amount, p.Mode })
            .ToListAsync(ct);

        var daily = rawPayments
            .GroupBy(p => BusinessClock.ToIstDate(p.PaymentTime))
            .Select(g => new RevenuePoint(g.Key, g.Sum(x => x.Amount)))
            .OrderBy(p => p.Date)
            .ToList();

        var byMode = rawPayments
            .GroupBy(p => p.Mode)
            .ToDictionary(g => g.Key.ToString(), g => g.Sum(x => x.Amount));

        return new RevenueReport(
            revenueInvoices.Sum(i => i.Revenue),
            revenueInvoices.Sum(i => i.Paid),
            revenueInvoices.Sum(i => i.Due),
            daily,
            byMode,
            byType,
            byStatus,
            revenueInvoices.Count,
            countByType);
    }

    public async Task<IReadOnlyList<MonthlyPoint>> MonthlyAsync(DateRange range, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return Array.Empty<MonthlyPoint>();
        var tid = _user.TenantId.Value;
        var (from, to) = RangeAsUtc(range);
        var (own, uid) = Scope;

        var rawPayments = await _db.Payments.AsNoTracking().RealRevenue()
            .Where(p => p.TenantId == tid && p.PaymentTime >= from && p.PaymentTime <= to
                        && (!own || p.CreatedById == uid))
            .Select(p => new { p.PaymentTime, p.Amount })
            .ToListAsync(ct);
        var rawExpenses = await _db.Expenses.AsNoTracking()
            .Where(e => e.TenantId == tid && e.Time >= from && e.Time <= to
                        && (!own || e.CreatedById == uid))
            .Select(e => new { e.Time, e.AmountIncTax })
            .ToListAsync(ct);

        // IST-based grouping, not UTC calendar month - a payment made between midnight and 5:30 AM
        // IST on the 1st of a month is still the previous UTC day (and so the previous UTC month),
        // which silently rolled it into last month's bar on the Profit trend chart.
        var revenueByMonth = rawPayments
            .GroupBy(p => BusinessClock.ToIstDate(p.PaymentTime).ToString("yyyy-MM"))
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));
        var expensesByMonth = rawExpenses
            .GroupBy(e => BusinessClock.ToIstDate(e.Time).ToString("yyyy-MM"))
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
        var (own, uid) = Scope;

        var services = await _db.BookingServices.AsNoTracking()
            .Where(s => s.TenantId == tid && s.Booking!.BookingDate >= range.From && s.Booking.BookingDate <= range.To
                        && (!own || s.CreatedById == uid))
            .Select(s => new {
                s.BookingId, s.ServiceType, s.Status, s.FinalAmount,
                s.Booking!.GrossBillingAmount, s.Booking.TotalBillingAmount,
            })
            .ToListAsync(ct);

        var bookingsCount = services.Select(s => s.BookingId).Distinct().Count();
        var completed = services.Count(s => s.Status == BookingStatus.CheckedOut);
        var cancelled = services.Count(s => s.Status == BookingStatus.Cancelled);
        var noshow = services.Count(s => s.Status == BookingStatus.NoShow);

        // ApplyDiscountAsync only adjusts Booking.TotalBillingAmount/Invoice.Revenue, never each
        // line's own FinalAmount - summed straight, a discounted booking's services still added up
        // to the pre-discount gross, so this breakdown never reconciled with "Bookings Revenue"
        // elsewhere on the same page. Scale each line by the booking's actual discount ratio so the
        // two agree; bookings that never had a discount applied (GrossBillingAmount unset) pass
        // through unchanged.
        var breakdown = services.GroupBy(s => s.ServiceType)
            .Select(g => new BookingsBreakdown(g.Key.ToString(), g.Count(), g.Sum(x =>
                x.GrossBillingAmount > 0.01m
                    ? Math.Round(x.FinalAmount * (x.TotalBillingAmount / x.GrossBillingAmount), 2, MidpointRounding.AwayFromZero)
                    : x.FinalAmount)))
            .ToList();

        return new BookingsReport(bookingsCount, completed, cancelled, noshow, breakdown);
    }

    public async Task<ClientsReport> ClientsAsync(DateRange range, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return new ClientsReport(0, 0, 0, Array.Empty<TopClient>());
        var tid = _user.TenantId.Value;

        var active = await _db.PetParents.CountAsync(p => p.TenantId == tid && p.Status == ClientStatus.Active, ct);
        var archived = await _db.PetParents.IgnoreQueryFilters().CountAsync(p => p.TenantId == tid && p.Status == ClientStatus.Archived, ct);
        // Counted separately so callers summing "all clients" don't silently drop blacklisted ones
        // (Active + Archived alone under-reports the total the Clients list actually shows).
        var blacklisted = await _db.PetParents.IgnoreQueryFilters().CountAsync(p => p.TenantId == tid && p.Status == ClientStatus.Blacklisted, ct);
        var newClients = await _db.PetParents.CountAsync(p => p.TenantId == tid && p.OnboardingDate >= range.From && p.OnboardingDate <= range.To, ct);

        // Pull rows then aggregate in-memory: the GroupBy + nullable-unwrap projection
        // doesn't translate to PostgreSQL cleanly.
        var (own, uid) = Scope;
        var rawInvoices = await _db.Invoices.AsNoTracking()
            .Where(i => i.TenantId == tid && i.PetParentId.HasValue
                        && i.InvoiceDate >= range.From && i.InvoiceDate <= range.To
                        && i.InvoiceType != InvoiceType.CreditNote
                        && (!own || i.CreatedById == uid))
            .Select(i => new { ParentId = i.PetParentId!.Value, i.ParentNameSnapshot, i.PhoneSnapshot, i.Revenue })
            .ToListAsync(ct);

        var top = rawInvoices
            .GroupBy(i => new { i.ParentId, i.ParentNameSnapshot, i.PhoneSnapshot })
            .Select(g => new TopClient(g.Key.ParentId, g.Key.ParentNameSnapshot, g.Key.PhoneSnapshot, g.Count(), g.Sum(x => x.Revenue)))
            .OrderByDescending(x => x.Spend)
            .Take(10)
            .ToList();

        // The span of the client base itself, so "All time" can state the period it covers.
        var onboarded = _db.PetParents.IgnoreQueryFilters().Where(p => p.TenantId == tid && p.OnboardingDate != null);
        var firstOnboarding = await onboarded.MinAsync(p => p.OnboardingDate, ct);
        var lastOnboarding = await onboarded.MaxAsync(p => p.OnboardingDate, ct);

        return new ClientsReport(active, archived, newClients, top, blacklisted, firstOnboarding, lastOnboarding);
    }

    public async Task<ExpensesReport> ExpensesAsync(DateRange range, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return new ExpensesReport(0, 0, Array.Empty<ExpenseSlice>(), Array.Empty<ExpenseSlice>());
        var tid = _user.TenantId.Value;
        var (from, to) = RangeAsUtc(range);

        var (own, uid) = Scope;
        var rows = await _db.Expenses.AsNoTracking()
            .Where(e => e.TenantId == tid && e.Time >= from && e.Time <= to
                        && (!own || e.CreatedById == uid))
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

        var (own, uid) = Scope;
        var collected = await _db.Payments.RealRevenue().Where(p => p.TenantId == tid && p.PaymentTime >= from && p.PaymentTime <= to
            && (!own || p.CreatedById == uid))
            .SumAsync(p => (decimal?)p.Amount, ct) ?? 0m;
        var expenses = await _db.Expenses.Where(e => e.TenantId == tid && e.Time >= from && e.Time <= to
            && (!own || e.CreatedById == uid))
            .SumAsync(e => (decimal?)e.AmountIncTax, ct) ?? 0m;
        return new ProfitReport(collected, expenses, collected - expenses);
    }

    public async Task<PeriodSummary> PeriodSummaryAsync(DateRange range, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return new PeriodSummary(0, 0, 0, 0, 0, 0, 0, 0);
        var tid = _user.TenantId.Value;
        var (from, to) = RangeAsUtc(range);

        // Sale/Booking revenue come from invoices (what was actually billed for that vertical),
        // kept separate per-type so a walk-in sale never gets blended into booking revenue.
        var (own, uid) = Scope;
        var invoicesByType = await _db.Invoices.AsNoTracking()
            .Where(i => i.TenantId == tid && i.InvoiceDate >= range.From && i.InvoiceDate <= range.To
                        && (i.InvoiceType == InvoiceType.Sale || i.InvoiceType == InvoiceType.Booking)
                        && (!own || i.CreatedById == uid))
            .Select(i => new
            {
                i.InvoiceType, i.Revenue, i.Due,
                // The part of this bill a subscription auto-covered at booking time is already
                // counted once, in subsRevenue below, on the day the plan itself was sold —
                // Payments.RealRevenue() exists precisely to keep this same rupee out of the
                // cash-collected figures, but nothing equivalent guarded the billed side until
                // now, so redeeming a plan against a booking was inflating Revenue by the covered
                // amount on top of the plan's own price, even though no new money or new sale
                // happened that day.
                SubscriptionCovered = i.Payments.Where(p => p.IssuedSubscriptionId != null).Sum(p => (decimal?)p.Amount) ?? 0m,
            })
            .ToListAsync(ct);
        var salesRevenue = invoicesByType.Where(i => i.InvoiceType == InvoiceType.Sale)
            .Sum(i => Math.Max(0, i.Revenue - i.SubscriptionCovered));
        var salesCount = invoicesByType.Count(i => i.InvoiceType == InvoiceType.Sale);
        var salesDue = invoicesByType.Where(i => i.InvoiceType == InvoiceType.Sale).Sum(i => i.Due);
        var bookingsRevenue = invoicesByType.Where(i => i.InvoiceType == InvoiceType.Booking)
            .Sum(i => Math.Max(0, i.Revenue - i.SubscriptionCovered));
        var bookingsDue = invoicesByType.Where(i => i.InvoiceType == InvoiceType.Booking).Sum(i => i.Due);

        var totalBookings = await _db.Bookings.CountAsync(
            b => b.TenantId == tid && b.BookingDate >= range.From && b.BookingDate <= range.To
                 && (!own || b.CreatedById == uid), ct);

        var subsIssuedCount = await _db.IssuedSubscriptions.CountAsync(
            s => s.TenantId == tid && s.IssuedOn >= range.From && s.IssuedOn <= range.To
                 && (!own || s.CreatedById == uid), ct);

        // What the plans sold in this period were worth, on the same basis as the Sales and
        // Bookings figures beside it — billed, not collected. AmountPaid + AmountDue is the plan's
        // price as agreed at issue, whether or not the customer has finished paying for it.
        // (Cash received against plans is not lost: it falls out of RevenueCollected below, and
        // the dashboard's reconciliation panel reports it separately.)
        var subsRevenue = await _db.IssuedSubscriptions.AsNoTracking()
            .Where(s => s.TenantId == tid && s.IssuedOn >= range.From && s.IssuedOn <= range.To
                        && (!own || s.CreatedById == uid))
            .SumAsync(s => (decimal?)(s.AmountPaid + s.AmountDue), ct) ?? 0m;

        // How much is still outstanding across subscriptions issued in this period specifically —
        // paired with subsRevenue (all-time cash collected) to show a paid-vs-due split on the card.
        var subsDue = await _db.IssuedSubscriptions.AsNoTracking()
            .Where(s => s.TenantId == tid && s.IssuedOn >= range.From && s.IssuedOn <= range.To
                        && (!own || s.CreatedById == uid))
            .SumAsync(s => (decimal?)s.AmountDue, ct) ?? 0m;

        // Purchase Orders are money spent (cost), not revenue — kept in its own field so it's
        // never mistaken for income when the frontend labels/sums these cards.
        var poRows = await _db.PurchaseOrders.AsNoTracking()
            .Where(p => p.TenantId == tid && p.PurchaseDate >= range.From && p.PurchaseDate <= range.To
                        && (!own || p.CreatedById == uid))
            .Select(p => new { p.Total, p.Due, p.DocType })
            .ToListAsync(ct);
        var poTotals = poRows.Where(p => p.DocType == PurchaseDocType.Purchase).Select(p => p.Total).ToList();
        // Goods sent back were never really bought, so the return comes off the spend — and off what
        // is owed, since the supplier's bill is settled net of it.
        var returned = poRows.Where(p => p.DocType == PurchaseDocType.DebitNote).Sum(p => p.Total);
        var poDue = poRows.Where(p => p.DocType == PurchaseDocType.Purchase).Sum(p => p.Due) - returned;

        // Revenue is cash received, while the Sales/Bookings cards are amounts billed, so the two
        // rarely tie out. Splitting the cash by whether it settled a bill raised in this period or
        // an older one lets the dashboard show why instead of leaving it looking like an error.
        var collected = await _db.Payments.AsNoTracking().RealRevenue()
            .Where(p => p.TenantId == tid && p.PaymentTime >= from && p.PaymentTime <= to
                        && (!own || p.CreatedById == uid))
            .Select(p => new
            {
                p.Amount,
                InvoiceDate = p.Invoice != null ? (DateOnly?)p.Invoice.InvoiceDate : null,
                InvoiceType = p.Invoice != null ? (InvoiceType?)p.Invoice.InvoiceType : null,
            })
            .ToListAsync(ct);
        var revenueCollected = collected.Sum(x => x.Amount);

        // Only Sale and Booking invoices, because those are the two cards this split exists to
        // reconcile against. Counting subscription or adjustment invoices here would put cash in
        // the "for this period's bills" bucket that no billed figure on screen accounts for, and
        // the leftover "paid on another day" would come out negative.
        bool Billable(InvoiceType? t) => t == InvoiceType.Sale || t == InvoiceType.Booking;
        var revenueForPeriod = collected
            .Where(x => Billable(x.InvoiceType) && x.InvoiceDate >= range.From && x.InvoiceDate <= range.To)
            .Sum(x => x.Amount);
        var revenueFromEarlier = collected
            .Where(x => Billable(x.InvoiceType) && x.InvoiceDate < range.From)
            .Sum(x => x.Amount);

        return new PeriodSummary(
            salesRevenue, salesCount,
            totalBookings, bookingsRevenue,
            subsIssuedCount, subsRevenue,
            poTotals.Count, poTotals.Sum() - returned,
            salesDue, bookingsDue, subsDue, poDue,
            salesRevenue + bookingsRevenue + subsRevenue,
            revenueCollected, revenueForPeriod, revenueFromEarlier);
    }

    public async Task<InventoryReport> InventoryAsync(CancellationToken ct = default)
    {
        if (_user.TenantId is null) return new InventoryReport(0, 0, 0, 0, Array.Empty<ExpenseSlice>(), 0, 0);
        var tid = _user.TenantId.Value;
        var soon = BusinessClock.TodayIst().AddDays(30);

        var total = await _db.Skus.CountAsync(s => s.TenantId == tid, ct);
        var low = await _db.Skus.CountAsync(s => s.TenantId == tid && s.IsActive && s.ReorderLevel > 0 && s.StockOnHand <= s.ReorderLevel, ct);
        var outOfStock = await _db.Skus.CountAsync(s => s.TenantId == tid && s.IsActive && s.StockOnHand <= 0, ct);
        var listedInApp = await _db.Skus.CountAsync(s => s.TenantId == tid && s.IsActive && s.IsListedInApp, ct);
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

        return new InventoryReport(total, low, expiring, value, byCategory, outOfStock, listedInApp);
    }
}
