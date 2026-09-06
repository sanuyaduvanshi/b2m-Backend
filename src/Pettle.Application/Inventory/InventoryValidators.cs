using FluentValidation;
using Pettle.Application.Invoices;
using Pettle.Application.Validation;

namespace Pettle.Application.Inventory;

public class CreateOrUpdateSkuValidator : AbstractValidator<CreateOrUpdateSkuRequest>
{
    public CreateOrUpdateSkuValidator()
    {
        RuleFor(x => x.Code).NotEmpty().WithMessage("SKU code is required.").MaximumLength(40)
            .Matches(@"^[A-Za-z0-9_\-\.]+$").WithMessage("Code may only contain letters, digits, '_', '-', '.'.");
        RuleFor(x => x.Name).NotEmpty().WithMessage("SKU name is required.").MaximumLength(160);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.Unit).NotEmpty().WithMessage("Unit is required.").MaximumLength(16);
        RuleFor(x => x.MrpPrice).NonNegativeAmount();
        RuleFor(x => x.SellingPrice).NonNegativeAmount();
        RuleFor(x => x.CostPrice).NonNegativeAmount();
        RuleFor(x => x.SellingPrice)
            .LessThanOrEqualTo(x => x.MrpPrice).When(x => x.MrpPrice > 0)
            .WithMessage("Selling price cannot exceed MRP.");
        RuleFor(x => x.TaxPercent).ValidTaxPercent();
        RuleFor(x => x.HsnSacCode).MaximumLength(20);
        RuleFor(x => x.ReorderLevel).GreaterThanOrEqualTo(0).WithMessage("Reorder level cannot be negative.");
    }
}

public class CreateOrUpdateProductValidator : AbstractValidator<CreateOrUpdateProductRequest>
{
    public CreateOrUpdateProductValidator()
    {
        RuleFor(x => x.Code).NotEmpty().WithMessage("Product code is required.").MaximumLength(60)
            .Matches(@"^[A-Za-z0-9_\-\.]+$").WithMessage("Product code may only contain letters, digits, '_', '-', '.'.");
        RuleFor(x => x.Name).NotEmpty().WithMessage("Product name is required.").MaximumLength(200);
        RuleFor(x => x.Category).MaximumLength(120);
        RuleFor(x => x.Brand).MaximumLength(120);
        RuleFor(x => x.HsnCode).MaximumLength(30);
        RuleFor(x => x.MrpPrice).NonNegativeAmount();
        RuleFor(x => x.SellingPrice).NonNegativeAmount()
            .LessThanOrEqualTo(x => x.MrpPrice).When(x => x.MrpPrice > 0)
            .WithMessage("Selling price cannot exceed MRP.");
        RuleFor(x => x.Quantity).InclusiveBetween(0, 1_000_000)
            .WithMessage("Quantity must be between 0 and 1,000,000.");
    }
}

public class CreateOrUpdateCategoryValidator : AbstractValidator<CreateOrUpdateCategoryRequest>
{
    public CreateOrUpdateCategoryValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Category name is required.").MaximumLength(80);
    }
}

public class CreateOrUpdateVendorValidator : AbstractValidator<CreateOrUpdateVendorRequest>
{
    public CreateOrUpdateVendorValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Vendor name is required.").MaximumLength(160);
        RuleFor(x => x.ContactPerson).MaximumLength(120);
        RuleFor(x => x.Phone).ValidPhoneFormat().When(x => !string.IsNullOrWhiteSpace(x.Phone));
        RuleFor(x => x.Email).ValidEmailFormat().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Address).MaximumLength(400);
        RuleFor(x => x.Gstin).ValidGstinFormat().When(x => !string.IsNullOrWhiteSpace(x.Gstin));
        RuleFor(x => x.CreditDays).InclusiveBetween(0, 365).WithMessage("Credit days must be between 0 and 365.");
        RuleFor(x => x.CreditLimit).NonNegativeAmount();
    }
}

public class CreatePoValidator : AbstractValidator<CreatePoRequest>
{
    public CreatePoValidator()
    {
        RuleFor(x => x.VendorId).NotEqual(Guid.Empty).WithMessage("Vendor is required.");
        RuleFor(x => x.PurchaseDate)
            .NotEqual(default(DateOnly)).WithMessage("Purchase date is required.")
            .LessThanOrEqualTo(_ => DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1))
            .WithMessage("Purchase date cannot be in the future.");
        RuleFor(x => x.VendorInvoiceNumber).MaximumLength(60);
        RuleFor(x => x.ReferenceBillNumber).MaximumLength(60);
        RuleFor(x => x.MaterialInwardNo).MaximumLength(60);
        RuleFor(x => x.PaymentTerm).MaximumLength(60);
        RuleFor(x => x.TaxType).MaximumLength(20);
        RuleFor(x => x.AccountLedger).MaximumLength(60);
        RuleFor(x => x.FlatDiscountPercent).InclusiveBetween(0, 100).WithMessage("Flat discount must be between 0 and 100%.");
        RuleFor(x => x.AdditionalCharges).NonNegativeAmount();
        RuleFor(x => x.DueDate).GreaterThanOrEqualTo(x => x.PurchaseDate)
            .When(x => x.DueDate.HasValue).WithMessage("Due date cannot be before the bill date.");
        RuleFor(x => x.Notes).MaximumLength(1000);
        RuleFor(x => x.Lines).NotEmpty().WithMessage("Add at least one line item.")
            .Must(l => l.Count <= 200).WithMessage("Maximum 200 line items per PO.");
        RuleForEach(x => x.Lines).SetValidator(new CreatePoLineValidator());
    }
}

