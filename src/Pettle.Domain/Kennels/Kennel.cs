using Pettle.Domain.Common;

namespace Pettle.Domain.Kennels;

public class KennelGroup : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Color { get; set; }
    public int SortOrder { get; set; }
    public ICollection<Kennel> Kennels { get; set; } = new List<Kennel>();
}

public class Kennel : SoftDeletableTenantEntity
{
    public string Name { get; set; } = string.Empty;
    public string? KennelType { get; set; }
    public string? SizeClass { get; set; }
    public int Capacity { get; set; } = 1;
    public decimal? PricePerNight { get; set; }
    public string? AllowedSpecies { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid? GroupId { get; set; }
    public KennelGroup? Group { get; set; }
    public int SortOrder { get; set; }
}

public class KennelBlocking : TenantEntity
{
    public Guid KennelId { get; set; }
    public Kennel? Kennel { get; set; }
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
    public KennelBlockReason Reason { get; set; }
    public string? Notes { get; set; }
}

public enum KennelBlockReason { Cleaning = 0, Maintenance = 1, Reserved = 2, Other = 99 }
