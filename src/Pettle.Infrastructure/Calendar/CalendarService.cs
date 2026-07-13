using Microsoft.EntityFrameworkCore;
using Pettle.Application.Calendar;
using Pettle.Application.Common;
using Pettle.Application.Common.Errors;
using Pettle.Domain.Bookings;
using Pettle.Infrastructure.Persistence;

namespace Pettle.Infrastructure.Calendar;

public class CalendarService : ICalendarService
{
    private readonly PettleDbContext _db;
    private readonly ICurrentUser _user;

    public CalendarService(PettleDbContext db, ICurrentUser user) { _db = db; _user = user; }

    public async Task<IReadOnlyList<CalendarEvent>> EventsAsync(
        DateOnly fromDate, DateOnly toDate, string? serviceType, bool overnightOnly, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return Array.Empty<CalendarEvent>();
        if (toDate < fromDate) (fromDate, toDate) = (toDate, fromDate);
        var tid = _user.TenantId.Value;

        BookingServiceType? typeFilter = null;
        if (!string.IsNullOrWhiteSpace(serviceType)
            && Enum.TryParse<BookingServiceType>(serviceType, true, out var st))
            typeFilter = st;

        var events = new List<CalendarEvent>();

        // Boarding: overlap test (CheckIn <= toDate AND CheckOut >= fromDate)
        if (typeFilter is null or BookingServiceType.Boarding)
        {
            var boarding =
                from d in _db.BoardingDetails.AsNoTracking()
                join s in _db.BookingServices.AsNoTracking() on d.BookingServiceId equals s.Id
                join b in _db.Bookings.AsNoTracking() on s.BookingId equals b.Id
                join p in _db.PetParents.AsNoTracking() on b.PetParentId equals p.Id
                join pet in _db.Pets.AsNoTracking() on s.PetId equals pet.Id
                where d.TenantId == tid && d.CheckInDate <= toDate && d.CheckOutDate >= fromDate
                      && s.Status != BookingStatus.Cancelled
                      && s.Status != BookingStatus.Rejected
                      && s.Status != BookingStatus.NoShow
                select new
                {
                    s.Id,
                    s.BookingId,
                    PetParentId = p.Id,
                    ParentName = p.Name,
                    PetName = pet.Name,
                    s.Status,
                    Start = d.CheckInDate,
                    End = d.CheckOutDate,
                    d.CheckInTime,
                    d.CheckOutTime,
                    d.KennelId,
                    d.KennelLabel,
                    d.BoardingType,
                    s.ServiceName,
                };

            foreach (var b in await boarding.ToListAsync(ct))
            {
                var overnight = b.End > b.Start; // spans at least one night
                if (overnightOnly && !overnight) continue;
                events.Add(new CalendarEvent(
                    b.Id, b.BookingId, b.PetParentId, b.ParentName, b.PetName,
                    BookingServiceType.Boarding, b.Status,
                    b.Start, b.End,
                    b.CheckInTime, b.CheckOutTime,
                    AllDay: true,
                    IsOvernight: overnight,
                    StaffName: null, StaffId: null,
                    b.KennelLabel, b.KennelId, b.BoardingType, b.ServiceName));
            }
        }

        if (overnightOnly) return events;  // skip non-overnight service types entirely

        if (typeFilter is null or BookingServiceType.Grooming)
            events.AddRange(await TimedEventsAsync(BookingServiceType.Grooming, tid, fromDate, toDate, ct));

        if (typeFilter is null or BookingServiceType.Vet)
            events.AddRange(await TimedEventsAsync(BookingServiceType.Vet, tid, fromDate, toDate, ct));

        if (typeFilter is null or BookingServiceType.DayCare)
            events.AddRange(await TimedEventsAsync(BookingServiceType.DayCare, tid, fromDate, toDate, ct));

        return events
            .OrderBy(e => e.StartDate)
            .ThenBy(e => e.StartTime ?? TimeOnly.MinValue)
            .ToList();
    }

