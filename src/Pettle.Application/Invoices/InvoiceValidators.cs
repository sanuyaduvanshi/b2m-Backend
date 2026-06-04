using FluentValidation;
using Pettle.Application.Validation;

namespace Pettle.Application.Invoices;

public class RecordPaymentValidator : AbstractValidator<RecordPaymentRequest>
{
    public RecordPaymentValidator()
    {
        RuleFor(x => x.Amount).PositiveAmount();
        RuleFor(x => x.Mode).IsInEnum().WithMessage("Unknown payment mode.");
        RuleFor(x => x.Source).IsInEnum().WithMessage("Unknown payment source.");
        RuleFor(x => x.TransactionId).MaximumLength(80);
        RuleFor(x => x.Notes).MaximumLength(500);
        RuleFor(x => x.PaymentTime!.Value)
            .LessThanOrEqualTo(_ => DateTimeOffset.UtcNow.AddMinutes(5))
            .When(x => x.PaymentTime.HasValue)
            .WithMessage("Payment time cannot be in the future.");
    }
}

public class RefundValidator : AbstractValidator<RefundRequest>
{
    public RefundValidator()
    {
        RuleFor(x => x.Amount).PositiveAmount();
        RuleFor(x => x.Reason).NotEmpty().WithMessage("A refund reason is required.").MaximumLength(500);
    }
}
