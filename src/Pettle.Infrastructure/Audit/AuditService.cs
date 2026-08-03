using Microsoft.EntityFrameworkCore;
using Pettle.Application.Audit;
using Pettle.Application.Clients;
using Pettle.Application.Common;
using Pettle.Domain.Audit;
using Pettle.Infrastructure.Persistence;

namespace Pettle.Infrastructure.Audit;

public class AuditService : IAuditService
{
    private readonly PettleDbContext _db;
    private readonly ICurrentUser _user;
    public AuditService(PettleDbContext db, ICurrentUser user) { _db = db; _user = user; }

    private IQueryable<AuditEntry> Filtered(AuditLogQuery q, Guid tenantId)
    {
        var rows = _db.AuditEntries.AsNoTracking().Where(a => a.TenantId == tenantId);

        if (q.ActorUserId.HasValue) rows = rows.Where(a => a.ActorUserId == q.ActorUserId);
        if (!string.IsNullOrWhiteSpace(q.Role)) rows = rows.Where(a => a.ActorRoleName == q.Role);
        if (!string.IsNullOrWhiteSpace(q.Module)) rows = rows.Where(a => a.Module == q.Module);
        if (q.Action.HasValue) rows = rows.Where(a => a.Action == q.Action);
        if (!string.IsNullOrWhiteSpace(q.EntityType)) rows = rows.Where(a => a.EntityType == q.EntityType);
        if (!string.IsNullOrWhiteSpace(q.EntityId)) rows = rows.Where(a => a.EntityId == q.EntityId);

        // Dates are the business day in IST, matching every other date filter in the app.
        if (q.From.HasValue)
        {
            var from = BusinessClock.StartOfDayUtc(q.From.Value);
            rows = rows.Where(a => a.CreatedAt >= from);
        }
        if (q.To.HasValue)
        {
            var to = BusinessClock.EndOfDayUtc(q.To.Value);
            rows = rows.Where(a => a.CreatedAt <= to);
        }

        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.Trim();
            rows = rows.Where(a =>
                (a.Summary != null && EF.Functions.ILike(a.Summary, $"%{s}%"))
                || (a.ActorDisplayName != null && EF.Functions.ILike(a.ActorDisplayName, $"%{s}%"))
                || EF.Functions.ILike(a.EntityType, $"%{s}%")
                || a.EntityId == s);
        }
        return rows;
    }

    private static AuditLogItem ToItem(AuditEntry a) => new(
        a.Id, a.CreatedAt, a.Action.ToString(), a.Module ?? "—", a.EntityType, a.EntityId,
        a.Summary ?? $"{a.Action} {a.EntityType}",
        a.ActorUserId, a.ActorDisplayName ?? "System", a.ActorRoleName ?? "—",
        a.ChangedColumns, a.IpAddress);

    public async Task<PagedResult<AuditLogItem>> ListAsync(AuditLogQuery q, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return new PagedResult<AuditLogItem>(Array.Empty<AuditLogItem>(), 0, q.Page, q.PageSize);
        var rows = Filtered(q, _user.TenantId.Value);
        var total = await rows.CountAsync(ct);
        var page = Math.Max(1, q.Page);
        var size = Math.Clamp(q.PageSize, 1, 200);
        var items = await rows
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * size).Take(size)
            .ToListAsync(ct);
        return new PagedResult<AuditLogItem>(items.Select(ToItem).ToList(), total, page, size);
    }

    public async Task<IReadOnlyList<AuditLogItem>> ExportAsync(AuditLogQuery q, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return Array.Empty<AuditLogItem>();
        // Capped so a year-wide export can't try to stream the entire table into one CSV.
        var rows = await Filtered(q, _user.TenantId.Value)
            .OrderByDescending(a => a.CreatedAt).Take(10_000).ToListAsync(ct);
        return rows.Select(ToItem).ToList();
    }

    public async Task<AuditLogDetail?> GetAsync(Guid id, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return null;
        var a = await _db.AuditEntries.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == _user.TenantId, ct);
        return a is null ? null : new AuditLogDetail(ToItem(a), a.BeforeJson, a.AfterJson, a.UserAgent);
    }

    public async Task<AuditFilterOptions> FilterOptionsAsync(CancellationToken ct = default)
    {
        if (_user.TenantId is null)
            return new AuditFilterOptions(Array.Empty<AuditActorOption>(), Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>());
        var tid = _user.TenantId.Value;

        var actors = await _db.AuditEntries.AsNoTracking()
            .Where(a => a.TenantId == tid && a.ActorUserId != null)
            .GroupBy(a => new { a.ActorUserId, a.ActorDisplayName })
            .Select(g => new { g.Key.ActorUserId, g.Key.ActorDisplayName, Last = g.Max(x => x.CreatedAt) })
            .OrderByDescending(x => x.Last)
            .Take(200)
            .ToListAsync(ct);

        var roles = await _db.AuditEntries.AsNoTracking()
            .Where(a => a.TenantId == tid && a.ActorRoleName != null)
            .Select(a => a.ActorRoleName!).Distinct().OrderBy(r => r).ToListAsync(ct);

        var modules = await _db.AuditEntries.AsNoTracking()
            .Where(a => a.TenantId == tid && a.Module != null)
            .Select(a => a.Module!).Distinct().OrderBy(m => m).ToListAsync(ct);

        var actions = await _db.AuditEntries.AsNoTracking()
            .Where(a => a.TenantId == tid)
            .Select(a => a.Action).Distinct().ToListAsync(ct);

        return new AuditFilterOptions(
            actors.Select(a => new AuditActorOption(a.ActorUserId!.Value, a.ActorDisplayName ?? "Unknown", null)).ToList(),
            roles, modules,
            actions.Select(a => a.ToString()).OrderBy(a => a).ToList());
    }

    public async Task RecordAsync(AuditAction action, string module, string entityType, string entityId, string summary,
        Guid? actorUserId = null, string? actorName = null, string? actorRole = null,
        Guid? tenantId = null, CancellationToken ct = default)
    {
        // Sign-in is logged before the request has an authenticated principal, so the caller has
        // to hand over who it was; everything else falls back to the current session.
        var tid = tenantId ?? _user.TenantId;
        if (tid is null) return;

        _db.AuditEntries.Add(new AuditEntry
        {
            TenantId = tid.Value,
            BranchId = _user.BranchId,
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            Module = module,
            Summary = summary,
            ActorUserId = actorUserId ?? _user.UserId,
            ActorDisplayName = actorName ?? _user.DisplayName ?? _user.Email,
            ActorRoleName = actorRole ?? _user.RoleName,
            IpAddress = _user.IpAddress,
            UserAgent = _user.UserAgent,
            CreatedById = actorUserId ?? _user.UserId,
        });
        await _db.SaveChangesAsync(ct);
    }
}
