using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pettle.Api.Authorization;
using Pettle.Domain.Identity;

namespace Pettle.Api.Controllers;

[ApiController]
[Route("api/files")]
[Authorize]
public class FilesController : ControllerBase
{
    private static readonly HashSet<string> _allowed = new(StringComparer.OrdinalIgnoreCase)
        { ".jpg", ".jpeg", ".png", ".webp", ".gif" };

    [HttpPost("sku-image")]
    [HasPermission(Modules.Inventory, Actions.Edit)]
    [RequestSizeLimit(5 * 1024 * 1024)] // 5 MB
    public async Task<IActionResult> UploadSkuImage(IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "validation_error", message = "No file provided." });

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!_allowed.Contains(ext))
            return BadRequest(new { error = "validation_error", message = "Only JPG, PNG, WEBP, or GIF images are allowed." });

        var dir = Path.Combine("/app/uploads", "sku-images");
        Directory.CreateDirectory(dir);

        var filename = $"{Guid.NewGuid()}{ext}";
        var path = Path.Combine(dir, filename);

        await using var stream = System.IO.File.Create(path);
        await file.CopyToAsync(stream, ct);

        return Ok(new { url = $"/uploads/sku-images/{filename}" });
    }
}
