using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pettle.Application.Subscriptions;
using Pettle.Domain.ClientEnquiries;
using Pettle.Infrastructure.Persistence;

namespace Pettle.Api.Controllers;

[ApiController]
[Route("api/public")]
public class PublicController : ControllerBase
{
    private readonly PettleDbContext _db;
    private readonly ISubscriptionService _subs;
    public PublicController(PettleDbContext db, ISubscriptionService subs) { _db = db; _subs = subs; }

    public record PublicEnquiryRequest(
        string ParentName,
        string Phone,
        string? Email,
        string? PetName,
        string? ServiceInterest,
        string? Message
    );

    [HttpPost("enquiry")]
    public async Task<IActionResult> SubmitEnquiry([FromBody] PublicEnquiryRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.ParentName) || string.IsNullOrWhiteSpace(req.Phone))
            return BadRequest(new { message = "Name and phone are required." });

        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.IsActive, ct);
        if (tenant is null) return StatusCode(503, new { message = "Service unavailable." });

        var msg = req.Message?.Trim();
        if (!string.IsNullOrWhiteSpace(req.ServiceInterest))
            msg = string.IsNullOrWhiteSpace(msg)
                ? $"Service interest: {req.ServiceInterest}"
                : $"Service interest: {req.ServiceInterest}\n{msg}";

        _db.ClientEnquiries.Add(new ClientEnquiry
        {
            TenantId = tenant.Id,
            Source = EnquirySource.Website,
            Status = EnquiryStatus.Pending,
            ParentName = req.ParentName.Trim(),
            Phone = req.Phone.Trim(),
            Email = req.Email?.Trim(),
            PetName = req.PetName?.Trim(),
            Message = msg,
        });
        await _db.SaveChangesAsync(ct);
        return Ok(new { message = "Thank you! We'll get back to you within 24 hours." });
    }

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

    /// <summary>
    /// Public, unauthenticated invoice view for an issued subscription — the link texted to a
    /// customer via WhatsApp when their subscription is assigned, opened straight from their
    /// phone with no staff login. Access control is the unguessable GUID id alone.
    /// </summary>
    [HttpGet("subscriptions/{id:guid}/invoice")]
    public async Task<IActionResult> SubscriptionInvoice(Guid id, CancellationToken ct)
    {
        var r = await _subs.GetPublicInvoiceAsync(id, ct);
        return r is null ? NotFound() : Ok(r);
    }

    [HttpGet("subscriptions/{id:guid}/invoice/pdf")]
    public async Task<IActionResult> SubscriptionInvoicePdf(Guid id, CancellationToken ct)
    {
        var bytes = await _subs.GeneratePublicInvoicePdfAsync(id, ct);
        return bytes is null ? NotFound() : File(bytes, "application/pdf", $"Subscription-Invoice-{id}.pdf");
    }
}
