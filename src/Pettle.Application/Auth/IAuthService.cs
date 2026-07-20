namespace Pettle.Application.Auth;

public interface IAuthService
{
    Task<AuthResult> LoginAsync(string email, string password, CancellationToken ct = default);
    Task<AuthResult> RefreshAsync(string refreshToken, CancellationToken ct = default);
    Task LogoutAsync(Guid userId, CancellationToken ct = default);
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
    IReadOnlyList<string> Permissions
);

public record TenantBranchOption(Guid TenantId, string TenantName, Guid BranchId, string BranchName, string RoleName, Guid RoleId, bool IsPrimary);

public record LoginRequest(string Email, string Password);
public record RefreshRequest(string RefreshToken);
public record SwitchContextRequest(Guid TenantId, Guid BranchId, Guid? RoleId = null);
