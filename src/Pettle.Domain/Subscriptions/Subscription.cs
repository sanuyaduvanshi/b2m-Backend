using Pettle.Domain.Clients;
using Pettle.Domain.Common;

namespace Pettle.Domain.Subscriptions;

public class SubscriptionPackage : SoftDeletableTenantEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public SubscriptionPackageType Type { get; set; } = SubscriptionPackageType.Boarding;
    public int ValidityDays { get; set; }
    public decimal Price { get; set; }
    public bool IsTaxInclusive { get; set; }
    public decimal TaxPercent { get; set; }
    public bool IsActive { get; set; } = true;
    /// <summary>Whether a plan sold from this package belongs to one animal or to the household.
    ///
    /// A vaccination course or a grooming card is bought for a particular pet — letting a sibling
    /// eat the sessions is wrong both medically and commercially. A boarding package is often
    /// genuinely household-level, since the parent may board whichever animal that week. Only the
    /// business knows which of its packages is which, so it is recorded per package rather than
    /// guessed at issue time.</summary>
    public SubscriptionScope AppliesTo { get; set; } = SubscriptionScope.PerCustomer;
    public ICollection<SubscriptionPackageService> Services { get; set; } = new List<SubscriptionPackageService>();
}

public class SubscriptionPackageService : TenantEntity
{
    public Guid PackageId { get; set; }
    public SubscriptionPackage? Package { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public decimal Discount { get; set; }
    public DiscountType DiscountType { get; set; } = DiscountType.Percentage;
    public int? DaysOrSessions { get; set; }
    public string? BoardingType { get; set; }
    public string? SkuCategory { get; set; }
    public string? SkuSubCategory { get; set; }
    public Guid? SkuId { get; set; }
    // Which catalogue this row was picked from and, therefore, which booking lines it's matched
    // against at deduction time (see BookingService.CreateAsync's SUB-4/5 block): Service rows
    // match booking Services by name, Sku rows match booking InventoryItems by SkuId, AddOn rows
    // match booking AddOns by catalogue id. Existing rows default to Service (0) so pre-migration
    // packages keep matching exactly as they did before this split.
    public PackageItemKind ItemKind { get; set; } = PackageItemKind.Service;
    public Guid? AddOnCatalogueId { get; set; }
}

// PerCustomer = 0 so every package that existed before this field keeps its old, household-wide
// behaviour on migration; the ones that should be per-pet are set explicitly.
public enum SubscriptionScope { PerCustomer = 0, PerPet = 1 }

public enum DiscountType { Percentage = 0, FlatAmount = 1 }
// Boarding = 0 so existing packages (created before this field existed) default to Boarding —
// the most reasonable fallback for legacy data, and matches SubscriptionPackage.Type's own default.
public enum SubscriptionPackageType { Boarding = 0, Vet = 1, Grooming = 2 }
public enum PackageItemKind { Service = 0, Sku = 1, AddOn = 2 }

public class IssuedSubscription : SoftDeletableTenantEntity
{
    public Guid PackageId { get; set; }
    public SubscriptionPackage? Package { get; set; }
    public Guid PetParentId { get; set; }
    public PetParent? PetParent { get; set; }
    /// <summary>Which animal this plan is for. Null means the whole household may use it — the
    /// only behaviour that existed before this field, so every plan issued until now keeps working
    /// exactly as it did.</summary>
    public Guid? PetId { get; set; }
    public Pet? Pet { get; set; }
    public DateOnly IssuedOn { get; set; }
    public DateOnly ValidUntil { get; set; }
    public int RemainingSessions { get; set; }
    public int TotalSessions { get; set; }
    public IssuedSubscriptionStatus Status { get; set; } = IssuedSubscriptionStatus.Active;
    public IssuedPaymentStatus PaymentStatus { get; set; } = IssuedPaymentStatus.Pending;
    public decimal AmountPaid { get; set; }
    public decimal AmountDue { get; set; }
    public decimal BalanceUsed { get; set; }
    public Guid? FrozenUntilTransferredTo { get; set; }
    public DateTimeOffset? FrozenAt { get; set; }
    // Only required (enforced in SubscriptionService.DeleteIssuedAsync) when deleting a still-Active
    // or Frozen subscription — captures why staff removed a plan that was still in use.
    public string? DeletedReason { get; set; }
}

public enum IssuedSubscriptionStatus { Active = 0, Expired = 1, Cancelled = 2, Frozen = 3, Transferred = 4 }
public enum IssuedPaymentStatus { Pending = 0, PartiallyPaid = 1, Paid = 2, Refunded = 3 }
