using Pettle.Domain.Clients;
using Pettle.Domain.Common;

namespace Pettle.Domain.ClientEnquiries;

public class ClientEnquiry : TenantEntity
{
    public string? LegacyEnquiryId { get; set; }
    public EnquirySource Source { get; set; } = EnquirySource.Manual;
    public EnquiryStatus Status { get; set; } = EnquiryStatus.Pending;

    public string ParentName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? PetName { get; set; }
    public string? Message { get; set; }

    public Guid? AssignedToUserId { get; set; }
    public string? AssignedToName { get; set; }

    public DateTimeOffset? ResolvedAt { get; set; }
    public Guid? ResolvedByUserId { get; set; }
    public string? ResolvedByName { get; set; }

    public string? RejectionReason { get; set; }

    public Guid? ConvertedClientId { get; set; }
    public PetParent? ConvertedClient { get; set; }
}

public enum EnquirySource
{
    Manual = 0,
    Website = 1,
    MetaAds = 2,
    GoogleAds = 3,
    WalkIn = 4,
    Phone = 5,
    External = 99
}

public enum EnquiryStatus
{
    Pending = 0,
    Completed = 1,
    Rejected = 2
}
