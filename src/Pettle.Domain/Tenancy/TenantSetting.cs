using Pettle.Domain.Common;

namespace Pettle.Domain.Tenancy;

/// <summary>
/// Per-tenant key→JSON configuration store. Each row is a settings group (e.g. "parent-app", "invoice", "printer").
/// Value is stored as a JSON string; consumers deserialize to their typed shape.
/// </summary>
public class TenantSetting : Entity
{
    public Guid TenantId { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = "{}";
}
