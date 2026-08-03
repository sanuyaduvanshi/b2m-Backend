using Pettle.Domain.Common;

namespace Pettle.Domain.Audit;

public class AuditEntry : TenantEntity
{
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public AuditAction Action { get; set; }
    public string? ChangedColumns { get; set; }
    public string? BeforeJson { get; set; }
    public string? AfterJson { get; set; }
    public Guid? ActorUserId { get; set; }
    public string? ActorDisplayName { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }

    /// <summary>Role the actor was acting as at the time. Stored rather than resolved later,
    /// because someone's roles change — an entry has to say which hat they were wearing then.</summary>
    public string? ActorRoleName { get; set; }
    /// <summary>Which part of the product this belongs to (Bookings, Invoices, Access…), so the
    /// log filters the way staff think about it rather than by table name.</summary>
    public string? Module { get; set; }
    /// <summary>One plain-English line describing what happened, written when the entry is
    /// recorded — so the list reads without anyone having to diff two JSON blobs.</summary>
    public string? Summary { get; set; }
}

public enum AuditAction { Create = 0, Update = 1, Delete = 2, Login = 3, Logout = 4, Approve = 5, Refund = 6, Export = 7 }