    private async Task<List<CalendarEvent>> TimedEventsAsync(
        BookingServiceType st, Guid tid, DateOnly rangeStart, DateOnly rangeEnd, CancellationToken ct)
    {
        // Switch on table per service type — same projected shape.
        var q = st switch
        {
            BookingServiceType.Grooming =>
                from d in _db.GroomingDetails.AsNoTracking()
                join s in _db.BookingServices.AsNoTracking() on d.BookingServiceId equals s.Id
                join b in _db.Bookings.AsNoTracking() on s.BookingId equals b.Id
                join p in _db.PetParents.AsNoTracking() on b.PetParentId equals p.Id
                join pet in _db.Pets.AsNoTracking() on s.PetId equals pet.Id
                where d.TenantId == tid && d.ServiceDate >= rangeStart && d.ServiceDate <= rangeEnd
                      && s.Status != BookingStatus.Cancelled
                      && s.Status != BookingStatus.Rejected
                      && s.Status != BookingStatus.NoShow
                select new TimedRow(s.Id, s.BookingId, p.Id, p.Name, pet.Name, s.Status,
                    d.ServiceDate, d.StartTime, d.EndTime, d.StaffId, d.StaffName, s.ServiceName),

            BookingServiceType.Vet =>
                from d in _db.VetDetails.AsNoTracking()
                join s in _db.BookingServices.AsNoTracking() on d.BookingServiceId equals s.Id
                join b in _db.Bookings.AsNoTracking() on s.BookingId equals b.Id
                join p in _db.PetParents.AsNoTracking() on b.PetParentId equals p.Id
                join pet in _db.Pets.AsNoTracking() on s.PetId equals pet.Id
                where d.TenantId == tid && d.ServiceDate >= rangeStart && d.ServiceDate <= rangeEnd
                      && s.Status != BookingStatus.Cancelled
                      && s.Status != BookingStatus.Rejected
                      && s.Status != BookingStatus.NoShow
                select new TimedRow(s.Id, s.BookingId, p.Id, p.Name, pet.Name, s.Status,
                    d.ServiceDate, d.StartTime, d.EndTime, d.StaffId, d.StaffName, s.ServiceName),

            BookingServiceType.DayCare =>
                from d in _db.DayCareDetails.AsNoTracking()
                join s in _db.BookingServices.AsNoTracking() on d.BookingServiceId equals s.Id
                join b in _db.Bookings.AsNoTracking() on s.BookingId equals b.Id
                join p in _db.PetParents.AsNoTracking() on b.PetParentId equals p.Id
                join pet in _db.Pets.AsNoTracking() on s.PetId equals pet.Id
                where d.TenantId == tid && d.ServiceDate >= rangeStart && d.ServiceDate <= rangeEnd
                      && s.Status != BookingStatus.Cancelled
                      && s.Status != BookingStatus.Rejected
                      && s.Status != BookingStatus.NoShow
                select new TimedRow(s.Id, s.BookingId, p.Id, p.Name, pet.Name, s.Status,
                    d.ServiceDate, d.StartTime, d.EndTime, d.StaffId, d.StaffName, s.ServiceName),

            _ => throw new ArgumentOutOfRangeException(nameof(st))
        };

        var rows = await q.ToListAsync(ct);
        return rows.Select(r => new CalendarEvent(
            r.Id, r.BookingId, r.PetParentId, r.ParentName, r.PetName,
            st, r.Status,
            r.Date, r.Date,
            r.StartTime, r.EndTime,
            AllDay: false,
            IsOvernight: false,
            r.StaffName, r.StaffId,
            KennelLabel: null, KennelId: null, BoardingType: null,
            r.ServiceName)).ToList();
    }

    private record TimedRow(
        Guid Id, Guid BookingId, Guid PetParentId, string ParentName, string PetName,
        BookingStatus Status, DateOnly Date, TimeOnly? StartTime, TimeOnly? EndTime,
        Guid? StaffId, string? StaffName, string? ServiceName);

    public async Task<CalendarCounters> CountersAsync(DateOnly date, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return new CalendarCounters(0, 0, 0, 0);
        var tid = _user.TenantId.Value;

        var boarding = await _db.BoardingDetails.AsNoTracking()
            .Where(d => d.TenantId == tid
                && (d.CheckInDate == date || d.CheckOutDate == date
                    || (d.CheckInDate <= date && d.CheckOutDate >= date)))
            .Select(d => new { d.CheckInDate, d.CheckOutDate })
            .ToListAsync(ct);

        var dayCareCount = await _db.DayCareDetails.AsNoTracking()
            .CountAsync(d => d.TenantId == tid && d.ServiceDate == date, ct);

        var checkIn = boarding.Count(b => b.CheckInDate == date);
        var checkOut = boarding.Count(b => b.CheckOutDate == date);
        // Same-day in/out = day boarding; otherwise overnight.
        var sameDay = boarding.Count(b => b.CheckInDate == date && b.CheckOutDate == date);
        var nightBoarding = boarding.Count(b => b.CheckInDate <= date && b.CheckOutDate > date);
        var dayBoarding = sameDay + dayCareCount;

        return new CalendarCounters(checkIn, checkOut, dayBoarding, nightBoarding);
    }

