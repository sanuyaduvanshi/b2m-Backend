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
    string? SkuName = null, string ItemKind = "Service", Guid? AddOnCatalogueId = null);

public record PackageListItem(
    Guid Id, string Name, int ValidityDays, decimal Price, decimal TaxPercent, bool IsTaxInclusive, bool IsActive,
    IReadOnlyList<PackageServiceItem>? Services = null, string Type = "Boarding", string? Description = null);

public record CreateOrUpdatePackageRequest(
    string Name, string? Description, int ValidityDays, decimal Price, decimal TaxPercent, bool IsTaxInclusive, bool IsActive,
    List<PackageServiceItem>? Services = null, string Type = "Boarding");

public record ActiveSubscriptionSummary(
    Guid Id, string PackageName, decimal PackagePrice, decimal BalanceUsed, decimal RemainingBalance,
    int RemainingSessions, int TotalSessions, DateOnly ValidUntil, string Status, Guid PackageId = default);

public record IssuedListItem(
    Guid Id, string PackageName, Guid PetParentId, string ParentName, string Phone,
    DateOnly IssuedOn, DateOnly ValidUntil, int RemainingSessions, int TotalSessions,
    IssuedSubscriptionStatus Status, IssuedPaymentStatus PaymentStatus, decimal AmountPaid, decimal AmountDue,
    decimal BalanceUsed = 0, string? PackageDescription = null
);

public record IssueSubscriptionRequest(Guid PackageId, Guid PetParentId, int TotalSessions, DateOnly? IssuedOn, decimal AmountPaid, PaymentMode Mode = PaymentMode.Cash);

/// <summary>Live (not date-ranged) count of issued subscriptions per lifecycle status — backs the
/// Subscriptions page's status breakdown cards, since "how many are Active right now" is a
/// current-state question, not something a Today/This Month period filter should scope.</summary>
public record SubscriptionStatusSummary(int Active, int Frozen, int Expired, int Cancelled, int Transferred);

// Public, unauthenticated view of an issued subscription — the WhatsApp "your subscription is
// confirmed" message links customers straight to this so they can see what they were billed for
// without logging in, so it only exposes what a customer should see (no internal ids/statuses).
public record PublicSubscriptionInvoice(
    string TenantName, string ParentName, string PackageName, string? PackageDescription,
    decimal Price, decimal AmountPaid, DateOnly IssuedOn, DateOnly ValidUntil, string? TenantLogoUrl = null);

public interface ISubscriptionService
{
    Task<IReadOnlyList<PackageListItem>> ListPackagesAsync(CancellationToken ct = default);
    Task<PackageListItem> CreatePackageAsync(CreateOrUpdatePackageRequest req, CancellationToken ct = default);
    Task<PackageListItem?> UpdatePackageAsync(Guid id, CreateOrUpdatePackageRequest req, CancellationToken ct = default);
    Task<bool> DeletePackageAsync(Guid id, CancellationToken ct = default);

    Task<PagedResult<IssuedListItem>> ListIssuedAsync(string? search, IssuedSubscriptionStatus? status, int page, int pageSize, CancellationToken ct = default);
    Task<SubscriptionStatusSummary> StatusSummaryAsync(CancellationToken ct = default);
    /// <summary>Every issued subscription matching the filters, unpaginated — backs the
    /// Subscriptions report's KPI cards and its CSV download so both read the same rows.</summary>
    Task<IReadOnlyList<IssuedListItem>> ExportIssuedAsync(string? search, IssuedSubscriptionStatus? status, Guid? packageId, DateOnly? from, DateOnly? to, CancellationToken ct = default);
    Task<IssuedListItem> IssueAsync(IssueSubscriptionRequest req, CancellationToken ct = default);
    Task<bool> FreezeAsync(Guid id, CancellationToken ct = default);
    Task<bool> CancelAsync(Guid id, CancellationToken ct = default);
    /// <summary>Hard-deletes an issued subscription — only allowed when AmountPaid is 0, mirroring
    /// invoices, so a subscription with money against it can't be removed except via Cancel.</summary>
    Task<bool> DeleteIssuedAsync(Guid id, CancellationToken ct = default);

    Task<IssuedSubscriptionDetail?> GetIssuedAsync(Guid id, CancellationToken ct = default);
    Task<PaymentDto?> RecordPaymentAsync(Guid issuedId, RecordSubscriptionPaymentRequest req, CancellationToken ct = default);
    Task<PaymentDto?> UpdatePaymentAsync(Guid issuedId, Guid paymentId, RecordSubscriptionPaymentRequest req, CancellationToken ct = default);
    Task<bool> DeletePaymentAsync(Guid issuedId, Guid paymentId, CancellationToken ct = default);

    Task<ActiveSubscriptionSummary?> GetActiveByClientAsync(Guid petParentId, string? packageType = null, CancellationToken ct = default);
    // A client can hold more than one active plan of the same type at once (e.g. two separate
    // Boarding packages) - the booking form lets staff pick which one to apply rather than only
    // ever surfacing whichever was issued most recently.
    Task<IReadOnlyList<ActiveSubscriptionSummary>> GetActiveSubscriptionsByClientAsync(Guid petParentId, string? packageType = null, CancellationToken ct = default);

    Task<PublicSubscriptionInvoice?> GetPublicInvoiceAsync(Guid issuedId, CancellationToken ct = default);
    Task<byte[]?> GeneratePublicInvoicePdfAsync(Guid issuedId, CancellationToken ct = default);
}
