using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Pettle.Application.Common;
using Pettle.Application.Common.Errors;
using Pettle.Application.DailyTasks;
using Pettle.Domain.Bookings;
using Pettle.Domain.DailyTasks;
using Pettle.Infrastructure.Persistence;

namespace Pettle.Infrastructure.DailyTasks;

public class DailyTaskService : IDailyTaskService
{
    private readonly PettleDbContext _db;
    private readonly ICurrentUser _user;

    public DailyTaskService(PettleDbContext db, ICurrentUser user) { _db = db; _user = user; }

    public async Task<DailyTaskBoard> BoardAsync(DateOnly date, string? serviceType, string? status, CancellationToken ct = default)
    {
        if (_user.TenantId is null)
            return new DailyTaskBoard(date, Array.Empty<string>(), Array.Empty<DailyTaskRow>());

        var tid = _user.TenantId.Value;

        // Collect active bookings on the date: any service active that day.
        var services = await _db.BookingServices.AsNoTracking()
            .Include(s => s.Booking).ThenInclude(b => b!.PetParent)
            .Include(s => s.Pet)
            .Where(s => s.TenantId == tid && (
                s.Status == BookingStatus.Upcoming ||
                s.Status == BookingStatus.CheckedIn ||
                s.Status == BookingStatus.Active))
            .ToListAsync(ct);

        // Filter by service-type-specific overlap with date.
        var boardingMeta = await _db.BoardingDetails.AsNoTracking()
            .Where(d => d.TenantId == tid)
            .Select(d => new { d.BookingServiceId, d.CheckInDate, d.CheckOutDate, d.KennelLabel, d.BoardingType, d.CompanionName })
            .ToListAsync(ct);
        var groomingMeta = await _db.GroomingDetails.AsNoTracking().Where(d => d.TenantId == tid).Select(d => new { d.BookingServiceId, d.ServiceDate, d.StaffName }).ToListAsync(ct);
        var vetMeta = await _db.VetDetails.AsNoTracking().Where(d => d.TenantId == tid).Select(d => new { d.BookingServiceId, d.ServiceDate, d.StaffName }).ToListAsync(ct);
        var daycareMeta = await _db.DayCareDetails.AsNoTracking().Where(d => d.TenantId == tid).Select(d => new { d.BookingServiceId, d.ServiceDate, d.StaffName }).ToListAsync(ct);

        var boardingIdx = boardingMeta.GroupBy(x => x.BookingServiceId).ToDictionary(g => g.Key, g => g.First());
        var groomingIdx = groomingMeta.ToDictionary(x => x.BookingServiceId, x => x);
        var vetIdx = vetMeta.ToDictionary(x => x.BookingServiceId, x => x);
        var daycareIdx = daycareMeta.ToDictionary(x => x.BookingServiceId, x => x);

        var activeOnDate = services.Where(s => s.ServiceType switch
        {
            BookingServiceType.Boarding => boardingIdx.TryGetValue(s.Id, out var b)
                && b.CheckInDate <= date && b.CheckOutDate >= date,
            BookingServiceType.Grooming => groomingIdx.TryGetValue(s.Id, out var g) && g.ServiceDate == date,
            BookingServiceType.Vet => vetIdx.TryGetValue(s.Id, out var v) && v.ServiceDate == date,
            BookingServiceType.DayCare => daycareIdx.TryGetValue(s.Id, out var d) && d.ServiceDate == date,
            _ => false,
        }).ToList();

        if (!string.IsNullOrWhiteSpace(serviceType)
            && Enum.TryParse<BookingServiceType>(serviceType, true, out var stEnum))
            activeOnDate = activeOnDate.Where(s => s.ServiceType == stEnum).ToList();

        if (activeOnDate.Count == 0)
            return new DailyTaskBoard(date, DailyTaskColumns.DefaultColumns.Select(c => c.ToString()).ToList(), Array.Empty<DailyTaskRow>());

        // Ensure entries exist for each active booking-service × default-column.
        await EnsureEntriesAsync(activeOnDate, date, ct);

        var serviceIds = activeOnDate.Select(s => s.Id).ToHashSet();
        var entries = await _db.DailyTaskEntries.AsNoTracking()
            .Where(e => e.TenantId == tid && e.Date == date && serviceIds.Contains(e.BookingServiceId))
            .ToListAsync(ct);

        var rows = activeOnDate.Select(s =>
        {
            var byType = entries
                .Where(e => e.BookingServiceId == s.Id)
                .ToDictionary(e => e.TaskType, e => new DailyTaskCell(e.Id, e.Status, e.CompletedAt, e.CompletedByName, e.Notes, e.Label));

            var cells = DailyTaskColumns.DefaultColumns
                .Where(t => byType.ContainsKey(t))
                .ToDictionary(t => t.ToString(), t => byType[t]);

            boardingIdx.TryGetValue(s.Id, out var b);
            return new DailyTaskRow(
                s.Id, s.BookingId,
                s.Booking?.PetParent?.Name ?? "—",
                s.Booking?.PetParent?.Phone ?? "",
                s.Pet?.Name ?? "(deleted pet)",
                s.ServiceType.ToString(),
                b?.KennelLabel,
                b?.BoardingType,
                cells,
                b?.CompanionName
            );
        }).ToList();

        // Status filter (any cell matching)
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<DailyTaskStatus>(status, true, out var st))
            rows = rows.Where(r => r.Cells.Values.Any(c => c.Status == st)).ToList();

