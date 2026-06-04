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
}

public enum AuditAction { Create = 0, Update = 1, Delete = 2, Login = 3, Logout = 4, Approve = 5, Refund = 6, Export = 7 }
