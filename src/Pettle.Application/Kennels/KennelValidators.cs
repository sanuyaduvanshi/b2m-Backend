using FluentValidation;
using Pettle.Application.Validation;

namespace Pettle.Application.Kennels;

public class CreateOrUpdateKennelValidator : AbstractValidator<CreateOrUpdateKennelRequest>
{
    public CreateOrUpdateKennelValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Kennel name is required.").MaximumLength(80);
        RuleFor(x => x.KennelType).MaximumLength(60);
        RuleFor(x => x.SizeClass).MaximumLength(40);
        RuleFor(x => x.Capacity).InclusiveBetween(1, 50).WithMessage("Capacity must be between 1 and 50.");
        RuleFor(x => x.PricePerNight!.Value).NonNegativeAmount().When(x => x.PricePerNight.HasValue);
        RuleFor(x => x.AllowedSpecies).MaximumLength(120);
    }
}

public class KennelBlockValidator : AbstractValidator<KennelBlockRequest>
{
    public KennelBlockValidator()
    {
        RuleFor(x => x.FromDate).NotEqual(default(DateOnly)).WithMessage("From date is required.");
        RuleFor(x => x.ToDate).NotEqual(default(DateOnly)).WithMessage("To date is required.");
        RuleFor(x => x)
            .Must(x => x.ToDate >= x.FromDate)
            .WithMessage("To date must be on or after From date.")
            .OverridePropertyName(nameof(KennelBlockRequest.ToDate));
        RuleFor(x => x)
            .Must(x => x.ToDate.DayNumber - x.FromDate.DayNumber <= 365)
            .WithMessage("Block range cannot exceed 1 year.")
            .OverridePropertyName(nameof(KennelBlockRequest.ToDate));
        RuleFor(x => x.Reason).IsInEnum().WithMessage("Unknown block reason.");
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}
