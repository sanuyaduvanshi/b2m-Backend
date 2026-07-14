using Pettle.Application.Clients;
using Pettle.Application.Invoices;
using Pettle.Domain.Invoices;
using Pettle.Domain.Subscriptions;

namespace Pettle.Application.Subscriptions;

public record RecordSubscriptionPaymentRequest(
    decimal Amount, PaymentMode Mode, PaymentSource Source = PaymentSource.WalkIn,
    string? TransactionId = null, string? Notes = null,
    PaymentType Type = PaymentType.Balance, PaymentRecordStatus Status = PaymentRecordStatus.Success,
    DateTimeOffset? PaymentTime = null);

public record IssuedSubscriptionDetail(
    Guid Id, string PackageName, Guid PetParentId, string ParentName, string Phone,
    DateOnly IssuedOn, DateOnly ValidUntil, int RemainingSessions, int TotalSessions,
    IssuedSubscriptionStatus Status, IssuedPaymentStatus PaymentStatus, decimal AmountPaid, decimal AmountDue,
    IReadOnlyList<PaymentDto> Payments, decimal BalanceUsed = 0);

public record PackageServiceItem(
    string ServiceName, decimal Discount, string DiscountType,
    int? DaysOrSessions, string? BoardingType, string? SkuCategory, string? SkuSubCategory, Guid? SkuId,
    string? SkuName = null);

public record PackageListItem(
    Guid Id, string Name, int ValidityDays, decimal Price, decimal TaxPercent, bool IsTaxInclusive, bool IsActive,
    IReadOnlyList<PackageServiceItem>? Services = null, string Type = "Boarding");

public record CreateOrUpdatePackageRequest(
    string Name, string? Description, int ValidityDays, decimal Price, decimal TaxPercent, bool IsTaxInclusive, bool IsActive,
    List<PackageServiceItem>? Services = null, string Type = "Boarding");

public record ActiveSubscriptionSummary(
    Guid Id, string PackageName, decimal PackagePrice, decimal BalanceUsed, decimal RemainingBalance,
    int RemainingSessions, int TotalSessions, DateOnly ValidUntil, string Status);

public record IssuedListItem(
    Guid Id, string PackageName, Guid PetParentId, string ParentName, string Phone,
    DateOnly IssuedOn, DateOnly ValidUntil, int RemainingSessions, int TotalSessions,
    IssuedSubscriptionStatus Status, IssuedPaymentStatus PaymentStatus, decimal AmountPaid, decimal AmountDue,
    decimal BalanceUsed = 0
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

    Task<IssuedSubscriptionDetail?> GetIssuedAsync(Guid id, CancellationToken ct = default);
    Task<PaymentDto?> RecordPaymentAsync(Guid issuedId, RecordSubscriptionPaymentRequest req, CancellationToken ct = default);
    Task<bool> DeletePaymentAsync(Guid issuedId, Guid paymentId, CancellationToken ct = default);

    Task<ActiveSubscriptionSummary?> GetActiveByClientAsync(Guid petParentId, CancellationToken ct = default);
}
