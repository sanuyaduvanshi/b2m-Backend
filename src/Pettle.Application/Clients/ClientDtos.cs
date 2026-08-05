using Pettle.Domain.Clients;

namespace Pettle.Application.Clients;

public record PetParentListItem(
    Guid Id,
    string? LegacyClientId,
    string Name,
    string Phone,
    string? Email,
    string? City,
    int PetCount,
    decimal OutstandingBalance,
    decimal WalletBalance,
    ClientStatus Status,
    DateOnly? OnboardingDate,
    DateOnly? LatestBookingDate,
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> PetBreeds,
    // The imported B2M data put the locality ("Kukatpally", "Moosapet", …) in AddressLine1 and
    // left City entirely null, so a City-only column renders blank for every row — the list needs
    // both to be able to fall back.
    string? AddressLine1 = null,
    /// <summary>The pets by name. A breed list answers "what kind of animal"; on a client report
    /// the name is what staff and the owner actually recognise the pet by.</summary>
    IReadOnlyList<string>? PetNames = null
);

public record PetParentDetail(
    Guid Id,
    string? LegacyClientId,
    string Name,
    string Phone,
    string? Email,
    string? AlternatePhone,
    string? AddressLine1,
    string? AddressLine2,
    string? City,
    string? State,
    string? Country,
    string? PostalCode,
    DateOnly? OnboardingDate,
    decimal WalletBalance,
    decimal OutstandingBalance,
    bool TermsAccepted,
    ClientStatus Status,
    string? ArchiveReason,
    IReadOnlyList<PetSummary> Pets,
    IReadOnlyList<string> Tags
);

public record PetSummary(
    Guid Id,
    string? LegacyPetId,
    string Name,
    PetSpecies Species,
    string? Breed,
    PetGender? Gender,
    DateOnly? Birthday,
    BreedSize? BreedSize,
    decimal? WeightKg,
    string? PhotoUrl,
    bool BirthdayReminderEnabled = true
);

public record CreatePetParentRequest(
    string Name,
    string Phone,
    string? Email,
    string? AlternatePhone,
    string? AddressLine1,
    string? AddressLine2,
    string? City,
    string? State,
    string? Country,
    string? PostalCode,
    DateOnly? OnboardingDate,
    bool TermsAccepted
);

public record UpdatePetParentRequest(
    string Name,
    string Phone,
    string? Email,
    string? AlternatePhone,
    string? AddressLine1,
    string? AddressLine2,
    string? City,
    string? State,
    string? Country,
    string? PostalCode,
    ClientStatus Status,
    string? ArchiveReason
);

public record CreatePetRequest(
    string Name,
    PetSpecies Species,
    string? Breed,
    PetGender? Gender,
    DateOnly? Birthday,
    BreedSize? BreedSize,
    decimal? WeightKg,
    bool BirthdayReminderEnabled = true
);

public record UpdatePetRequest(
    string Name,
    PetSpecies Species,
    string? Breed,
    PetGender? Gender,
    DateOnly? Birthday,
    BreedSize? BreedSize,
    decimal? WeightKg,
    bool BirthdayReminderEnabled = true
);

public record ClientListQuery(
    string? Search,
    ClientStatus? Status,
    int Page = 1,
    int PageSize = 50,
    string? Sort = "name",
    bool Desc = false,
    /// <summary>Only clients who still owe money — server-side so it holds across pages, unlike
    /// filtering the current page's rows in the UI.</summary>
    bool? HasDues = null
);

public record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);
