using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Pettle.Application.Auth;
using Pettle.Infrastructure.Identity;
using Pettle.Infrastructure.Persistence;

namespace Pettle.Infrastructure.Auth;

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly PettleDbContext _db;
    private readonly JwtOptions _jwt;

    public AuthService(UserManager<ApplicationUser> userManager, PettleDbContext db, IOptions<JwtOptions> jwt)
    {
        _userManager = userManager;
        _db = db;
        _jwt = jwt.Value;
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

        var defaultContext = contexts.FirstOrDefault(c => c.TenantId == user.DefaultTenantId && c.BranchId == user.DefaultBranchId)
                             ?? contexts.First();

        return await IssueTokenAsync(user, defaultContext, contexts, ct);
    }

    public async Task<AuthResult> RefreshAsync(string refreshToken, CancellationToken ct = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.RefreshToken == refreshToken, ct);
        if (user is null || user.RefreshTokenExpiresAt < DateTimeOffset.UtcNow)
            return new AuthResult(false, null, null, null, null, "Invalid refresh token");

        var contexts = await GetContextsAsync(user.Id, ct);
        var current = contexts.FirstOrDefault(c => c.TenantId == user.DefaultTenantId && c.BranchId == user.DefaultBranchId) ?? contexts.First();
        return await IssueTokenAsync(user, current, contexts, ct);
    }

    public async Task LogoutAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _db.Users.FindAsync(new object?[] { userId }, ct);
        if (user is null) return;
        user.RefreshToken = null;
        user.RefreshTokenExpiresAt = null;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<AuthResult> SwitchContextAsync(Guid userId, Guid tenantId, Guid branchId, CancellationToken ct = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null) return new AuthResult(false, null, null, null, null, "User not found");

        var contexts = await GetContextsAsync(userId, ct);
        var target = contexts.FirstOrDefault(c => c.TenantId == tenantId && c.BranchId == branchId);
        if (target is null) return new AuthResult(false, null, null, null, null, "Context not allowed");

        user.DefaultTenantId = tenantId;
        user.DefaultBranchId = branchId;
        await _db.SaveChangesAsync(ct);

        return await IssueTokenAsync(user, target, contexts, ct);
    }

    private async Task<List<TenantBranchOption>> GetContextsAsync(Guid userId, CancellationToken ct)
    {
        var rows = await (from ub in _db.UserBranches.AsNoTracking()
                          join b in _db.Branches.AsNoTracking() on ub.BranchId equals b.Id
                          join t in _db.Tenants.AsNoTracking() on ub.TenantId equals t.Id
                          join r in _db.AppRoles.AsNoTracking() on ub.RoleId equals r.Id
                          where ub.UserId == userId && t.IsActive && b.IsActive
                          select new TenantBranchOption(t.Id, t.Name, b.Id, b.Name, r.Name))
                         .ToListAsync(ct);
        return rows;
    }

    private async Task<AuthResult> IssueTokenAsync(ApplicationUser user, TenantBranchOption context, List<TenantBranchOption> all, CancellationToken ct)
    {
        var role = await _db.AppRoles.FirstAsync(r => r.Name == context.RoleName && r.TenantId == context.TenantId, ct);
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
        user.RefreshToken = refresh;
        user.RefreshTokenExpiresAt = now.AddDays(_jwt.RefreshTokenDays);
        user.LastLoginAt = now;
        await _db.SaveChangesAsync(ct);

        var session = new UserSession(
            user.Id, user.Email ?? string.Empty, user.DisplayName,
            context.TenantId, context.TenantName, context.BranchId, context.BranchName, context.RoleName,
            all, perms);

        return new AuthResult(true, accessToken, refresh, exp, session, null);
    }
}
