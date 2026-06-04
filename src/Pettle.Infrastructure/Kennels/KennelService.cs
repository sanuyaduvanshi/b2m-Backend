using Microsoft.EntityFrameworkCore;
using Pettle.Application.Common;
using Pettle.Application.Common.Errors;
using Pettle.Application.Kennels;
using Pettle.Domain.Bookings;
using Pettle.Domain.Kennels;
using Pettle.Infrastructure.Persistence;

namespace Pettle.Infrastructure.Kennels;

public class KennelService : IKennelService
{
    private readonly PettleDbContext _db;
    private readonly ICurrentUser _user;
    public KennelService(PettleDbContext db, ICurrentUser user) { _db = db; _user = user; }

    public async Task<IReadOnlyList<KennelListItem>> ListAsync(CancellationToken ct = default)
    {
        if (_user.TenantId is null) return Array.Empty<KennelListItem>();
        return await _db.Kennels.AsNoTracking()
            .Where(k => k.TenantId == _user.TenantId)
            .OrderBy(k => k.KennelType).ThenBy(k => k.Name)
            .Select(k => new KennelListItem(k.Id, k.Name, k.KennelType, k.SizeClass, k.Capacity, k.PricePerNight, k.AllowedSpecies, k.IsActive))
            .ToListAsync(ct);
    }

    public async Task<KennelListItem> CreateAsync(CreateOrUpdateKennelRequest req, CancellationToken ct = default)
    {
        var k = new Kennel
        {
            Name = req.Name, KennelType = req.KennelType, SizeClass = req.SizeClass,
            Capacity = req.Capacity, PricePerNight = req.PricePerNight, AllowedSpecies = req.AllowedSpecies, IsActive = req.IsActive
        };
        _db.Kennels.Add(k);
        await _db.SaveChangesAsync(ct);
        return new KennelListItem(k.Id, k.Name, k.KennelType, k.SizeClass, k.Capacity, k.PricePerNight, k.AllowedSpecies, k.IsActive);
    }

