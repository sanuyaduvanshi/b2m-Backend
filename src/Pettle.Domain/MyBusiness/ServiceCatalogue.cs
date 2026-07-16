using Pettle.Domain.Common;

namespace Pettle.Domain.MyBusiness;

public class ServiceCategory : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}

public class ServiceItem : SoftDeletableTenantEntity
{
    public Guid? CategoryId { get; set; }
    public ServiceCategory? Category { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Vertical { get; set; } = "Boarding";
    public decimal BasePrice { get; set; }
    public int? DurationMinutes { get; set; }
    public decimal? TaxPercent { get; set; }
    public Guid? TaxId { get; set; }
    public Tax? Tax { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<ServiceVariant> Variants { get; set; } = new List<ServiceVariant>();
    public ICollection<ServiceAddOn> AddOns { get; set; } = new List<ServiceAddOn>();
    public ICollection<ServiceDaySlab> DaySlabs { get; set; } = new List<ServiceDaySlab>();
}

public class ServiceVariant : TenantEntity
{
    public Guid ServiceItemId { get; set; }
    public ServiceItem? ServiceItem { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string? SizeClass { get; set; }
    public string? Notes { get; set; }
}

/// <summary>A per-day rate that applies to a whole boarding stay when its length falls within
/// [MinDays, MaxDays] (MaxDays null = unbounded, i.e. this slab and up). Lets a business charge
/// a different nightly rate for longer stays instead of a flat BasePrice x nights. Optional — a
/// service with no slabs keeps the plain BasePrice x nights behavior.</summary>
public class ServiceDaySlab : TenantEntity
{
    public Guid ServiceItemId { get; set; }
    public ServiceItem? ServiceItem { get; set; }
    public int MinDays { get; set; }
    public int? MaxDays { get; set; }
    public decimal PricePerDay { get; set; }
}

public class ServiceAddOn : TenantEntity
{
    public Guid ServiceItemId { get; set; }
    public ServiceItem? ServiceItem { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>Standalone, tenant-level add-on services (e.g. nail clipping, teeth cleaning) usable as extra bill line items.</summary>
public class AddOnService : SoftDeletableTenantEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public decimal? TaxPercent { get; set; }
    public bool IsActive { get; set; } = true;
}

public class Staff : SoftDeletableTenantEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? RoleLabel { get; set; }
    public string? Vertical { get; set; }
    public Guid? UserId { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<StaffShift> Shifts { get; set; } = new List<StaffShift>();
}

public class StaffShift : TenantEntity
{
    public Guid StaffId { get; set; }
    public Staff? Staff { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
}

public class Tax : SoftDeletableTenantEntity
{
    public string Name { get; set; } = string.Empty;
    public TaxKind Kind { get; set; } = TaxKind.GST;
    public decimal Percent { get; set; }
    public bool IsInclusive { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public bool IsActive { get; set; } = true;
}

public enum TaxKind { GST = 0, IGST = 1, CGST = 2, SGST = 3, VAT = 4, SalesTax = 5, Other = 99 }
