using Pettle.Application.Clients;
using Pettle.Domain.ClientEnquiries;

namespace Pettle.Application.ClientEnquiries;

public record ClientEnquiryRow(
    Guid Id,
    string? LegacyEnquiryId,
    EnquirySource Source,
    EnquiryStatus Status,
    string ParentName,
    string Phone,
    string? Email,
    string? PetName,
    string? Message,
    Guid? AssignedToUserId,
    string? AssignedToName,
    DateTimeOffset? ResolvedAt,
    string? ResolvedByName,
    string? RejectionReason,
    Guid? ConvertedClientId,
    DateTimeOffset CreatedAt
);

public record ClientEnquiryCounts(int All, int Pending, int Completed, int Rejected);

public record ClientEnquiryBoard(ClientEnquiryCounts Counts, PagedResult<ClientEnquiryRow> Page);

public record CreateClientEnquiryRequest(
    string ParentName,
    string Phone,
    string? Email,
    string? PetName,
    string? Message,
    EnquirySource Source
);

public record UpdateClientEnquiryRequest(
    string ParentName,
    string Phone,
    string? Email,
    string? PetName,
    string? Message,
    Guid? AssignedToUserId,
    string? AssignedToName
);

public record RejectClientEnquiryRequest(string Reason);

public record ConvertEnquiryRequest(
    string Name,
    string Phone,
    string? Email,
    string? AlternatePhone,
    string? AddressLine1,
    string? City,
    string? State,
    string? PostalCode,
    DateOnly? OnboardingDate,
    bool TermsAccepted
);

public record ConvertEnquiryResult(Guid EnquiryId, Guid PetParentId);

public interface IClientEnquiryService
{
    Task<ClientEnquiryBoard> ListAsync(string? tab, string? search, string? source, int page, int pageSize, CancellationToken ct = default);
    Task<ClientEnquiryRow?> GetAsync(Guid id, CancellationToken ct = default);
    Task<ClientEnquiryRow> CreateAsync(CreateClientEnquiryRequest req, CancellationToken ct = default);
    Task<ClientEnquiryRow?> UpdateAsync(Guid id, UpdateClientEnquiryRequest req, CancellationToken ct = default);
    Task<bool> RejectAsync(Guid id, RejectClientEnquiryRequest req, CancellationToken ct = default);
    Task<ConvertEnquiryResult?> ConvertToClientAsync(Guid id, ConvertEnquiryRequest req, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
}
