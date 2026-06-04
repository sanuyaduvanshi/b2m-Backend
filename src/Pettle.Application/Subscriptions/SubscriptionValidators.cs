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
