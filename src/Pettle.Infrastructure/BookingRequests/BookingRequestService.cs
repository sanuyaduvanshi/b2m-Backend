using Microsoft.EntityFrameworkCore;
using Pettle.Application.BookingRequests;
using Pettle.Application.Clients;
using Pettle.Application.Common;
using Pettle.Application.Common.Errors;
using Pettle.Domain.Bookings;
using Pettle.Infrastructure.Persistence;

namespace Pettle.Infrastructure.BookingRequests;

public class BookingRequestService : IBookingRequestService
{
    private readonly PettleDbContext _db;
    private readonly ICurrentUser _user;

    public BookingRequestService(PettleDbContext db, ICurrentUser user) { _db = db; _user = user; }

    public async Task<BookingRequestBoard> ListAsync(string? tab, string? search, int page, int pageSize, CancellationToken ct = default)
    {
        if (_user.TenantId is null)
            return new BookingRequestBoard(new BookingRequestCounts(0, 0, 0, 0),
                new PagedResult<BookingRequestRow>(Array.Empty<BookingRequestRow>(), 0, page, pageSize));

        var q = _db.BookingRequests.AsNoTracking().Where(r => r.TenantId == _user.TenantId);

        var counts = await q.GroupBy(r => r.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var board = new BookingRequestCounts(
            counts.FirstOrDefault(c => c.Status == BookingRequestStatus.Requested)?.Count ?? 0,
            counts.FirstOrDefault(c => c.Status == BookingRequestStatus.Accepted)?.Count ?? 0,
            counts.FirstOrDefault(c => c.Status == BookingRequestStatus.Rejected)?.Count ?? 0,
            counts.FirstOrDefault(c => c.Status == BookingRequestStatus.Converted)?.Count ?? 0);

        switch (tab?.ToLowerInvariant())
        {
            case "requested": q = q.Where(r => r.Status == BookingRequestStatus.Requested); break;
            case "accepted": q = q.Where(r => r.Status == BookingRequestStatus.Accepted); break;
            case "rejected": q = q.Where(r => r.Status == BookingRequestStatus.Rejected); break;
            case "converted": q = q.Where(r => r.Status == BookingRequestStatus.Converted); break;
            case "all":
            case null:
            case "":
                break;
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            q = q.Where(r =>
                r.ParentName.ToLower().Contains(s) ||
                r.Phone.Contains(s) ||
                (r.PetName != null && r.PetName.ToLower().Contains(s)) ||
                (r.Email != null && r.Email.ToLower().Contains(s)));
        }

        var total = await q.CountAsync(ct);
        var p = Math.Max(page, 1);
        var sz = Math.Clamp(pageSize, 1, 200);

        var rows = await q.OrderByDescending(r => r.CreatedAt)
            .Skip((p - 1) * sz).Take(sz)
            .Select(r => new BookingRequestRow(
                r.Id, r.LegacyRequestId, r.PetParentId, r.ParentName, r.Phone, r.Email,
                r.PetName, r.RequestedServiceType, r.RequestedDate, r.Status,
                r.Notes, r.RejectionReason, r.ConvertedBookingId, r.CreatedAt))
            .ToListAsync(ct);

        return new BookingRequestBoard(board, new PagedResult<BookingRequestRow>(rows, total, p, sz));
    }

    public async Task<BookingRequestRow> CreateAsync(CreateBookingRequestRequest req, CancellationToken ct = default)
    {
        if (_user.TenantId is null) throw AppException.Forbidden();
        if (req.PetParentId.HasValue)
        {
            var parentOk = await _db.PetParents.AnyAsync(p => p.Id == req.PetParentId && p.TenantId == _user.TenantId, ct);
            if (!parentOk) throw AppException.Validation("Invalid pet parent",
                new Dictionary<string, string[]> { ["petParentId"] = new[] { "Pet parent not found in this business." } });
        }

        var r = new BookingRequest
        {
            ParentName = req.ParentName.Trim(),
            Phone = NormalizePhone(req.Phone),
            Email = req.Email,
            PetName = req.PetName,
            RequestedServiceType = req.RequestedServiceType,
            RequestedDate = req.RequestedDate,
            Notes = req.Notes,
            PetParentId = req.PetParentId,
            Status = BookingRequestStatus.Requested,
        };
        _db.BookingRequests.Add(r);
        await _db.SaveChangesAsync(ct);
        return new BookingRequestRow(
            r.Id, r.LegacyRequestId, r.PetParentId, r.ParentName, r.Phone, r.Email,
            r.PetName, r.RequestedServiceType, r.RequestedDate, r.Status,
            r.Notes, r.RejectionReason, r.ConvertedBookingId, r.CreatedAt);
    }

    public async Task<bool> ApproveAsync(Guid id, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return false;
        var r = await _db.BookingRequests.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == _user.TenantId, ct);
        if (r is null) return false;
        if (r.Status != BookingRequestStatus.Requested)
            throw AppException.BusinessRule($"Only Requested entries can be approved (current: {r.Status}).");
        r.Status = BookingRequestStatus.Accepted;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> RejectAsync(Guid id, RejectBookingRequestRequest req, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return false;
        var r = await _db.BookingRequests.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == _user.TenantId, ct);
        if (r is null) return false;
        if (r.Status == BookingRequestStatus.Converted)
            throw AppException.BusinessRule("Cannot reject a converted request.");
        r.Status = BookingRequestStatus.Rejected;
        r.RejectionReason = req.Reason;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> MarkConvertedAsync(Guid id, Guid bookingId, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return false;
        var r = await _db.BookingRequests.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == _user.TenantId, ct);
        if (r is null) return false;
        var bookingOk = await _db.Bookings.AnyAsync(b => b.Id == bookingId && b.TenantId == _user.TenantId, ct);
        if (!bookingOk)
            throw AppException.Validation("Booking not found",
                new Dictionary<string, string[]> { ["bookingId"] = new[] { "Booking does not belong to this business." } });
        r.Status = BookingRequestStatus.Converted;
        r.ConvertedBookingId = bookingId;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private static string NormalizePhone(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return raw;
        var trimmed = raw.Trim();
        var leadingPlus = trimmed.StartsWith("+");
        var digits = new string(trimmed.Where(char.IsDigit).ToArray());
        return leadingPlus ? "+" + digits : digits;
    }
}
