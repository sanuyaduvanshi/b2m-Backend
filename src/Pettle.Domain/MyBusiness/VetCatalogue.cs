using Pettle.Domain.Common;

namespace Pettle.Domain.MyBusiness;

/// <summary>
/// Tenant-level veterinary masters used during vet visits:
/// prescription templates, tests, treatments, and certificate templates.
/// </summary>
public class VetCatalogueItem : SoftDeletableTenantEntity
{
    public VetItemKind Kind { get; set; } = VetItemKind.Test;
    public string Name { get; set; } = string.Empty;
    /// <summary>Free-text body for templates (prescription / certificate); null for tests/treatments.</summary>
    public string? Content { get; set; }
    /// <summary>Charge for tests/treatments; null for templates.</summary>
    public decimal? Price { get; set; }
    public bool IsActive { get; set; } = true;
}

public enum VetItemKind
{
    Prescription = 0,
    Test = 1,
    Treatment = 2,
    Certificate = 3
}
