using Pettle.Domain.Clients;
using Pettle.Domain.Common;

namespace Pettle.Domain.Emr;

/// <summary>One visit in a pet's Electronic Medical Record: what was wrong, what was diagnosed and the
/// prescription (the Medicines rows) handed to the parent.</summary>
public class EmrRecord : SoftDeletableTenantEntity
{
    public Guid PetParentId { get; set; }
    public PetParent? PetParent { get; set; }
    public Guid PetId { get; set; }
    public Pet? Pet { get; set; }

    public DateOnly VisitDate { get; set; }
    /// <summary>The pet's weight (kg) at this visit - a snapshot; the pet's own record is updated to match.</summary>
    public decimal? PetWeightKg { get; set; }
    public string? Complaint { get; set; }
    public string? Diagnosis { get; set; }
    public string? Advice { get; set; }
    public DateOnly? NextVisitDate { get; set; }
    public string? DoctorName { get; set; }

    public ICollection<EmrMedicine> Medicines { get; set; } = new List<EmrMedicine>();
}

/// <summary>One prescription row: the medicine and the dose for Morning / Afternoon / Night (free text such as
/// "1", "1/2" or "-"), plus a comment.</summary>
public class EmrMedicine : TenantEntity
{
    public Guid EmrRecordId { get; set; }
    public EmrRecord? EmrRecord { get; set; }
    public int SortOrder { get; set; }
    public string MedicineName { get; set; } = string.Empty;
    public string? Morning { get; set; }
    public string? Afternoon { get; set; }
    public string? Night { get; set; }
    public string? Comments { get; set; }
}
