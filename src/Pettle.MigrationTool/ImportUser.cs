using Pettle.Application.Common;

namespace Pettle.MigrationTool;

/// <summary>
/// Stand-in for ICurrentUser during data migration — supplies the tenant context that
/// PettleDbContext's SaveChangesAsync interceptor uses to stamp TenantId / CreatedBy.
/// Permissions are empty (migrations bypass [HasPermission] since we don't go through MVC).
/// </summary>
public class ImportUser : ICurrentUser
{
    public Guid? UserId { get; set; }
    public string? Email { get; set; } = "migration@pettle.local";
    public string? DisplayName { get; set; } = "Migration Tool";
    public Guid? TenantId { get; set; }
    public Guid? BranchId { get; set; }
    public IReadOnlySet<string> Permissions { get; } = new HashSet<string>();
    public bool IsAuthenticated => true;
    public bool Has(string permission) => true; // bypass for the importer
}