        return new DailyTaskBoard(
            date,
            DailyTaskColumns.DefaultColumns.Select(c => c.ToString()).ToList(),
            rows);
    }

    public async Task<bool> UpdateStatusAsync(Guid entryId, UpdateDailyTaskStatusRequest req, CancellationToken ct = default)
    {
        if (_user.TenantId is null) throw AppException.Forbidden();
        var e = await _db.DailyTaskEntries.FirstOrDefaultAsync(x => x.Id == entryId && x.TenantId == _user.TenantId, ct);
        if (e is null) return false;
        e.Status = req.Status;
        e.Notes = req.Notes;
        if (req.Status == DailyTaskStatus.Done || req.Status == DailyTaskStatus.Skipped)
        {
            e.CompletedAt = DateTimeOffset.UtcNow;
            e.CompletedByUserId = _user.UserId;
            e.CompletedByName = _user.DisplayName ?? _user.Email;
        }
        else
        {
            e.CompletedAt = null; e.CompletedByUserId = null; e.CompletedByName = null;
        }
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<string> ExportCsvAsync(DateOnly date, string? serviceType, CancellationToken ct = default)
    {
        var board = await BoardAsync(date, serviceType, null, ct);
        var sb = new StringBuilder();
        sb.Append("Date,Booking,Parent,Phone,Pet,ServiceType,Kennel");
        foreach (var col in board.Columns) sb.Append(',').Append(col).Append("_Status").Append(',').Append(col).Append("_By").Append(',').Append(col).Append("_At").Append(',').Append(col).Append("_Notes");
        sb.AppendLine();

        foreach (var r in board.Rows)
        {
            sb.Append(date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            sb.Append(',').Append(Csv(r.BookingId.ToString()));
            sb.Append(',').Append(Csv(r.ParentName));
            sb.Append(',').Append(Csv(r.Phone));
            sb.Append(',').Append(Csv(r.PetName));
            sb.Append(',').Append(Csv(r.ServiceType));
            sb.Append(',').Append(Csv(r.KennelLabel ?? ""));
            foreach (var col in board.Columns)
            {
                var cell = r.Cells.TryGetValue(col, out var c) ? c : null;
                sb.Append(',').Append(cell?.Status.ToString() ?? "");
                sb.Append(',').Append(Csv(cell?.CompletedByName ?? ""));
                sb.Append(',').Append(cell?.CompletedAt?.ToString("u", CultureInfo.InvariantCulture) ?? "");
                sb.Append(',').Append(Csv(cell?.Notes ?? ""));
            }
            sb.AppendLine();
        }
        return sb.ToString();
    }

    private async Task EnsureEntriesAsync(List<BookingService> services, DateOnly date, CancellationToken ct)
    {
        var ids = services.Select(s => s.Id).ToList();
        var existing = await _db.DailyTaskEntries
            .Where(e => ids.Contains(e.BookingServiceId) && e.Date == date)
            .Select(e => new { e.BookingServiceId, e.TaskType })
            .ToListAsync(ct);
        var existingSet = existing.Select(x => (x.BookingServiceId, x.TaskType)).ToHashSet();

        var toAdd = new List<DailyTaskEntry>();
        foreach (var s in services)
        {
            IEnumerable<DailyTaskType> cols = s.ServiceType switch
            {
                BookingServiceType.Boarding => DailyTaskColumns.ForBoarding(),
                BookingServiceType.Grooming => DailyTaskColumns.ForGrooming(),
                BookingServiceType.Vet => DailyTaskColumns.ForVet(),
                BookingServiceType.DayCare => DailyTaskColumns.ForDayCare(),
                _ => Array.Empty<DailyTaskType>(),
            };
            foreach (var col in cols)
            {
                if (existingSet.Contains((s.Id, col))) continue;
                toAdd.Add(new DailyTaskEntry
                {
                    BookingServiceId = s.Id,
                    Date = date,
                    TaskType = col,
                    Status = DailyTaskStatus.Pending,
                });
            }
        }
        if (toAdd.Count > 0)
        {
            _db.DailyTaskEntries.AddRange(toAdd);
            await _db.SaveChangesAsync(ct);
        }
    }

    private static string Csv(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        if (s.Contains(',') || s.Contains('"') || s.Contains('\n'))
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        return s;
    }
}
