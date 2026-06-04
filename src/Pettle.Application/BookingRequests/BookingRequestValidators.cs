using FluentValidation;
using Pettle.Application.Validation;

namespace Pettle.Application.BookingRequests;

public class CreateBookingRequestValidator : AbstractValidator<CreateBookingRequestRequest>
{
    public CreateBookingRequestValidator()
    {
        RuleFor(x => x.ParentName).NotEmpty().WithMessage("Parent name required.").MaximumLength(120);
        RuleFor(x => x.Phone).NotEmpty().WithMessage("Phone required.").ValidPhoneFormat();
        RuleFor(x => x.Email).ValidEmailFormat().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.PetName).MaximumLength(80);
        RuleFor(x => x.RequestedServiceType).IsInEnum().WithMessage("Unknown service type.");
        RuleFor(x => x.RequestedDate)
            .NotEqual(default(DateOnly)).WithMessage("Requested date required.")
            .GreaterThanOrEqualTo(DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1))
            .WithMessage("Requested date cannot be in the past.");
        RuleFor(x => x.Notes).MaximumLength(2000);
    }
}

public class RejectBookingRequestValidator : AbstractValidator<RejectBookingRequestRequest>
{
    public RejectBookingRequestValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Reject reason required.").MaximumLength(500);
    }
}
