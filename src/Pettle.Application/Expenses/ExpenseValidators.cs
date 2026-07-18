using FluentValidation;
using Pettle.Application.Validation;

namespace Pettle.Application.Expenses;

public class CreateOrUpdateExpenseValidator : AbstractValidator<CreateOrUpdateExpenseRequest>
{
    private static readonly string[] AllowedModes = { "Cash", "Card", "Upi", "NetBanking", "Wallet", "Cheque", "Credit", "Other" };

    public CreateOrUpdateExpenseValidator()
    {
        RuleFor(x => x.Description).NotEmpty().WithMessage("Description is required.").MaximumLength(500);
        RuleFor(x => x.PaymentMode)
            .NotEmpty().WithMessage("Payment mode is required.")
            .Must(m => AllowedModes.Contains(m))
            .WithMessage($"Payment mode must be one of: {string.Join(", ", AllowedModes)}.");
        RuleFor(x => x.Amount).PositiveAmount();
        RuleFor(x => x.AmountIncTax)
            .GreaterThanOrEqualTo(x => x.Amount)
            .WithMessage("Amount inclusive of tax cannot be less than the base amount.")
            .Must((req, incTax) => incTax == 0 || incTax <= req.Amount * 1.35m)
            .WithMessage("That's over 35% tax on the base amount — double-check the values.");
        RuleFor(x => x.Time)
            .LessThanOrEqualTo(_ => DateTimeOffset.UtcNow.AddMinutes(5))
            .WithMessage("Expense time cannot be in the future.")
            .GreaterThan(new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero))
            .WithMessage("Expense time looks invalid.");
        RuleFor(x => x.Notes).MaximumLength(1000);
    }
}
