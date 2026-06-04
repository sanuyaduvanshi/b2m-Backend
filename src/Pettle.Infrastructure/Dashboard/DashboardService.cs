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

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var tid = _user.TenantId.Value;

        // ---------- KPIs ----------
        var upcoming = await _db.BookingServices.CountAsync(s => s.TenantId == tid && s.Status == BookingStatus.Upcoming, ct);
        var active = await _db.BookingServices.CountAsync(s => s.TenantId == tid && (s.Status == BookingStatus.CheckedIn || s.Status == BookingStatus.Active), ct);

        var checkInDue = await _db.BoardingDetails
            .CountAsync(d => d.TenantId == tid && d.CheckInDate == today
                && d.BookingService!.Status == BookingStatus.Upcoming, ct);

        var checkOutDue = await _db.BoardingDetails
            .CountAsync(d => d.TenantId == tid && d.CheckOutDate == today
                && (d.BookingService!.Status == BookingStatus.CheckedIn || d.BookingService.Status == BookingStatus.Active), ct);

        var todayStart = today.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var todayEnd = today.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);
        var revenueToday = await _db.Payments
            .Where(p => p.TenantId == tid && p.PaymentTime >= todayStart && p.PaymentTime <= todayEnd)
            .SumAsync(p => (decimal?)p.Amount, ct) ?? 0m;

        var pendingReminders = await _db.Reminders.CountAsync(r => r.TenantId == tid && r.Status == ReminderStatus.Pending && r.DueDate <= today, ct);
        var lowStock = await _db.Skus.CountAsync(s => s.TenantId == tid && s.IsActive && s.StockOnHand <= s.ReorderLevel, ct);
        var outstanding = await _db.Invoices.CountAsync(i => i.TenantId == tid && i.PaymentStatus != InvoicePaymentStatus.Paid && i.Due > 0, ct);

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
        var fromUtc = today.AddDays(-13).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var rawPayments = await _db.Payments.AsNoTracking()
            .Where(p => p.TenantId == tid && p.PaymentTime >= fromUtc && p.PaymentTime <= todayEnd)
            .Select(p => new { p.PaymentTime, p.Amount })
            .ToListAsync(ct);
        var byDay = rawPayments
            .GroupBy(p => DateOnly.FromDateTime(p.PaymentTime.UtcDateTime))
            .ToDictionary(g => g.Key, g => g.Sum(p => p.Amount));
        var trend = Enumerable.Range(0, 14)
            .Select(i => today.AddDays(-(13 - i)))
            .Select(d => new RevenueDay(d, byDay.GetValueOrDefault(d, 0m)))
            .ToList();

        // ---------- Recent bookings (last 5 by date, then created) ----------
        var recent = await _db.Bookings.AsNoTracking()
            .Where(b => b.TenantId == tid)
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
                b.Services
                    .OrderByDescending(s => (int)s.Status)
                    .Select(s => s.Status.ToString())
                    .FirstOrDefault() ?? "Upcoming"
            ))
            .ToListAsync(ct);

        return new DashboardOverview(
            new DashboardKpis(upcoming, active, checkInDue, checkOutDue, revenueToday, pendingReminders, lowStock, outstanding),
            top,
            trend,
            recent);
    }
}
