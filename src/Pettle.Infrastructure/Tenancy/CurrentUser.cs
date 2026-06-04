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
    public bool Has(string permission) => Permissions.Contains(permission);

    private Guid? GuidClaim(string type)
    {
        var v = User?.FindFirstValue(type);
        return Guid.TryParse(v, out var g) ? g : null;
    }
}
