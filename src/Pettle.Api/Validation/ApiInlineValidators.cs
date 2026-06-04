using FluentValidation;
using Pettle.Api.Controllers;

namespace Pettle.Api.Validation;

// Validators for small request records declared inside controllers.

public class MarkSentRequestValidator : AbstractValidator<MarkSentRequest>
{
    private static readonly string[] AllowedVias = { "WhatsApp", "SMS", "Email", "Push", "Manual" };
    public MarkSentRequestValidator()
    {
        RuleFor(x => x.Via)
            .NotEmpty().WithMessage("Channel is required.")
            .Must(v => AllowedVias.Contains(v))
            .WithMessage($"Channel must be one of: {string.Join(", ", AllowedVias)}.");
    }
}

public class CancelRequestValidator : AbstractValidator<CancelRequest>
{
    public CancelRequestValidator()
    {
        RuleFor(x => x.Reason).MaximumLength(500);
    }
}

public class ArchiveRequestValidator : AbstractValidator<ArchiveRequest>
{
    public ArchiveRequestValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Archive reason is required.").MaximumLength(500);
    }
}

public class CreateCategoryRequestValidator : AbstractValidator<CreateCategoryRequest>
{
    public CreateCategoryRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Category name is required.").MaximumLength(80);
    }
}
