using Microsoft.EntityFrameworkCore;
using Pettle.Application.Clients;
using Pettle.Application.Common;
using Pettle.Application.Reminders;
using Pettle.Domain.Reminders;
using Pettle.Infrastructure.Persistence;

namespace Pettle.Infrastructure.Reminders;

public class ReminderService : IReminderService
{
    private readonly PettleDbContext _db;
    private readonly ICurrentUser _user;
    public ReminderService(PettleDbContext db, ICurrentUser user) { _db = db; _user = user; }

    public async Task<PagedResult<ReminderListItem>> ListAsync(ReminderType? type, ReminderStatus? status, DateOnly? from, DateOnly? to, int page, int pageSize, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return new PagedResult<ReminderListItem>(Array.Empty<ReminderListItem>(), 0, page, pageSize);
        var q = _db.Reminders.AsNoTracking().Include(r => r.PetParent).Where(r => r.TenantId == _user.TenantId);
        if (type.HasValue) q = q.Where(r => r.Type == type.Value);
        if (status.HasValue) q = q.Where(r => r.Status == status.Value);
        if (from.HasValue) q = q.Where(r => r.DueDate >= from.Value);
        if (to.HasValue) q = q.Where(r => r.DueDate <= to.Value);

        var total = await q.CountAsync(ct);
        var p = Math.Max(page, 1); var sz = Math.Clamp(pageSize, 1, 200);
        var items = await q.OrderBy(r => r.DueDate).Skip((p - 1) * sz).Take(sz)
            .Select(r => new ReminderListItem(r.Id, r.Type, r.Title, r.Message, r.DueDate, r.Status,
                r.PetParent != null ? r.PetParent.Name : null,
                r.PetParent != null ? r.PetParent.Phone : null))
            .ToListAsync(ct);
        return new PagedResult<ReminderListItem>(items, total, p, sz);
    }

