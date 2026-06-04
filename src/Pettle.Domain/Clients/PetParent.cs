using Pettle.Domain.Common;

namespace Pettle.Domain.Clients;

public class PetParent : SoftDeletableTenantEntity
{
    public string? LegacyClientId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? AlternatePhone { get; set; }
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Country { get; set; } = "India";
    public string? PostalCode { get; set; }
    public DateOnly? OnboardingDate { get; set; }
    public decimal WalletBalance { get; set; }
    public decimal OutstandingBalance { get; set; }
    public bool TermsAccepted { get; set; }
    public ClientStatus Status { get; set; } = ClientStatus.Active;
    public string? ArchiveReason { get; set; }

    public ICollection<Pet> Pets { get; set; } = new List<Pet>();
    public ICollection<ClientTagAssignment> Tags { get; set; } = new List<ClientTagAssignment>();
}

public enum ClientStatus
{
    Active = 0,
    Archived = 1,
    Blacklisted = 2
}
