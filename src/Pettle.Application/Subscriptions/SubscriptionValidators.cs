using FluentValidation;
using Pettle.Application.Validation;

namespace Pettle.Application.Subscriptions;

public class CreateOrUpdatePackageValidator : AbstractValidator<CreateOrUpdatePackageRequest>
{
    public CreateOrUpdatePackageValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Package name is required.").MaximumLength(120);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.ValidityDays).InclusiveBetween(1, 3650).WithMessage("Validity must be between 1 and 3650 days.");
        RuleFor(x => x.Price).NonNegativeAmount();
        RuleFor(x => x.TaxPercent).ValidTaxPercent();
        RuleForEach(x => x.Services).SetValidator(new PackageServiceItemValidator()).When(x => x.Services is not null);
    }
}

// Nothing previously stopped a package's per-item Coverage from being negative, or over 100%
// in Percentage mode - either of which would silently corrupt how much a booking's subscription
// auto-debit deducts (see BookingService.CreateAsync's SUB-4/5 block).
public class PackageServiceItemValidator : AbstractValidator<PackageServiceItem>
{
    public PackageServiceItemValidator()
    {
        RuleFor(x => x.ServiceName).NotEmpty().WithMessage("Service name is required.").MaximumLength(200);
        RuleFor(x => x.Discount).GreaterThanOrEqualTo(0).WithMessage("Coverage cannot be negative.");
        RuleFor(x => x.Discount).LessThanOrEqualTo(100)
            .When(x => !string.Equals(x.DiscountType, "FlatAmount", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Coverage percent cannot exceed 100.");
        RuleFor(x => x.DaysOrSessions).GreaterThan(0).When(x => x.DaysOrSessions.HasValue)
            .WithMessage("Quantity/sessions must be greater than zero.");
        RuleFor(x => x.SkuId).NotNull()
            .When(x => string.Equals(x.ItemKind, "Sku", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Pick a SKU for this item.");
    }
}

public class IssueSubscriptionValidator : AbstractValidator<IssueSubscriptionRequest>
{
    public IssueSubscriptionValidator()
    {
        RuleFor(x => x.PackageId).NotEqual(Guid.Empty).WithMessage("Package is required.");
        RuleFor(x => x.PetParentId).NotEqual(Guid.Empty).WithMessage("Pet parent is required.");
        RuleFor(x => x.TotalSessions).InclusiveBetween(1, 999).WithMessage("Total sessions must be between 1 and 999.");
        RuleFor(x => x.AmountPaid).NonNegativeAmount();
        RuleFor(x => x.IssuedOn!.Value)
            .LessThanOrEqualTo(DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1))
            .When(x => x.IssuedOn.HasValue)
            .WithMessage("Issued-on date cannot be in the future.");
    }
}
