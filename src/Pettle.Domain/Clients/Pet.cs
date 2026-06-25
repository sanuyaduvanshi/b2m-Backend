using Pettle.Domain.Common;

namespace Pettle.Domain.Clients;

public class Pet : SoftDeletableTenantEntity
{
    public string? LegacyPetId { get; set; }
    public Guid PetParentId { get; set; }
    public PetParent? PetParent { get; set; }

    public string Name { get; set; } = string.Empty;
    public PetSpecies Species { get; set; }
    public string? Breed { get; set; }
    public PetGender? Gender { get; set; }
    public DateOnly? Birthday { get; set; }
    public string? Coat { get; set; }
    public BreedSize? BreedSize { get; set; }
    public decimal? WeightKg { get; set; }
    public string? PhotoUrl { get; set; }
    public bool ConsentToUsePetPhotos { get; set; }
    public bool BirthdayReminderEnabled { get; set; } = true;

    public PetMedicalProfile? MedicalProfile { get; set; }
    public ICollection<PetVaccination> Vaccinations { get; set; } = new List<PetVaccination>();
}

public enum PetSpecies { Dog = 0, Cat = 1, Bird = 2, Rabbit = 3, Other = 99 }
public enum PetGender { Male = 0, Female = 1, Unknown = 2 }
public enum BreedSize { Small = 0, Medium = 1, Large = 2, ExtraLarge = 3 }

public class PetMedicalProfile : TenantEntity
{
    public Guid PetId { get; set; }
    public Pet? Pet { get; set; }
    public string? VaccinationStatus { get; set; }
    public DateOnly? LastTickPreventionDate { get; set; }
    public string? TickPreventionMethod { get; set; }
    public bool OngoingMedication { get; set; }
    public string? MedicationDetail { get; set; }
    public string? MajorIllnessHistory { get; set; }
    public DateOnly? DewormingDate { get; set; }
}

public class PetVaccination : TenantEntity
{
    public Guid PetId { get; set; }
    public Pet? Pet { get; set; }
    public string VaccineCode { get; set; } = string.Empty;
    public string VaccineName { get; set; } = string.Empty;
    public DateOnly? GivenOn { get; set; }
    public DateOnly? NextDueOn { get; set; }
    public string? AdministeredBy { get; set; }
    public string? BatchNumber { get; set; }
    public string? Notes { get; set; }
}

public class ClientTag : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Color { get; set; }
    public string? Description { get; set; }
}

public class ClientTagAssignment
{
    public Guid PetParentId { get; set; }
    public PetParent? PetParent { get; set; }
    public Guid ClientTagId { get; set; }
    public ClientTag? ClientTag { get; set; }
    public DateTimeOffset AssignedAt { get; set; } = DateTimeOffset.UtcNow;
}