    public async Task<ReminderOverview> OverviewAsync(int year, int month, CancellationToken ct = default)
    {
        if (_user.TenantId is null)
            return new ReminderOverview(Array.Empty<HeatmapCell>(), 0, new Dictionary<string, int>());

        var first = new DateOnly(year, month, 1);
        var last = first.AddMonths(1).AddDays(-1);
        var rows = await _db.Reminders.AsNoTracking()
            .Where(r => r.TenantId == _user.TenantId && r.DueDate >= first && r.DueDate <= last && r.Status != ReminderStatus.Dismissed)
            .GroupBy(r => r.DueDate)
            .Select(g => new HeatmapCell(g.Key, g.Count()))
            .ToListAsync(ct);

        var byType = await _db.Reminders.AsNoTracking()
            .Where(r => r.TenantId == _user.TenantId && r.DueDate >= first && r.DueDate <= last && r.Status != ReminderStatus.Dismissed)
            .GroupBy(r => r.Type)
            .Select(g => new { Type = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        return new ReminderOverview(rows, rows.Sum(c => c.Count), byType.ToDictionary(x => x.Type.ToString(), x => x.Count));
    }

    public async Task<ReminderListItem> CreateAsync(CreateReminderRequest req, CancellationToken ct = default)
    {
        var r = new Reminder
        {
            Type = req.Type, Title = req.Title, Message = req.Message, DueDate = req.DueDate,
            PetParentId = req.PetParentId, PetId = req.PetId, SkuId = req.SkuId
        };
        _db.Reminders.Add(r);
        await _db.SaveChangesAsync(ct);
        return new ReminderListItem(r.Id, r.Type, r.Title, r.Message, r.DueDate, r.Status, null, null);
    }

    public async Task<bool> SnoozeAsync(Guid id, SnoozeRequest req, CancellationToken ct = default)
    {
        var r = await _db.Reminders.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == _user.TenantId, ct);
        if (r is null) return false;
        r.Status = ReminderStatus.Snoozed;
        r.SnoozedUntil = req.Until;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DismissAsync(Guid id, CancellationToken ct = default)
    {
        var r = await _db.Reminders.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == _user.TenantId, ct);
        if (r is null) return false;
        r.Status = ReminderStatus.Dismissed;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> MarkSentAsync(Guid id, string via, CancellationToken ct = default)
    {
        var r = await _db.Reminders.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == _user.TenantId, ct);
        if (r is null) return false;
        r.Status = ReminderStatus.Sent;
        r.SentAt = DateTimeOffset.UtcNow;
        r.SentVia = via;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ===========================================================================================
    //  Derived reminder feed — Birthdays, Upcoming Bookings, Vaccinations, Reorder, Expiry, etc.
    //  These are computed live from underlying entities; nothing is persisted.
    // ===========================================================================================

    public async Task<IReadOnlyList<ReminderFeedItem>> FeedAsync(string type, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return Array.Empty<ReminderFeedItem>();
        var key = (type ?? "").Trim().ToLowerInvariant();
        return key switch
        {
            "birthdays"          => await BirthdaysAsync(from, to, ct),
            "upcoming-bookings"  => await UpcomingBookingsAsync(from, to, ct),
            "vaccinations"       => await VaccinationsAsync(from, to, ct),
            "bookings"           => await StoredRemindersAsync(ReminderType.BookingFollowUp, from, to, ct),
            "reorder"            => await ReorderSkusAsync(ct),
            "inventory-expiry"   => await ExpirySkusAsync(from, to, ct),
            "low-inventory"      => await LowInventoryAsync(ct),
            "subscriptions"      => await SubscriptionExpiryAsync(from, to, ct),
            _ => Array.Empty<ReminderFeedItem>(),
        };
    }

    public async Task<ReminderCalendarResponse> CalendarAsync(int year, int month, CancellationToken ct = default)
    {
        if (_user.TenantId is null)
            return new ReminderCalendarResponse(year, month, new Dictionary<string, int>(), 0, Array.Empty<ReminderCalendarDay>());

        var firstDay = new DateOnly(year, month, 1);
        var lastDay = firstDay.AddMonths(1).AddDays(-1);

        // Gather all items from each non-stockless type for the month.
        var birthdays = await BirthdaysAsync(firstDay, lastDay, ct);
        var bookings = await UpcomingBookingsAsync(firstDay, lastDay, ct);
        var vaccinations = await VaccinationsAsync(firstDay, lastDay, ct);
        var followUps = await StoredRemindersAsync(ReminderType.BookingFollowUp, firstDay, lastDay, ct);
        var expiries = await ExpirySkusAsync(firstDay, lastDay, ct);
        var subExpiries = await SubscriptionExpiryAsync(firstDay, lastDay, ct);
        // Reorder/low-inventory have no specific date — surfaced on tabs only, not on calendar.

        var totals = new Dictionary<string, int>
        {
            ["birthdays"] = birthdays.Count,
            ["upcoming-bookings"] = bookings.Count,
            ["vaccinations"] = vaccinations.Count,
            ["bookings"] = followUps.Count,
            ["inventory-expiry"] = expiries.Count,
            ["subscriptions"] = subExpiries.Count,
        };

        var perDay = birthdays
            .Concat(bookings).Concat(vaccinations).Concat(followUps).Concat(expiries).Concat(subExpiries)
            .Where(x => x.DueDate >= firstDay && x.DueDate <= lastDay)
            .GroupBy(x => x.DueDate)
            .Select(g => new ReminderCalendarDay(g.Key, g.Count()))
            .OrderBy(x => x.Date)
            .ToList();

        return new ReminderCalendarResponse(year, month, totals, totals.Values.Sum(), perDay);
    }

    private async Task<IReadOnlyList<ReminderFeedItem>> BirthdaysAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        var tid = _user.TenantId!.Value;
        // Only pets with birthday reminders enabled; reminder fires 1 day prior to birthday.
        var pets = await _db.Pets.AsNoTracking()
            .Where(p => p.TenantId == tid && p.Birthday != null && p.BirthdayReminderEnabled)
            .Include(p => p.PetParent)
            .Select(p => new {
                p.Id, p.Name, p.Birthday,
                ParentName = p.PetParent != null ? p.PetParent.Name : null,
                ParentPhone = p.PetParent != null ? p.PetParent.Phone : null,
                ParentEmail = p.PetParent != null ? p.PetParent.Email : null,
            })
            .ToListAsync(ct);

        var items = new List<ReminderFeedItem>();
        foreach (var p in pets)
        {
            if (p.Birthday is null) continue;
            for (int year = from.Year; year <= to.Year + 1; year++)
            {
                DateOnly occurrence;
                try { occurrence = new DateOnly(year, p.Birthday.Value.Month, p.Birthday.Value.Day); }
                catch { continue; /* Feb 29 in non-leap year */ }
                // Reminder fires 1 day prior to birthday
                var reminderDate = occurrence.AddDays(-1);
                if (reminderDate < from || reminderDate > to) continue;
                items.Add(new ReminderFeedItem(
                    Id: $"birthday:{p.Id}:{occurrence:O}",
                    Type: "Birthday",
                    Title: $"{p.Name}'s birthday tomorrow!",
                    Subtitle: p.ParentName,
                    DueDate: reminderDate,
                    ContactName: p.ParentName,
                    Phone: p.ParentPhone,
                    Email: p.ParentEmail,
                    RelatedId: p.Id));
            }
        }
        return items.OrderBy(x => x.DueDate).ToList();
    }

    private async Task<IReadOnlyList<ReminderFeedItem>> UpcomingBookingsAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        var tid = _user.TenantId!.Value;
        var rows = await _db.Bookings.AsNoTracking()
            .Include(b => b.PetParent)
            .Where(b => b.TenantId == tid
                        && b.BookingDate >= from && b.BookingDate <= to
                        && b.Services.Any(s => s.Status == Pettle.Domain.Bookings.BookingStatus.Upcoming
                                              || s.Status == Pettle.Domain.Bookings.BookingStatus.Accepted))
            .OrderBy(b => b.BookingDate)
            .Take(500)
            .Select(b => new {
                b.Id, b.BookingDate, b.TotalBillingAmount,
                ParentName = b.PetParent != null ? b.PetParent.Name : "(unknown)",
                ParentPhone = b.PetParent != null ? b.PetParent.Phone : null,
                ParentEmail = b.PetParent != null ? b.PetParent.Email : null,
                FirstSvc = b.Services.Select(s => s.ServiceType).FirstOrDefault().ToString()
            })
            .ToListAsync(ct);

        return rows.Select(r => new ReminderFeedItem(
            Id: $"booking:{r.Id}",
            Type: "UpcomingBooking",
            Title: $"{r.FirstSvc} booking",
            Subtitle: $"{r.ParentName} · ₹{r.TotalBillingAmount:N0}",
            DueDate: r.BookingDate,
            ContactName: r.ParentName,
            Phone: r.ParentPhone,
            Email: r.ParentEmail,
            RelatedId: r.Id
        )).ToList();
    }

