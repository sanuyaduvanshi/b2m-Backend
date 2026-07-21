using FluentValidation;
using Pettle.Application.Validation;

namespace Pettle.Application.Invoices;

// CreateSaleRequest/UpdateInvoiceRequest previously had no registered validator at all - only the
// ad-hoc "Quantity <= 0" check inline in the service. Nothing stopped a negative UnitAmount, or a
// Discount%/Tax% outside 0-100 (which can flip a line net negative and corrupt the whole bill
// total), from reaching CreateSaleAsync/UpdateAsync.
public class CreateSaleLineValidator : AbstractValidator<CreateSaleLine>
{
    public CreateSaleLineValidator()
    {
        RuleFor(x => x.ItemName).NotEmpty().WithMessage("Item name is required.").MaximumLength(200);
        RuleFor(x => x.Quantity).PositiveAmount();
        RuleFor(x => x.UnitAmount).NonNegativeAmount();
        RuleFor(x => x.DiscountPercent).ValidTaxPercent().WithMessage("Discount percent must be between 0 and 100.");
        RuleFor(x => x.AddDiscountPercent).ValidTaxPercent().WithMessage("Additional discount percent must be between 0 and 100.");
        RuleFor(x => x.TaxPercent).ValidTaxPercent();
    }
}

public class CreateSalePaymentValidator : AbstractValidator<CreateSalePayment>
{
    public CreateSalePaymentValidator()
    {
        RuleFor(x => x.Amount).NonNegativeAmount();
        RuleFor(x => x.Mode).IsInEnum().WithMessage("Unknown payment mode.");
        RuleFor(x => x.TransactionId).MaximumLength(80);
    }
}

public class CreateSaleValidator : AbstractValidator<CreateSaleRequest>
{
    public CreateSaleValidator()
    {
        RuleFor(x => x.ParentName).MaximumLength(160);
        RuleFor(x => x.Phone).ValidPhoneFormat().When(x => !string.IsNullOrWhiteSpace(x.Phone));
        RuleFor(x => x.Notes).MaximumLength(2000);
        RuleFor(x => x.FlatDiscountPercent).ValidTaxPercent().WithMessage("Flat discount percent must be between 0 and 100.");
        RuleFor(x => x.AdditionalCharges).NonNegativeAmount();
        RuleFor(x => x.AdditionalChargesReason).MaximumLength(200);
        RuleFor(x => x.RedeemCreditNoteAmount).NonNegativeAmount();
        RuleFor(x => x.Lines).NotEmpty().WithMessage("Add at least one item to the sale.");
        RuleForEach(x => x.Lines).SetValidator(new CreateSaleLineValidator());
        RuleForEach(x => x.Payments).SetValidator(new CreateSalePaymentValidator());
    }
}

public class UpdateInvoiceLineValidator : AbstractValidator<UpdateInvoiceLine>
{
    public UpdateInvoiceLineValidator()
    {
        RuleFor(x => x.ItemName).NotEmpty().WithMessage("Item name is required.").MaximumLength(200);
        RuleFor(x => x.Quantity).PositiveAmount();
        RuleFor(x => x.UnitAmount).NonNegativeAmount();
        RuleFor(x => x.DiscountPercent).ValidTaxPercent().WithMessage("Discount percent must be between 0 and 100.");
        RuleFor(x => x.TaxPercent).ValidTaxPercent();
    }
}

public class UpdateInvoiceValidator : AbstractValidator<UpdateInvoiceRequest>
{
    public UpdateInvoiceValidator()
    {
        RuleFor(x => x.ParentName).NotEmpty().WithMessage("Client name is required.").MaximumLength(160);
        RuleFor(x => x.Phone).ValidPhoneFormat().When(x => !string.IsNullOrWhiteSpace(x.Phone));
        RuleFor(x => x.Notes).MaximumLength(2000);
        RuleFor(x => x.FlatDiscountPercent).ValidTaxPercent().WithMessage("Flat discount percent must be between 0 and 100.");
        RuleFor(x => x.AdditionalCharges).NonNegativeAmount();
        RuleFor(x => x.AdditionalChargesReason).MaximumLength(200);
        RuleFor(x => x.Lines).NotEmpty().WithMessage("Add at least one line item.");
        RuleForEach(x => x.Lines).SetValidator(new UpdateInvoiceLineValidator());
    }
}

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
