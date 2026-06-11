using Pettle.Application.Clients;
using Pettle.Domain.Subscriptions;

namespace Pettle.Application.Subscriptions;

public record PackageListItem(Guid Id, string Name, int ValidityDays, decimal Price, decimal TaxPercent, bool IsTaxInclusive, bool IsActive);
public record CreateOrUpdatePackageRequest(string Name, string? Description, int ValidityDays, decimal Price, decimal TaxPercent, bool IsTaxInclusive, bool IsActive);

public record IssuedListItem(
    Guid Id, string PackageName, Guid PetParentId, string ParentName, string Phone,
    DateOnly IssuedOn, DateOnly ValidUntil, int RemainingSessions, int TotalSessions,
    IssuedSubscriptionStatus Status, IssuedPaymentStatus PaymentStatus, decimal AmountPaid, decimal AmountDue
);

public record IssueSubscriptionRequest(Guid PackageId, Guid PetParentId, int TotalSessions, DateOnly? IssuedOn, decimal AmountPaid);

public interface ISubscriptionService
{
    Task<IReadOnlyList<PackageListItem>> ListPackagesAsync(CancellationToken ct = default);
    Task<PackageListItem> CreatePackageAsync(CreateOrUpdatePackageRequest req, CancellationToken ct = default);
    Task<PackageListItem?> UpdatePackageAsync(Guid id, CreateOrUpdatePackageRequest req, CancellationToken ct = default);
    Task<bool> DeletePackageAsync(Guid id, CancellationToken ct = default);

    Task<PagedResult<IssuedListItem>> ListIssuedAsync(string? search, IssuedSubscriptionStatus? status, int page, int pageSize, CancellationToken ct = default);
    Task<IssuedListItem> IssueAsync(IssueSubscriptionRequest req, CancellationToken ct = default);
    Task<bool> FreezeAsync(Guid id, CancellationToken ct = default);
    Task<bool> CancelAsync(Guid id, CancellationToken ct = default);
}
