using Pettle.Application.Clients;
using Pettle.Domain.Audit;

namespace Pettle.Application.Audit;

public record AuditLogItem(
    Guid Id,
    DateTimeOffset At,
    string Action,
    string Module,
    string EntityType,
    string EntityId,
    string Summary,
    Guid? ActorUserId,
    string ActorName,
    string ActorRole,
    string? ChangedColumns,
    string? IpAddress);

/// <summary>A single logged change with the before/after payload attached — kept off the list
/// response because the JSON blobs are far larger than everything else on a row.</summary>
public record AuditLogDetail(AuditLogItem Entry, string? BeforeJson, string? AfterJson, string? UserAgent);

public record AuditLogQuery(
    string? Search = null,
    Guid? ActorUserId = null,
    string? Role = null,
    string? Module = null,
    AuditAction? Action = null,
    string? EntityType = null,
    string? EntityId = null,
    DateOnly? From = null,
    DateOnly? To = null,
    int Page = 1,
    int PageSize = 50);

/// <summary>The distinct values actually present in the log, so the filter dropdowns only ever
/// offer choices that will match something.</summary>
public record AuditFilterOptions(
    IReadOnlyList<AuditActorOption> Actors,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Modules,
    IReadOnlyList<string> Actions);

public record AuditActorOption(Guid Id, string Name, string? Role);

public interface IAuditService
{
    Task<PagedResult<AuditLogItem>> ListAsync(AuditLogQuery query, CancellationToken ct = default);
    Task<AuditLogDetail?> GetAsync(Guid id, CancellationToken ct = default);
    Task<AuditFilterOptions> FilterOptionsAsync(CancellationToken ct = default);
    /// <summary>Everything matching the filters, unpaginated, for the CSV download.</summary>
    Task<IReadOnlyList<AuditLogItem>> ExportAsync(AuditLogQuery query, CancellationToken ct = default);
    /// <summary>Records something that isn't a database write — a sign-in, a sign-out, an export.
    /// CRUD is picked up automatically by the save interceptor.</summary>
    Task RecordAsync(AuditAction action, string module, string entityType, string entityId, string summary,
        Guid? actorUserId = null, string? actorName = null, string? actorRole = null,
        Guid? tenantId = null, CancellationToken ct = default);
}
