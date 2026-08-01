using Microsoft.EntityFrameworkCore;
using Pettle.Application.Common;
using Pettle.Application.Dashboard;
using Pettle.Domain.Bookings;
using Pettle.Domain.Invoices;
using Pettle.Domain.Reminders;
using Pettle.Infrastructure.Persistence;

namespace Pettle.Infrastructure.Dashboard;

public class DashboardService : IDashboardService
{
    private readonly PettleDbContext _db;
    private readonly ICurrentUser _user;
    public DashboardService(PettleDbContext db, ICurrentUser user) { _db = db; _user = user; }

    public async Task<DashboardOverview> OverviewAsync(CancellationToken ct = default)
    {
        if (_user.TenantId is null)
            return new DashboardOverview(
                new DashboardKpis(0, 0, 0, 0, 0, 0, 0, 0),
                Array.Empty<ReminderTile>(),
                Array.Empty<RevenueDay>(),
                Array.Empty<RecentBookingTile>());

        var today = BusinessClock.TodayIst();
        var tid = _user.TenantId.Value;
        // Receptionist-type roles only see business activity they personally created — same
        // restriction already applied to the Bookings/Invoices list pages, mirrored here so the
        // dashboard summary doesn't leak tenant-wide figures to a role that can't see the underlying records.
        var restrictOwn = _user.RestrictToOwnRecords;
        var uid = _user.UserId;

        // ---------- KPIs ----------
        var upcoming = await _db.BookingServices.CountAsync(s => s.TenantId == tid && s.Status == BookingStatus.Upcoming
            && (!restrictOwn || s.CreatedById == uid), ct);
        var active = await _db.BookingServices.CountAsync(s => s.TenantId == tid && (s.Status == BookingStatus.CheckedIn || s.Status == BookingStatus.Active)
            && (!restrictOwn || s.CreatedById == uid), ct);

        var checkInDue = await _db.BoardingDetails
            .CountAsync(d => d.TenantId == tid && d.CheckInDate == today
                && d.BookingService!.Status == BookingStatus.Upcoming
                && (!restrictOwn || d.BookingService.CreatedById == uid), ct);

        var checkOutDue = await _db.BoardingDetails
            .CountAsync(d => d.TenantId == tid && d.CheckOutDate == today
                && (d.BookingService!.Status == BookingStatus.CheckedIn || d.BookingService.Status == BookingStatus.Active)
                && (!restrictOwn || d.BookingService.CreatedById == uid), ct);

        var todayStart = BusinessClock.StartOfDayUtc(today);
        var todayEnd = BusinessClock.EndOfDayUtc(today);
        var revenueToday = await _db.Payments.RealRevenue()
            .Where(p => p.TenantId == tid && p.PaymentTime >= todayStart && p.PaymentTime <= todayEnd
                && (!restrictOwn || p.CreatedById == uid))
            .SumAsync(p => (decimal?)p.Amount, ct) ?? 0m;

        var pendingReminders = await _db.Reminders.CountAsync(r => r.TenantId == tid && r.Status == ReminderStatus.Pending && r.DueDate <= today, ct);
        var lowStock = await _db.Skus.CountAsync(s => s.TenantId == tid && s.IsActive && s.StockOnHand <= s.ReorderLevel, ct);
        var outstanding = await _db.Invoices.CountAsync(i => i.TenantId == tid && i.PaymentStatus != InvoicePaymentStatus.Paid && i.Due > 0
            && (!restrictOwn || i.CreatedById == uid), ct);

        // ---------- Top reminders ----------
        var top = await _db.Reminders.AsNoTracking()
            .Include(r => r.PetParent)
            .Where(r => r.TenantId == tid && r.Status == ReminderStatus.Pending && r.DueDate <= today.AddDays(7))
            .OrderBy(r => r.DueDate)
            .Take(10)
            .Select(r => new ReminderTile(r.Id, r.Title,
                r.PetParent != null ? r.PetParent.Name : null,
                r.PetParent != null ? r.PetParent.Phone : null,
                r.Type.ToString(), r.DueDate))
            .ToListAsync(ct);

        // ---------- Revenue trend (last 14 days, fill gaps with zero) ----------
        var fromUtc = BusinessClock.StartOfDayUtc(today.AddDays(-13));
        var rawPayments = await _db.Payments.AsNoTracking().RealRevenue()
            .Where(p => p.TenantId == tid && p.PaymentTime >= fromUtc && p.PaymentTime <= todayEnd)
            .Select(p => new { p.PaymentTime, p.Amount })
            .ToListAsync(ct);
        var byDay = rawPayments
            .GroupBy(p => BusinessClock.ToIstDate(p.PaymentTime))
            .ToDictionary(g => g.Key, g => g.Sum(p => p.Amount));
        var trend = Enumerable.Range(0, 14)
            .Select(i => today.AddDays(-(13 - i)))
            .Select(d => new RevenueDay(d, byDay.GetValueOrDefault(d, 0m)))
            .ToList();

        // ---------- Recent bookings (last 5 by date, then created) ----------
        var recent = await _db.Bookings.AsNoTracking()
            .Where(b => b.TenantId == tid && (!restrictOwn || b.CreatedById == uid))
            .OrderByDescending(b => b.BookingDate)
            .ThenByDescending(b => b.CreatedAt)
            .Take(5)
            .Select(b => new RecentBookingTile(
                b.Id,
                b.LegacyBookingId,
                b.BookingDate,
                b.PetParent!.Name,
                b.PetParent!.Phone,
                string.Join(", ", b.Services.Select(s => s.ServiceType.ToString()).Distinct()),
                b.TotalBillingAmount,
                b.Services.Any()
                    ? b.Services.OrderBy(s =>
                        s.Status == BookingStatus.Active ? 0 :
                        s.Status == BookingStatus.CheckedIn ? 1 :
                        s.Status == BookingStatus.Upcoming ? 2 :
                        s.Status == BookingStatus.Accepted ? 3 :
                        s.Status == BookingStatus.NoShow ? 4 :
                        s.Status == BookingStatus.CheckedOut ? 5 :
                        s.Status == BookingStatus.Cancelled ? 6 :
                        s.Status == BookingStatus.Rejected ? 7 : 8)
                      .Select(s => s.Status.ToString()).FirstOrDefault()!
                    : "—"
            ))
            .ToListAsync(ct);

        return new DashboardOverview(
            new DashboardKpis(upcoming, active, checkInDue, checkOutDue, revenueToday, pendingReminders, lowStock, outstanding),
            top,
            trend,
            recent);
    }
}