    private async Task<IReadOnlyList<ReminderFeedItem>> VaccinationsAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        var tid = _user.TenantId!.Value;
        // Annual cycle: next due = GivenOn + 1 year. If no GivenOn, skip.
        var rows = await _db.PetVaccinations.AsNoTracking()
            .Include(v => v.Pet).ThenInclude(p => p!.PetParent)
            .Where(v => v.TenantId == tid && v.GivenOn != null)
            .ToListAsync(ct);

        var items = new List<ReminderFeedItem>();
        foreach (var v in rows)
        {
            var given = v.GivenOn!.Value;
            var nextDue = given.AddYears(1);
            if (nextDue < from || nextDue > to) continue;
            items.Add(new ReminderFeedItem(
                Id: $"vax:{v.Id}",
                Type: "Vaccination",
                Title: $"{v.VaccineName} due — {v.Pet?.Name ?? "(pet)"}",
                Subtitle: v.Pet?.PetParent?.Name,
                DueDate: nextDue,
                ContactName: v.Pet?.PetParent?.Name,
                Phone: v.Pet?.PetParent?.Phone,
                Email: v.Pet?.PetParent?.Email,
                RelatedId: v.PetId));
        }
        return items.OrderBy(x => x.DueDate).ToList();
    }

    private async Task<IReadOnlyList<ReminderFeedItem>> StoredRemindersAsync(ReminderType type, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var tid = _user.TenantId!.Value;
        return await _db.Reminders.AsNoTracking()
            .Include(r => r.PetParent)
            .Where(r => r.TenantId == tid && r.Type == type && r.Status == ReminderStatus.Pending
                        && r.DueDate >= from && r.DueDate <= to)
            .OrderBy(r => r.DueDate)
            .Select(r => new ReminderFeedItem(
                r.Id.ToString(),
                r.Type.ToString(),
                r.Title,
                r.Message,
                r.DueDate,
                r.PetParent != null ? r.PetParent.Name : null,
                r.PetParent != null ? r.PetParent.Phone : null,
                r.PetParent != null ? r.PetParent.Email : null,
                r.PetParentId))
            .ToListAsync(ct);
    }

    private async Task<IReadOnlyList<ReminderFeedItem>> ReorderSkusAsync(CancellationToken ct)
    {
        var tid = _user.TenantId!.Value;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var rows = await _db.Skus.AsNoTracking()
            .Where(s => s.TenantId == tid && s.IsActive && s.StockOnHand <= s.ReorderLevel && s.ReorderLevel > 0)
            .OrderBy(s => s.StockOnHand)
            .Take(500)
            .Select(s => new { s.Id, s.Code, s.Name, s.StockOnHand, s.ReorderLevel, s.Unit })
            .ToListAsync(ct);

        return rows.Select(s => new ReminderFeedItem(
            Id: $"reorder:{s.Id}",
            Type: "Reorder",
            Title: s.Name,
            Subtitle: $"Stock: {s.StockOnHand} {s.Unit} · Reorder at {s.ReorderLevel}",
            DueDate: today,
            ContactName: null, Phone: null, Email: null, RelatedId: s.Id)).ToList();
    }

    private async Task<IReadOnlyList<ReminderFeedItem>> ExpirySkusAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        var tid = _user.TenantId!.Value;
        var rows = await _db.Skus.AsNoTracking()
            .Where(s => s.TenantId == tid && s.IsActive && s.TrackExpiry
                        && s.NearestExpiry != null && s.NearestExpiry >= from && s.NearestExpiry <= to)
            .OrderBy(s => s.NearestExpiry)
            .Take(500)
            .Select(s => new { s.Id, s.Code, s.Name, s.NearestExpiry, s.StockOnHand, s.Unit })
            .ToListAsync(ct);

        return rows.Select(s => new ReminderFeedItem(
            Id: $"expiry:{s.Id}",
            Type: "InventoryExpiry",
            Title: s.Name,
            Subtitle: $"Expires · stock {s.StockOnHand} {s.Unit}",
            DueDate: s.NearestExpiry!.Value,
            ContactName: null, Phone: null, Email: null, RelatedId: s.Id)).ToList();
    }

    private async Task<IReadOnlyList<ReminderFeedItem>> SubscriptionExpiryAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        var tid = _user.TenantId!.Value;
        // Same "fires 1 day prior" convention as Birthdays: only plans still Active (a Frozen plan
        // is paused by staff choice, not counting down to its date the same way) are eligible, and
        // the alert date is ValidUntil - 1 day, not ValidUntil itself.
        var rows = await _db.IssuedSubscriptions.AsNoTracking()
            .Include(s => s.Package).Include(s => s.PetParent)
            .Where(s => s.TenantId == tid && s.Status == Pettle.Domain.Subscriptions.IssuedSubscriptionStatus.Active
                        && s.ValidUntil >= from && s.ValidUntil <= to.AddDays(1))
            .Select(s => new {
                s.Id, s.ValidUntil, PackageName = s.Package!.Name,
                ParentName = s.PetParent != null ? s.PetParent.Name : null,
                ParentPhone = s.PetParent != null ? s.PetParent.Phone : null,
                ParentEmail = s.PetParent != null ? s.PetParent.Email : null,
            })
            .ToListAsync(ct);

        var items = new List<ReminderFeedItem>();
        foreach (var s in rows)
        {
            var reminderDate = s.ValidUntil.AddDays(-1);
            if (reminderDate < from || reminderDate > to) continue;
            items.Add(new ReminderFeedItem(
                Id: $"subscription:{s.Id}",
                Type: "SubscriptionExpiry",
                Title: $"{s.PackageName} expires tomorrow",
                Subtitle: s.ParentName,
                DueDate: reminderDate,
                ContactName: s.ParentName,
                Phone: s.ParentPhone,
                Email: s.ParentEmail,
                RelatedId: s.Id));
        }
        return items.OrderBy(x => x.DueDate).ToList();
    }

    public async Task<bool> SetBirthdayReminderAsync(Guid petId, bool enabled, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return false;
        var pet = await _db.Pets.FirstOrDefaultAsync(p => p.Id == petId && p.TenantId == _user.TenantId, ct);
        if (pet is null) return false;
        pet.BirthdayReminderEnabled = enabled;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private async Task<IReadOnlyList<ReminderFeedItem>> LowInventoryAsync(CancellationToken ct)
    {
        var tid = _user.TenantId!.Value;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        // Stricter than reorder: at or below half the reorder level (or zero stock with no reorder set).
        var rows = await _db.Skus.AsNoTracking()
            .Where(s => s.TenantId == tid && s.IsActive
                        && ((s.ReorderLevel > 0 && s.StockOnHand * 2 <= s.ReorderLevel)
                            || (s.ReorderLevel == 0 && s.StockOnHand <= 0)))
            .OrderBy(s => s.StockOnHand)
            .Take(500)
            .Select(s => new { s.Id, s.Name, s.StockOnHand, s.Unit, s.ReorderLevel })
            .ToListAsync(ct);

        return rows.Select(s => new ReminderFeedItem(
            Id: $"low:{s.Id}",
            Type: "LowInventory",
            Title: s.Name,
            Subtitle: $"Critically low: {s.StockOnHand} {s.Unit} (reorder {s.ReorderLevel})",
            DueDate: today,
            ContactName: null, Phone: null, Email: null, RelatedId: s.Id)).ToList();
    }
}
