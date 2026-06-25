using Pettle.Domain.Clients;
using Pettle.Domain.Common;

namespace Pettle.Domain.Subscriptions;

public class SubscriptionPackage : SoftDeletableTenantEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int ValidityDays { get; set; }
    public decimal Price { get; set; }
    public bool IsTaxInclusive { get; set; }
    public decimal TaxPercent { get; set; }
    public bool IsActive { get; set; } = true;
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
}

public enum DiscountType { Percentage = 0, FlatAmount = 1 }

public class IssuedSubscription : SoftDeletableTenantEntity
{
    public Guid PackageId { get; set; }
    public SubscriptionPackage? Package { get; set; }
    public Guid PetParentId { get; set; }
    public PetParent? PetParent { get; set; }
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
}

public enum IssuedSubscriptionStatus { Active = 0, Expired = 1, Cancelled = 2, Frozen = 3, Transferred = 4 }
public enum IssuedPaymentStatus { Pending = 0, PartiallyPaid = 1, Paid = 2, Refunded = 3 }
