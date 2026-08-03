namespace Pettle.Application.Common;

public interface ICurrentUser
{
    Guid? UserId { get; }
    string? Email { get; }
    string? DisplayName { get; }
    Guid? TenantId { get; }
    Guid? BranchId { get; }
    IReadOnlySet<string> Permissions { get; }
    bool IsAuthenticated { get; }
    /// <summary>True when the current session's role scopes data visibility to only records
    /// this user personally created (see Role.RestrictToOwnRecords).</summary>
    bool RestrictToOwnRecords { get; }
    /// <summary>Role the session is currently acting as. Stamped onto audit entries so the log
    /// records which hat someone was wearing, not whichever roles they happen to hold today.</summary>
    string? RoleName { get; }
    string? IpAddress { get; }
    string? UserAgent { get; }
    bool Has(string permission);
}