public class CreatePoLineValidator : AbstractValidator<CreatePoLine>
{
    public CreatePoLineValidator()
    {
        RuleFor(x => x.ItemName).NotEmpty().WithMessage("Item name is required.").MaximumLength(160);
        RuleFor(x => x.ItemCode).MaximumLength(60);
        RuleFor(x => x.Unit).MaximumLength(16);
        RuleFor(x => x.Quantity).PositiveAmount();
        RuleFor(x => x.FreeQuantity).NonNegativeAmount();
        RuleFor(x => x.UnitCost).NonNegativeAmount();
        RuleFor(x => x.Mrp).NonNegativeAmount();
        RuleFor(x => x.SellingPrice).NonNegativeAmount();
        RuleFor(x => x.PurDisc1Percent).InclusiveBetween(0, 100).WithMessage("Discount 1 must be between 0 and 100%.");
        RuleFor(x => x.PurDisc2Percent).InclusiveBetween(0, 100).WithMessage("Discount 2 must be between 0 and 100%.");
        RuleFor(x => x.TaxPercent).ValidTaxPercent();
        RuleFor(x => x.BatchNumber).MaximumLength(60);
        RuleFor(x => x.ExpiryDate)
            .Must(d => !d.HasValue || d.Value >= DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("Expiry date is in the past — double-check before saving.");
    }
}

public class CreateDebitNoteValidator : AbstractValidator<CreateDebitNoteRequest>
{
    public CreateDebitNoteValidator()
    {
        RuleFor(x => x.VendorId).NotEqual(Guid.Empty).WithMessage("Supplier is required.");
        RuleFor(x => x.DebitNoteDate)
            .NotEqual(default(DateOnly)).WithMessage("Debit note date is required.")
            .LessThanOrEqualTo(_ => DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1))
            .WithMessage("Debit note date cannot be in the future.");
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Say why the goods are going back — damaged, expired, wrong item.")
            .MaximumLength(500);
        RuleFor(x => x.ReferenceBillNumber).MaximumLength(60);
        RuleFor(x => x.PaymentTerm).MaximumLength(60);
        RuleFor(x => x.TaxType).MaximumLength(20);
        RuleFor(x => x.AccountLedger).MaximumLength(60);
        RuleFor(x => x.FlatDiscountPercent).InclusiveBetween(0, 100).WithMessage("Flat discount must be between 0 and 100%.");
        RuleFor(x => x.AdditionalCharges).NonNegativeAmount();
        RuleFor(x => x.DueDate).GreaterThanOrEqualTo(x => x.DebitNoteDate)
            .When(x => x.DueDate.HasValue).WithMessage("Due date cannot be before the debit note date.");
        RuleFor(x => x.Notes).MaximumLength(1000);
        RuleFor(x => x.Lines).NotEmpty().WithMessage("Add at least one item to return.")
            .Must(l => l.Count <= 200).WithMessage("Maximum 200 line items per debit note.");
        RuleForEach(x => x.Lines).SetValidator(new DebitNoteLineValidator());
    }
}

/// <summary>Same shape as a purchase line, minus the "expiry is in the past" warning — returning
/// expired stock is the whole point of most debit notes.</summary>
public class DebitNoteLineValidator : AbstractValidator<CreatePoLine>
{
    public DebitNoteLineValidator()
    {
        RuleFor(x => x.ItemName).NotEmpty().WithMessage("Item name is required.").MaximumLength(160);
        RuleFor(x => x.ItemCode).MaximumLength(60);
        RuleFor(x => x.Unit).MaximumLength(16);
        RuleFor(x => x.Quantity).PositiveAmount();
        RuleFor(x => x.UnitCost).NonNegativeAmount();
        RuleFor(x => x.Mrp).NonNegativeAmount();
        RuleFor(x => x.SellingPrice).NonNegativeAmount();
        RuleFor(x => x.PurDisc1Percent).InclusiveBetween(0, 100).WithMessage("Discount 1 must be between 0 and 100%.");
        RuleFor(x => x.PurDisc2Percent).InclusiveBetween(0, 100).WithMessage("Discount 2 must be between 0 and 100%.");
        RuleFor(x => x.TaxPercent).ValidTaxPercent();
        RuleFor(x => x.BatchNumber).MaximumLength(60);
    }
}

public class RecordPoPaymentValidator : AbstractValidator<RecordPoPaymentRequest>
{
    public RecordPoPaymentValidator()
    {
        RuleFor(x => x.Amount).PositiveAmount();
        RuleFor(x => x.Mode).NotEmpty().WithMessage("Payment mode is required.").MaximumLength(40);
        RuleFor(x => x.Notes).MaximumLength(1000);
        // Same window the POS uses: yesterday's payment can be caught up on, but the books can't
        // be rewritten weeks back or dated into the future. Validated on the nullable property
        // itself so the error comes back keyed "paidOn" — keying it "paidOn.Value" would leave the
        // message stranded at the top of the form instead of under the date field.
        RuleFor(x => x.PaidOn)
            .Must(d => !d.HasValue || InvoiceDateWindow.IsAllowed(d.Value))
            .WithMessage(InvoiceDateWindow.PaymentMessage);
    }
}

public class ReceivePoValidator : AbstractValidator<ReceivePoRequest>
{
    public ReceivePoValidator()
    {
        RuleFor(x => x.Lines).NotEmpty().WithMessage("No receipt quantities provided.");
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.LineId).NotEqual(Guid.Empty);
            line.RuleFor(l => l.ReceivedQuantity).GreaterThanOrEqualTo(0).WithMessage("Received quantity cannot be negative.");
        });
    }
}
