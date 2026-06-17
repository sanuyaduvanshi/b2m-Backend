using FluentValidation;
using Pettle.Application.Validation;
using Pettle.Domain.MyBusiness;

namespace Pettle.Application.MyBusiness;

public class UpdateTenantProfileValidator : AbstractValidator<UpdateTenantProfileRequest>
{
    public UpdateTenantProfileValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Business name is required.").MaximumLength(120);

        RuleFor(x => x.LogoUrl)
            .Must(url => Uri.TryCreate(url, UriKind.Absolute, out var u) && (u.Scheme == "http" || u.Scheme == "https"))
            .When(x => !string.IsNullOrWhiteSpace(x.LogoUrl))
            .WithMessage("Logo URL must be a valid http(s) URL.");

        RuleFor(x => x.PrimaryColor).ValidHexColorFormat().When(x => !string.IsNullOrWhiteSpace(x.PrimaryColor));
        RuleFor(x => x.SecondaryColor).ValidHexColorFormat().When(x => !string.IsNullOrWhiteSpace(x.SecondaryColor));
        RuleFor(x => x.AccentColor).ValidHexColorFormat().When(x => !string.IsNullOrWhiteSpace(x.AccentColor));

        RuleFor(x => x.Currency)
            .Length(3).When(x => !string.IsNullOrWhiteSpace(x.Currency))
            .WithMessage("Currency must be a 3-letter ISO code (e.g., INR, USD).");

        RuleFor(x => x.Locale)
            .Matches(@"^[a-z]{2}(-[A-Z]{2})?$").When(x => !string.IsNullOrWhiteSpace(x.Locale))
            .WithMessage("Locale must look like 'en' or 'en-IN'.");

        RuleFor(x => x.TimeZone)
            .Must(tz => { try { TimeZoneInfo.FindSystemTimeZoneById(tz!); return true; } catch { return false; } })
            .When(x => !string.IsNullOrWhiteSpace(x.TimeZone))
            .WithMessage("Unknown time zone identifier.");

        RuleFor(x => x.IdleSessionMinutes)
            .InclusiveBetween(5, 1440).WithMessage("Idle session timeout must be between 5 and 1440 minutes (24h).");
    }
}

public class CreateOrUpdateServiceValidator : AbstractValidator<CreateOrUpdateServiceRequest>
{
    private static readonly string[] AllowedVerticals = { "Boarding", "Grooming", "Vet", "DayCare", "Training", "Retail", "AddOn" };

    public CreateOrUpdateServiceValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Service name is required.").MaximumLength(120);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.Vertical)
            .NotEmpty().WithMessage("Vertical is required.")
            .Must(v => AllowedVerticals.Contains(v))
            .WithMessage($"Vertical must be one of: {string.Join(", ", AllowedVerticals)}.");
        RuleFor(x => x.BasePrice).NonNegativeAmount();
        RuleFor(x => x.TaxPercent!.Value).ValidTaxPercent().When(x => x.TaxPercent.HasValue);
        RuleFor(x => x.DurationMinutes)
            .InclusiveBetween(1, 24 * 60).When(x => x.DurationMinutes.HasValue)
            .WithMessage("Duration must be between 1 and 1440 minutes.");

        RuleForEach(x => x.Variants).ChildRules(v =>
        {
            v.RuleFor(z => z.Name).NotEmpty().WithMessage("Variant name is required.").MaximumLength(120);
            v.RuleFor(z => z.Price).NonNegativeAmount();
            v.RuleFor(z => z.SizeClass).MaximumLength(60);
            v.RuleFor(z => z.Notes).MaximumLength(500);
        }).When(x => x.Variants is not null);
    }
}

public class CreateOrUpdateAddOnServiceValidator : AbstractValidator<CreateOrUpdateAddOnServiceRequest>
{
    public CreateOrUpdateAddOnServiceValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Add-on name is required.").MaximumLength(120);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.Price).NonNegativeAmount();
        RuleFor(x => x.TaxPercent!.Value).ValidTaxPercent().When(x => x.TaxPercent.HasValue);
    }
}

public class CreateOrUpdateVetCatalogueItemValidator : AbstractValidator<CreateOrUpdateVetCatalogueItemRequest>
{
    public CreateOrUpdateVetCatalogueItemValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Name is required.").MaximumLength(160);
        RuleFor(x => x.Content).MaximumLength(8000);
        RuleFor(x => x.Price!.Value).NonNegativeAmount().When(x => x.Price.HasValue);
    }
}

public class CreateServiceCategoryValidator : AbstractValidator<CreateServiceCategoryRequest>
{
    public CreateServiceCategoryValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Category name is required.").MaximumLength(120);
        RuleFor(x => x.Description).MaximumLength(500);
    }
}

public class CreateOrUpdateStaffValidator : AbstractValidator<CreateOrUpdateStaffRequest>
{
    public CreateOrUpdateStaffValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Staff name is required.").MaximumLength(120);
        RuleFor(x => x.Phone).ValidPhoneFormat().When(x => !string.IsNullOrWhiteSpace(x.Phone));
        RuleFor(x => x.Email).ValidEmailFormat().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.RoleLabel).MaximumLength(80);
        RuleFor(x => x.Vertical).MaximumLength(60);
    }
}

public class CreateOrUpdateTaxValidator : AbstractValidator<CreateOrUpdateTaxRequest>
{
    public CreateOrUpdateTaxValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Tax name is required.").MaximumLength(80);
        RuleFor(x => x.Kind).IsInEnum().WithMessage("Unknown tax kind.");
        RuleFor(x => x.Percent).ValidTaxPercent();
        RuleFor(x => x.EffectiveFrom).NotEqual(default(DateOnly)).WithMessage("Effective-from date is required.");
        RuleFor(x => x)
            .Must(x => !x.EffectiveTo.HasValue || x.EffectiveTo.Value >= x.EffectiveFrom)
            .WithMessage("Effective-to must be on or after Effective-from.")
            .OverridePropertyName(nameof(CreateOrUpdateTaxRequest.EffectiveTo));
    }
}
