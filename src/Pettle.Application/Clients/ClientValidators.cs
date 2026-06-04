using FluentValidation;
using Pettle.Application.Validation;
using Pettle.Domain.Clients;

namespace Pettle.Application.Clients;

public class CreatePetParentValidator : AbstractValidator<CreatePetParentRequest>
{
    public CreatePetParentValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Parent name is required.")
            .MaximumLength(120);
        RuleFor(x => x.Phone)
            .NotEmpty().WithMessage("Phone number is required.")
            .ValidPhoneFormat();
        RuleFor(x => x.Email).ValidEmailFormat().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.AlternatePhone).ValidPhoneFormat().When(x => !string.IsNullOrWhiteSpace(x.AlternatePhone));
        RuleFor(x => x.AddressLine1).MaximumLength(200);
        RuleFor(x => x.City).MaximumLength(80);
        RuleFor(x => x.State).MaximumLength(80);
        RuleFor(x => x.PostalCode).ValidIndianPostalFormat().When(x => !string.IsNullOrWhiteSpace(x.PostalCode));
        RuleFor(x => x.OnboardingDate)
            .Must(d => !d.HasValue || d.Value <= DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1))
            .WithMessage("Onboarding date cannot be in the future.");
        RuleFor(x => x.TermsAccepted)
            .Equal(true).WithMessage("Terms must be accepted before saving the client.");
    }
}

public class UpdatePetParentValidator : AbstractValidator<UpdatePetParentRequest>
{
    public UpdatePetParentValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Phone).NotEmpty().ValidPhoneFormat();
        RuleFor(x => x.Email).ValidEmailFormat().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.AlternatePhone).ValidPhoneFormat().When(x => !string.IsNullOrWhiteSpace(x.AlternatePhone));
        RuleFor(x => x.AddressLine1).MaximumLength(200);
        RuleFor(x => x.AddressLine2).MaximumLength(200);
        RuleFor(x => x.City).MaximumLength(80);
        RuleFor(x => x.State).MaximumLength(80);
        RuleFor(x => x.Country).MaximumLength(80);
        RuleFor(x => x.PostalCode).ValidIndianPostalFormat().When(x => !string.IsNullOrWhiteSpace(x.PostalCode));
        RuleFor(x => x.Status).IsInEnum().WithMessage("Unknown client status.");
        RuleFor(x => x.ArchiveReason)
            .NotEmpty().When(x => x.Status == ClientStatus.Archived)
            .WithMessage("Archive reason is required when archiving a client.")
            .MaximumLength(500);
    }
}