    public async Task<CalendarEvent?> RescheduleAsync(Guid bookingServiceId, RescheduleRequest req, CancellationToken ct = default)
    {
        if (_user.TenantId is null) throw AppException.Forbidden();
        var tid = _user.TenantId.Value;

        var svc = await _db.BookingServices.FirstOrDefaultAsync(s => s.Id == bookingServiceId && s.TenantId == tid, ct);
        if (svc is null) return null;
        if (svc.Status is BookingStatus.Cancelled or BookingStatus.Rejected or BookingStatus.CheckedOut or BookingStatus.NoShow)
            throw AppException.BusinessRule($"Cannot reschedule a service that's already {svc.Status.Humanize().ToLower()}.");

        switch (svc.ServiceType)
        {
            case BookingServiceType.Boarding:
                await RescheduleBoardingAsync(svc, req, tid, ct); break;
            case BookingServiceType.Grooming:
                await RescheduleTimedAsync<Pettle.Domain.Bookings.GroomingDetail>(svc, req, tid, ct,
                    _db.GroomingDetails, (d, date) => d.ServiceDate = date,
                    (d, start, end) => { d.StartTime = start; d.EndTime = end; },
                    d => (d.ServiceDate, d.StartTime, d.EndTime, d.StaffId)); break;
            case BookingServiceType.Vet:
                await RescheduleTimedAsync<Pettle.Domain.Bookings.VetDetail>(svc, req, tid, ct,
                    _db.VetDetails, (d, date) => d.ServiceDate = date,
                    (d, start, end) => { d.StartTime = start; d.EndTime = end; },
                    d => (d.ServiceDate, d.StartTime, d.EndTime, d.StaffId)); break;
            case BookingServiceType.DayCare:
                await RescheduleTimedAsync<Pettle.Domain.Bookings.DayCareDetail>(svc, req, tid, ct,
                    _db.DayCareDetails, (d, date) => d.ServiceDate = date,
                    (d, start, end) => { d.StartTime = start; d.EndTime = end; },
                    d => (d.ServiceDate, d.StartTime, d.EndTime, d.StaffId)); break;
        }

        await _db.SaveChangesAsync(ct);

        // Re-project this single event to return the post-update calendar row.
        var refreshed = await EventsAsync(req.NewStartDate, req.NewEndDate ?? req.NewStartDate, svc.ServiceType.ToString(), false, ct);
        return refreshed.FirstOrDefault(e => e.Id == bookingServiceId);
    }

    private async Task RescheduleBoardingAsync(BookingService svc, RescheduleRequest req, Guid tid, CancellationToken ct)
    {
        var d = await _db.BoardingDetails.FirstOrDefaultAsync(x => x.BookingServiceId == svc.Id && x.TenantId == tid, ct)
                ?? throw AppException.BusinessRule("Boarding detail missing for this service.");

        var newStart = req.NewStartDate;
        var newEnd = req.NewEndDate ?? newStart.AddDays(d.CheckOutDate.DayNumber - d.CheckInDate.DayNumber);
        if (newEnd < newStart)
            throw AppException.Validation("Invalid range",
                new Dictionary<string, string[]> { ["newEndDate"] = new[] { "End date must be on or after start date." } });

        var kennelId = req.NewKennelId ?? d.KennelId;
        if (kennelId.HasValue)
        {
            // Overlapping other boarding in same kennel?
            var conflict = await _db.BoardingDetails.AsNoTracking()
                .Where(x => x.TenantId == tid && x.BookingServiceId != svc.Id && x.KennelId == kennelId
                    && x.CheckInDate <= newEnd && x.CheckOutDate >= newStart)
                .AnyAsync(ct);
            if (conflict) throw AppException.Conflict("Kennel is already booked for an overlapping date range.");

            var blocked = await _db.KennelBlockings.AsNoTracking()
                .Where(x => x.TenantId == tid && x.KennelId == kennelId
                    && x.FromDate <= newEnd && x.ToDate >= newStart)
                .AnyAsync(ct);
            if (blocked) throw AppException.Conflict("Kennel is blocked during the requested range.");

            if (req.NewKennelId.HasValue && req.NewKennelId != d.KennelId)
            {
                var kennel = await _db.Kennels.FirstOrDefaultAsync(k => k.Id == kennelId && k.TenantId == tid, ct)
                             ?? throw AppException.Validation("Unknown kennel",
                                 new Dictionary<string, string[]> { ["newKennelId"] = new[] { "Kennel not found." } });
                d.KennelId = kennel.Id;
                d.KennelLabel = kennel.Name;
            }
        }

        d.CheckInDate = newStart;
        d.CheckOutDate = newEnd;
    }

