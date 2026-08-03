namespace Pettle.Application.Auth;

public interface IAuthService
{
    Task<AuthResult> LoginAsync(string email, string password, CancellationToken ct = default);
    Task<AuthResult> RefreshAsync(string refreshToken, CancellationToken ct = default);
    /// <summary>Revokes only the session identified by <paramref name="refreshToken"/> — other
    /// devices/tabs the user is signed in on are left untouched.</summary>
    Task LogoutAsync(Guid userId, string? refreshToken, CancellationToken ct = default);
    /// <summary>roleId disambiguates when a user holds more than one role at the same tenant/branch
    /// (a "switch role" rather than a "switch branch"); when null, the primary role there is used.</summary>
    Task<AuthResult> SwitchContextAsync(Guid userId, Guid tenantId, Guid branchId, Guid? roleId = null, CancellationToken ct = default);
}

public record AuthResult(
    bool Success,
    string? AccessToken,
    string? RefreshToken,
    DateTimeOffset? ExpiresAt,
    UserSession? User,
    string? Error
);

public record UserSession(
    Guid UserId,
    string Email,
    string DisplayName,
    Guid TenantId,
    string TenantName,
    Guid BranchId,
    string BranchName,
    string RoleName,
    Guid RoleId,
    IReadOnlyList<TenantBranchOption> AvailableContexts,
    IReadOnlyList<string> Permissions,
    /// <summary>True when this role only sees records the user personally created (Receptionist).
    /// The UI needs it to suppress figures that mix own-scoped money with tenant-wide counts —
    /// "revenue ÷ all clients" is meaningless when the revenue half is only your own.</summary>
    bool RestrictToOwnRecords = false
);

public record TenantBranchOption(Guid TenantId, string TenantName, Guid BranchId, string BranchName, string RoleName, Guid RoleId, bool IsPrimary);

public record LoginRequest(string Email, string Password);
public record RefreshRequest(string RefreshToken);
public record SwitchContextRequest(Guid TenantId, Guid BranchId, Guid? RoleId = null);
