namespace Pettle.Application.Common;

public interface ICurrentUser
{
    Guid? UserId { get; }
    string? Email { get; }
    string? DisplayName { get; }
    Guid? TenantId { get; }
    Guid? BranchId { get; }
    IReadOnlySet<string> Permissions { get; }
    bool IsAuthenticated { get; }
    bool Has(string permission);
}
