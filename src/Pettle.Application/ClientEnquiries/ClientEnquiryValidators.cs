using FluentValidation;
using Pettle.Application.Validation;

namespace Pettle.Application.ClientEnquiries;

public class CreateClientEnquiryValidator : AbstractValidator<CreateClientEnquiryRequest>
{
    public CreateClientEnquiryValidator()
    {
        RuleFor(x => x.ParentName).NotEmpty().WithMessage("Parent name required.").MaximumLength(120);
        RuleFor(x => x.Phone).NotEmpty().WithMessage("Phone required.").ValidPhoneFormat();
        RuleFor(x => x.Email).ValidEmailFormat().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.PetName).MaximumLength(80);
        RuleFor(x => x.Message).MaximumLength(2000);
        RuleFor(x => x.Source).IsInEnum().WithMessage("Unknown enquiry source.");
    }
}

public class UpdateClientEnquiryValidator : AbstractValidator<UpdateClientEnquiryRequest>
{
    public UpdateClientEnquiryValidator()
    {
        RuleFor(x => x.ParentName).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Phone).NotEmpty().ValidPhoneFormat();
        RuleFor(x => x.Email).ValidEmailFormat().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.PetName).MaximumLength(80);
        RuleFor(x => x.Message).MaximumLength(2000);
        RuleFor(x => x.AssignedToName).MaximumLength(120);
    }
}

public class RejectClientEnquiryValidator : AbstractValidator<RejectClientEnquiryRequest>
{
    public RejectClientEnquiryValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Reject reason required.").MaximumLength(500);
    }
}

public class ConvertEnquiryValidator : AbstractValidator<ConvertEnquiryRequest>
{
    public ConvertEnquiryValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Client name required.").MaximumLength(120);
        RuleFor(x => x.Phone).NotEmpty().WithMessage("Phone required.").ValidPhoneFormat();
        RuleFor(x => x.Email).ValidEmailFormat().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.AlternatePhone).ValidPhoneFormat().When(x => !string.IsNullOrWhiteSpace(x.AlternatePhone));
        RuleFor(x => x.PostalCode).ValidIndianPostalFormat().When(x => !string.IsNullOrWhiteSpace(x.PostalCode));
        RuleFor(x => x.AddressLine1).MaximumLength(200);
        RuleFor(x => x.City).MaximumLength(80);
        RuleFor(x => x.State).MaximumLength(80);
    }
}
