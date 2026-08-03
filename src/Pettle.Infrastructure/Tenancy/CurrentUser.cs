using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Pettle.Application.Common;

namespace Pettle.Infrastructure.Tenancy;

public class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _accessor;
    private readonly Lazy<IReadOnlySet<string>> _permissions;

    public CurrentUser(IHttpContextAccessor accessor)
    {
        _accessor = accessor;
        _permissions = new Lazy<IReadOnlySet<string>>(() =>
            new HashSet<string>(User?.Claims.Where(c => c.Type == "perm").Select(c => c.Value) ?? Array.Empty<string>())
        );
    }

    private ClaimsPrincipal? User => _accessor.HttpContext?.User;

    public Guid? UserId => GuidClaim(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub) ?? GuidClaim(ClaimTypes.NameIdentifier);
    public string? Email => User?.FindFirstValue(ClaimTypes.Email) ?? User?.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Email);
    public string? DisplayName => User?.FindFirstValue("display_name");
    public Guid? TenantId => GuidClaim("tenant_id");
    public Guid? BranchId => GuidClaim("branch_id");
    public IReadOnlySet<string> Permissions => _permissions.Value;
    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;
    public bool RestrictToOwnRecords => bool.TryParse(User?.FindFirstValue("restrict_own"), out var v) && v;
    // The JWT carries "role", but ASP.NET's token handler remaps that to ClaimTypes.Role, so the
    // raw name only survives on tokens read before mapping — check both.
    public string? RoleName => User?.FindFirstValue("role") ?? User?.FindFirstValue(ClaimTypes.Role);
    // Behind nginx the socket address is the proxy, so prefer the forwarded client address.
    public string? IpAddress =>
        _accessor.HttpContext?.Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim()
        ?? _accessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
    public string? UserAgent => _accessor.HttpContext?.Request.Headers.UserAgent.ToString() is { Length: > 0 } ua ? ua : null;
    public bool Has(string permission) => Permissions.Contains(permission);

    private Guid? GuidClaim(string type)
    {
        var v = User?.FindFirstValue(type);
        return Guid.TryParse(v, out var g) ? g : null;
    }
}