    public async Task<KennelListItem?> UpdateAsync(Guid id, CreateOrUpdateKennelRequest req, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return null;
        var k = await _db.Kennels.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == _user.TenantId, ct);
        if (k is null) return null;
        k.Name = req.Name; k.KennelType = req.KennelType; k.SizeClass = req.SizeClass;
        k.Capacity = req.Capacity; k.PricePerNight = req.PricePerNight; k.AllowedSpecies = req.AllowedSpecies; k.IsActive = req.IsActive;
        await _db.SaveChangesAsync(ct);
        return new KennelListItem(k.Id, k.Name, k.KennelType, k.SizeClass, k.Capacity, k.PricePerNight, k.AllowedSpecies, k.IsActive);
    }

    public async Task<IReadOnlyList<KennelLiveGroup>> LiveGridAsync(DateOnly date, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return Array.Empty<KennelLiveGroup>();
        var tid = _user.TenantId.Value;

        var kennels = await _db.Kennels.AsNoTracking()
            .Where(k => k.TenantId == tid && k.IsActive)
            .OrderBy(k => k.SortOrder).ThenBy(k => k.Name)
            .ToListAsync(ct);

        var groups = await _db.KennelGroups.AsNoTracking()
            .Where(g => g.TenantId == tid)
            .OrderBy(g => g.SortOrder).ThenBy(g => g.Name)
            .ToListAsync(ct);

        var occupied = await _db.BoardingDetails.AsNoTracking()
            .Include(d => d.BookingService).ThenInclude(s => s!.Pet)
            .Include(d => d.BookingService).ThenInclude(s => s!.Booking).ThenInclude(b => b!.PetParent)
            .Where(d => d.TenantId == tid && d.KennelId.HasValue
                        && d.CheckInDate <= date && d.CheckOutDate >= date
                        && (d.BookingService!.Status == BookingStatus.CheckedIn || d.BookingService.Status == BookingStatus.Active))
            .ToListAsync(ct);

        var blockings = await _db.KennelBlockings.AsNoTracking()
            .Where(b => b.TenantId == tid && b.FromDate <= date && b.ToDate >= date)
            .ToListAsync(ct);

        KennelLiveCell MakeCell(Kennel k)
        {
            var occ = occupied.FirstOrDefault(o => o.KennelId == k.Id);
            if (occ is not null)
                return new KennelLiveCell(
                    k.Id, k.Name, k.KennelType, KennelLiveStatus.Occupied,
                    occ.BookingService!.Pet?.Name, occ.BookingService.Booking?.PetParent?.Name,
                    occ.CheckInDate, occ.CheckOutDate,
                    occ.BookingService.BookingId, occ.BookingService.Id,
                    null);
            var blk = blockings.FirstOrDefault(b => b.KennelId == k.Id);
            if (blk is not null)
                return new KennelLiveCell(
                    k.Id, k.Name, k.KennelType, KennelLiveStatus.Blocked,
                    null, null, blk.FromDate, blk.ToDate, null, null, blk.Reason.ToString());
            return new KennelLiveCell(k.Id, k.Name, k.KennelType, KennelLiveStatus.Free, null, null, null, null, null, null, null);
        }

        var result = new List<KennelLiveGroup>();
        foreach (var g in groups)
        {
            var cells = kennels.Where(k => k.GroupId == g.Id).Select(MakeCell).ToList();
            if (cells.Count == 0) continue;
            result.Add(new KennelLiveGroup(g.Id, g.Name, g.Color, g.SortOrder, cells));
        }

        var ungrouped = kennels.Where(k => k.GroupId == null).Select(MakeCell).ToList();
        if (ungrouped.Count > 0)
            result.Add(new KennelLiveGroup(null, "Ungrouped", null, int.MaxValue, ungrouped));

        return result;
    }

    public async Task<IReadOnlyList<KennelTimelineGroup>> TimelineAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return Array.Empty<KennelTimelineGroup>();
        var tid = _user.TenantId.Value;

        var kennels = await _db.Kennels.AsNoTracking()
            .Where(k => k.TenantId == tid && k.IsActive)
            .OrderBy(k => k.SortOrder).ThenBy(k => k.Name)
            .ToListAsync(ct);

        var groups = await _db.KennelGroups.AsNoTracking()
            .Where(g => g.TenantId == tid)
            .OrderBy(g => g.SortOrder).ThenBy(g => g.Name)
            .ToListAsync(ct);

        var slots = await _db.BoardingDetails.AsNoTracking()
            .Include(d => d.BookingService).ThenInclude(s => s!.Pet)
            .Include(d => d.BookingService).ThenInclude(s => s!.Booking).ThenInclude(b => b!.PetParent)
            .Where(d => d.TenantId == tid && d.KennelId.HasValue
                        && d.CheckInDate <= to && d.CheckOutDate >= from
                        && d.BookingService!.Status != BookingStatus.Cancelled
                        && d.BookingService.Status != BookingStatus.Rejected)
            .Select(d => new KennelTimelineSlot(
                d.BookingService!.Id,
                d.BookingService.BookingId,
                d.KennelId!.Value,
                d.BookingService.Pet != null ? d.BookingService.Pet.Name : "(pet)",
                d.BookingService.Booking != null && d.BookingService.Booking.PetParent != null
                    ? d.BookingService.Booking.PetParent.Name : "(parent)",
                d.CheckInDate, d.CheckOutDate,
                d.BookingService.Status.ToString()))
            .ToListAsync(ct);

        KennelTimelineKennel MakeKennelRow(Kennel k) => new(
            k.Id, k.Name,
            slots.Where(s => s.KennelId == k.Id).OrderBy(s => s.CheckIn).ToList());

        var result = new List<KennelTimelineGroup>();
        foreach (var g in groups)
        {
            var rows = kennels.Where(k => k.GroupId == g.Id).Select(MakeKennelRow).ToList();
            if (rows.Count == 0) continue;
            result.Add(new KennelTimelineGroup(g.Id, g.Name, g.Color, g.SortOrder, rows));
        }

        var ungroupedRows = kennels.Where(k => k.GroupId == null).Select(MakeKennelRow).ToList();
        if (ungroupedRows.Count > 0)
            result.Add(new KennelTimelineGroup(null, "Ungrouped", null, int.MaxValue, ungroupedRows));

        return result;
    }

    public async Task<bool> BlockAsync(Guid kennelId, KennelBlockRequest req, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return false;
        var exists = await _db.Kennels.AnyAsync(k => k.Id == kennelId && k.TenantId == _user.TenantId, ct);
        if (!exists) return false;

        // Overlapping block?
        var overlap = await _db.KennelBlockings
            .AnyAsync(b => b.TenantId == _user.TenantId && b.KennelId == kennelId
                && b.FromDate <= req.ToDate && b.ToDate >= req.FromDate, ct);
        if (overlap) throw AppException.Conflict("This kennel is already blocked for an overlapping range.");

        // Occupied by a live boarding for any day in range?
        var occupied = await _db.BoardingDetails
            .AnyAsync(d => d.TenantId == _user.TenantId && d.KennelId == kennelId
                && d.CheckInDate <= req.ToDate && d.CheckOutDate >= req.FromDate
                && (d.BookingService!.Status == BookingStatus.Upcoming
                    || d.BookingService.Status == BookingStatus.CheckedIn
                    || d.BookingService.Status == BookingStatus.Active), ct);
        if (occupied) throw AppException.Conflict("This kennel is occupied during the requested range.");

        _db.KennelBlockings.Add(new KennelBlocking
        {
            KennelId = kennelId, FromDate = req.FromDate, ToDate = req.ToDate, Reason = req.Reason, Notes = req.Notes
        });
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> UnblockAsync(Guid blockingId, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return false;
        var b = await _db.KennelBlockings.FirstOrDefaultAsync(x => x.Id == blockingId && x.TenantId == _user.TenantId, ct);
        if (b is null) return false;
        _db.KennelBlockings.Remove(b);
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
