using Pettle.Domain.Common;

namespace Pettle.Domain.Tenancy;

public class Tenant : Entity
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public string? PrimaryColor { get; set; }
    public string? SecondaryColor { get; set; }
    public string? AccentColor { get; set; }
    public string? Currency { get; set; } = "INR";
    public string? Locale { get; set; } = "en-IN";
    public string? TimeZone { get; set; } = "Asia/Kolkata";
    public bool IsActive { get; set; } = true;
    public int IdleSessionMinutes { get; set; } = 60;

    public ICollection<Branch> Branches { get; set; } = new List<Branch>();
}

public class Branch : Entity
{
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Country { get; set; }
    public string? PostalCode { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public bool IsActive { get; set; } = true;
}
