using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pettle.Application.Auth;
using Pettle.Application.Common;

namespace Pettle.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;
    private readonly ICurrentUser _user;

    public AuthController(IAuthService auth, ICurrentUser user)
    {
        _auth = auth;
        _user = user;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req, CancellationToken ct)
    {
        var r = await _auth.LoginAsync(req.Email, req.Password, ct);
        return r.Success ? Ok(r) : Unauthorized(new { error = r.Error, message = r.Error });
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest req, CancellationToken ct)
    {
        var r = await _auth.RefreshAsync(req.RefreshToken, ct);
        return r.Success ? Ok(r) : Unauthorized(new { error = r.Error, message = r.Error });
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] RefreshRequest? req, CancellationToken ct)
    {
        if (_user.UserId is null) return Unauthorized();
        await _auth.LogoutAsync(_user.UserId.Value, req?.RefreshToken, ct);
        return NoContent();
    }

    [Authorize]
    [HttpPost("switch-context")]
    public async Task<IActionResult> SwitchContext([FromBody] SwitchContextRequest req, CancellationToken ct)
    {
        if (_user.UserId is null) return Unauthorized();
        var r = await _auth.SwitchContextAsync(_user.UserId.Value, req.TenantId, req.BranchId, req.RoleId, ct);
        return r.Success ? Ok(r) : BadRequest(new { error = r.Error });
    }

    [Authorize]
    [HttpGet("me")]
    public IActionResult Me() => Ok(new
    {
        userId = _user.UserId,
        email = _user.Email,
        displayName = _user.DisplayName,
        tenantId = _user.TenantId,
        branchId = _user.BranchId,
        permissions = _user.Permissions
    });
}
