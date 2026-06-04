using Pettle.Domain.Common;

namespace Pettle.Domain.MyBusiness;

/// <summary>
/// Intake / consent form configured per tenant. Each form has a type and a flexible field list (JSON).
/// Linked to bookings at fill-time (filled instances stored separately later — out of scope for now).
/// </summary>
public class IntakeForm : SoftDeletableTenantEntity
{
    public string Name { get; set; } = string.Empty;
    public IntakeFormType Type { get; set; } = IntakeFormType.Boarding;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    /// <summary>JSON array of field definitions: [{ id, label, type, required, options? }, ...]</summary>
    public string FieldsJson { get; set; } = "[]";
}

public enum IntakeFormType
{
    Boarding = 0,
    Grooming = 1,
    Vet = 2,
    DayCare = 3,
    Consent = 4,
    Custom = 99,
}
