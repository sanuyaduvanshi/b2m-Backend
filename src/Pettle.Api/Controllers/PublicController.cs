using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pettle.Infrastructure.Persistence;

namespace Pettle.Api.Controllers;

[ApiController]
[Route("api/public")]
public class PublicController : ControllerBase
{
    private readonly PettleDbContext _db;
    public PublicController(PettleDbContext db) => _db = db;

    /// <summary>
    /// Returns the tenant brand to display on unauthenticated screens (login, password reset).
    /// Resolution order:
    ///   1. ?slug=&lt;tenant-slug&gt; query param
    ///   2. First active tenant (current single-tenant deployment)
    /// </summary>
    [HttpGet("brand")]
    public async Task<IActionResult> Brand([FromQuery] string? slug, CancellationToken ct)
    {
        var q = _db.Tenants.AsNoTracking().Where(t => t.IsActive);
        if (!string.IsNullOrWhiteSpace(slug)) q = q.Where(t => t.Slug == slug);

        var t = await q.OrderBy(x => x.Name)
            .Select(x => new
            {
                name = x.Name,
                slug = x.Slug,
                logoUrl = x.LogoUrl,
                primaryColor = x.PrimaryColor,
                secondaryColor = x.SecondaryColor,
                accentColor = x.AccentColor,
            })
            .FirstOrDefaultAsync(ct);

        if (t is null) return Ok(new { name = "Pettle", slug = (string?)null, logoUrl = (string?)null, primaryColor = (string?)null, secondaryColor = (string?)null, accentColor = (string?)null });
        return Ok(t);
    }
}