    private async Task RescheduleTimedAsync<TDetail>(
        BookingService svc, RescheduleRequest req, Guid tid, CancellationToken ct,
        DbSet<TDetail> set,
        Action<TDetail, DateOnly> setDate,
        Action<TDetail, TimeOnly?, TimeOnly?> setTimes,
        Func<TDetail, (DateOnly date, TimeOnly? start, TimeOnly? end, Guid? staffId)> read)
        where TDetail : class
    {
        // Locate the detail row via a typed projection so we can read current values.
        var entries = await set.Where(e => EF.Property<Guid>(e, "BookingServiceId") == svc.Id
                                           && EF.Property<Guid>(e, "TenantId") == tid).ToListAsync(ct);
        var detail = entries.FirstOrDefault()
                     ?? throw AppException.BusinessRule($"This {svc.ServiceType.Humanize().ToLower()} booking is missing its scheduling details — please contact support.");

        var (curDate, curStart, curEnd, staffId) = read(detail);
        var newDate = req.NewStartDate;
        var newStart = req.NewStartTime ?? curStart;
        var newEnd = req.NewEndTime ?? curEnd;

        // Staff overlap check: same staff, same date, time overlap (when both ranges have times)
        if (staffId.HasValue && newStart.HasValue && newEnd.HasValue)
        {
            var conflictGrooming = await _db.GroomingDetails.AsNoTracking()
                .AnyAsync(x => x.TenantId == tid && x.StaffId == staffId && x.ServiceDate == newDate
                    && x.BookingServiceId != svc.Id
                    && x.StartTime != null && x.EndTime != null
                    && x.StartTime < newEnd && x.EndTime > newStart, ct);
            var conflictVet = await _db.VetDetails.AsNoTracking()
                .AnyAsync(x => x.TenantId == tid && x.StaffId == staffId && x.ServiceDate == newDate
                    && x.BookingServiceId != svc.Id
                    && x.StartTime != null && x.EndTime != null
                    && x.StartTime < newEnd && x.EndTime > newStart, ct);
            var conflictDayCare = await _db.DayCareDetails.AsNoTracking()
                .AnyAsync(x => x.TenantId == tid && x.StaffId == staffId && x.ServiceDate == newDate
                    && x.BookingServiceId != svc.Id
                    && x.StartTime != null && x.EndTime != null
                    && x.StartTime < newEnd && x.EndTime > newStart, ct);
            if (conflictGrooming || conflictVet || conflictDayCare)
                throw AppException.Conflict("Staff member is already booked for an overlapping time slot.");
        }

        setDate(detail, newDate);
        setTimes(detail, newStart, newEnd);
    }

    // ---- Manual appointments (add/edit/delete) ----
    public async Task<IReadOnlyList<CalendarAppointmentDto>> ListAppointmentsAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return Array.Empty<CalendarAppointmentDto>();
        if (to < from) (from, to) = (to, from);
        return await _db.CalendarAppointments.AsNoTracking()
            .Where(a => a.TenantId == _user.TenantId && a.Date >= from && a.Date <= to)
            .OrderBy(a => a.Date).ThenBy(a => a.StartTime)
            .Select(a => new CalendarAppointmentDto(a.Id, a.Title, a.Date, a.StartTime, a.EndTime, a.Notes, a.Color))
            .ToListAsync(ct);
    }

    public async Task<CalendarAppointmentDto> CreateAppointmentAsync(CreateOrUpdateAppointmentRequest req, CancellationToken ct = default)
    {
        if (req.EndTime is { } e && req.StartTime is { } s && e < s)
            throw AppException.Validation("Invalid time range",
                new Dictionary<string, string[]> { ["endTime"] = new[] { "End time cannot be before start time." } });
        var a = new Pettle.Domain.Calendar.CalendarAppointment
        {
            Title = req.Title, Date = req.Date, StartTime = req.StartTime,
            EndTime = req.EndTime, Notes = req.Notes, Color = req.Color,
        };
        _db.CalendarAppointments.Add(a);
        await _db.SaveChangesAsync(ct);
        return new CalendarAppointmentDto(a.Id, a.Title, a.Date, a.StartTime, a.EndTime, a.Notes, a.Color);
    }

    public async Task<CalendarAppointmentDto?> UpdateAppointmentAsync(Guid id, CreateOrUpdateAppointmentRequest req, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return null;
        var a = await _db.CalendarAppointments.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == _user.TenantId, ct);
        if (a is null) return null;
        if (req.EndTime is { } e && req.StartTime is { } s && e < s)
            throw AppException.Validation("Invalid time range",
                new Dictionary<string, string[]> { ["endTime"] = new[] { "End time cannot be before start time." } });
        a.Title = req.Title; a.Date = req.Date; a.StartTime = req.StartTime;
        a.EndTime = req.EndTime; a.Notes = req.Notes; a.Color = req.Color;
        await _db.SaveChangesAsync(ct);
        return new CalendarAppointmentDto(a.Id, a.Title, a.Date, a.StartTime, a.EndTime, a.Notes, a.Color);
    }

    public async Task<bool> DeleteAppointmentAsync(Guid id, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return false;
        var a = await _db.CalendarAppointments.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == _user.TenantId, ct);
        if (a is null) return false;
        _db.CalendarAppointments.Remove(a);
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
