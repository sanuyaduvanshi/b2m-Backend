using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Pettle.Application.Audit;
using Pettle.Application.Auth;
using Pettle.Domain.Audit;
using Pettle.Domain.Identity;
using Pettle.Infrastructure.Identity;
using Pettle.Infrastructure.Persistence;

namespace Pettle.Infrastructure.Auth;

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly PettleDbContext _db;
    private readonly JwtOptions _jwt;
    private readonly IAuditService _audit;

    public AuthService(UserManager<ApplicationUser> userManager, PettleDbContext db, IOptions<JwtOptions> jwt, IAuditService audit)
    {
        _userManager = userManager;
        _db = db;
        _jwt = jwt.Value;
        _audit = audit;
    }

    public async Task<AuthResult> LoginAsync(string email, string password, CancellationToken ct = default)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user is null || !user.IsActive)
            return new AuthResult(false, null, null, null, null, "Invalid credentials");

        if (!await _userManager.CheckPasswordAsync(user, password))
            return new AuthResult(false, null, null, null, null, "Invalid credentials");

        var contexts = await GetContextsAsync(user.Id, ct);
        if (contexts.Count == 0)
            return new AuthResult(false, null, null, null, null, "No tenant access");

        var defaultContext = PickDefault(contexts, user.DefaultTenantId, user.DefaultBranchId);

        // Logged explicitly: a sign-in writes no business row for the save interceptor to notice,
        // and there's no authenticated principal yet, so the actor has to be passed in.
        await _audit.RecordAsync(AuditAction.Login, "Access", "User", user.Id.ToString(),
            $"Signed in as {defaultContext.RoleName}",
            actorUserId: user.Id, actorName: user.DisplayName, actorRole: defaultContext.RoleName,
            tenantId: defaultContext.TenantId, ct: ct);

        return await IssueTokenAsync(user, defaultContext, contexts, ct);
    }

    public async Task<AuthResult> RefreshAsync(string refreshToken, CancellationToken ct = default)
    {
        var entry = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.Token == refreshToken, ct);
        if (entry is null || entry.RevokedAt is not null || entry.ExpiresAt < DateTimeOffset.UtcNow)
            return new AuthResult(false, null, null, null, null, "Invalid refresh token");

        var user = await _db.Users.FindAsync(new object?[] { entry.UserId }, ct);
        if (user is null) return new AuthResult(false, null, null, null, null, "Invalid refresh token");

        // Rotate: revoke this one row only — sibling sessions on other devices/tabs keep theirs.
        entry.RevokedAt = DateTimeOffset.UtcNow;

        var contexts = await GetContextsAsync(user.Id, ct);
        var current = PickDefault(contexts, user.DefaultTenantId, user.DefaultBranchId);
        return await IssueTokenAsync(user, current, contexts, ct);
    }

    public async Task LogoutAsync(Guid userId, string? refreshToken, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(refreshToken)) return;
        // Only revoke the session that's actually logging out — other devices/tabs stay signed in.
        var entry = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.UserId == userId && t.Token == refreshToken, ct);
        if (entry is null) return;
        entry.RevokedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        var user = await _db.Users.FindAsync(new object?[] { userId }, ct);
        await _audit.RecordAsync(AuditAction.Logout, "Access", "User", userId.ToString(), "Signed out",
            actorUserId: userId, actorName: user?.DisplayName, ct: ct);
    }

    public async Task<AuthResult> SwitchContextAsync(Guid userId, Guid tenantId, Guid branchId, Guid? roleId = null, CancellationToken ct = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null) return new AuthResult(false, null, null, null, null, "User not found");

        var contexts = await GetContextsAsync(userId, ct);
        var candidates = contexts.Where(c => c.TenantId == tenantId && c.BranchId == branchId).ToList();
        var target = roleId.HasValue
            ? candidates.FirstOrDefault(c => c.RoleId == roleId.Value)
            : candidates.FirstOrDefault(c => c.IsPrimary) ?? candidates.FirstOrDefault();
        if (target is null) return new AuthResult(false, null, null, null, null, "Context not allowed");

        user.DefaultTenantId = tenantId;
        user.DefaultBranchId = branchId;
        await _db.SaveChangesAsync(ct);

        // Worth its own entry: everything this session does afterwards is attributed to the new
        // role, and the log would otherwise show the switch as an unexplained change of hat.
        await _audit.RecordAsync(AuditAction.Login, "Access", "User", user.Id.ToString(),
            $"Switched role to {target.RoleName} ({target.BranchName})",
            actorUserId: user.Id, actorName: user.DisplayName, actorRole: target.RoleName,
            tenantId: target.TenantId, ct: ct);

        return await IssueTokenAsync(user, target, contexts, ct);
    }

    /// <summary>Picks which role is active by default at login/refresh: the row matching the user's
    /// stored default tenant/branch, preferring the one flagged IsPrimary when more than one role
    /// exists there (a user can hold several roles at the same branch since switching), falling back
    /// to the first context at all if the stored default no longer resolves to anything.</summary>
    private static TenantBranchOption PickDefault(List<TenantBranchOption> contexts, Guid? defaultTenantId, Guid? defaultBranchId)
    {
        var candidates = contexts.Where(c => c.TenantId == defaultTenantId && c.BranchId == defaultBranchId).ToList();
        return candidates.FirstOrDefault(c => c.IsPrimary) ?? candidates.FirstOrDefault() ?? contexts.First();
    }

    private async Task<List<TenantBranchOption>> GetContextsAsync(Guid userId, CancellationToken ct)
    {
        var rows = await (from ub in _db.UserBranches.AsNoTracking()
                          join b in _db.Branches.AsNoTracking() on ub.BranchId equals b.Id
                          join t in _db.Tenants.AsNoTracking() on ub.TenantId equals t.Id
                          join r in _db.AppRoles.AsNoTracking() on ub.RoleId equals r.Id
                          where ub.UserId == userId && t.IsActive && b.IsActive
                          select new TenantBranchOption(t.Id, t.Name, b.Id, b.Name, r.Name, r.Id, ub.IsPrimary))
                         .ToListAsync(ct);
        return rows;
    }

    private async Task<AuthResult> IssueTokenAsync(ApplicationUser user, TenantBranchOption context, List<TenantBranchOption> all, CancellationToken ct)
    {
        var role = await _db.AppRoles.FirstAsync(r => r.Id == context.RoleId, ct);
        var perms = await _db.RolePermissions.Where(p => p.RoleId == role.Id).Select(p => p.PermissionKey).ToListAsync(ct);

        var now = DateTimeOffset.UtcNow;
        var exp = now.AddMinutes(_jwt.AccessTokenMinutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new("display_name", user.DisplayName),
            new("tenant_id", context.TenantId.ToString()),
            new("branch_id", context.BranchId.ToString()),
            new("role", context.RoleName),
            new("restrict_own", role.RestrictToOwnRecords.ToString())
        };
        foreach (var p in perms) claims.Add(new Claim("perm", p));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(_jwt.Issuer, _jwt.Audience, claims, now.UtcDateTime, exp.UtcDateTime, creds);
        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);

        var refresh = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        _db.RefreshTokens.Add(new RefreshTokenEntry
        {
            UserId = user.Id,
            Token = refresh,
            ExpiresAt = now.AddDays(_jwt.RefreshTokenDays),
        });
        user.LastLoginAt = now;
        await _db.SaveChangesAsync(ct);

        var session = new UserSession(
            user.Id, user.Email ?? string.Empty, user.DisplayName,
            context.TenantId, context.TenantName, context.BranchId, context.BranchName, context.RoleName, context.RoleId,
            all, perms, role.RestrictToOwnRecords);

        return new AuthResult(true, accessToken, refresh, exp, session, null);
    }
}
