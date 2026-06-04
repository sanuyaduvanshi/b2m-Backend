using FluentValidation;

namespace Pettle.Application.Messages;

public class CreateMessageTemplateValidator : AbstractValidator<CreateMessageTemplateRequest>
{
    public CreateMessageTemplateValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Template name required.").MaximumLength(120);
        RuleFor(x => x.Channel).IsInEnum().WithMessage("Unknown channel.");
        RuleFor(x => x.Category).IsInEnum().WithMessage("Unknown category.");
        RuleFor(x => x.Body).NotEmpty().WithMessage("Template body required.").MaximumLength(4000);
        RuleFor(x => x.Subject).MaximumLength(200);
        RuleFor(x => x.Subject)
            .NotEmpty().WithMessage("Subject required for Email templates.")
            .When(x => x.Channel == Pettle.Domain.Messages.MessageChannel.Email);
    }
}

public class UpdateMessageTemplateValidator : AbstractValidator<UpdateMessageTemplateRequest>
{
    public UpdateMessageTemplateValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Channel).IsInEnum();
        RuleFor(x => x.Category).IsInEnum();
        RuleFor(x => x.Body).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.Subject).MaximumLength(200);
        RuleFor(x => x.Subject)
            .NotEmpty().WithMessage("Subject required for Email templates.")
            .When(x => x.Channel == Pettle.Domain.Messages.MessageChannel.Email);
    }
}

public class SendMessageValidator : AbstractValidator<SendMessageRequest>
{
    public SendMessageValidator()
    {
        RuleFor(x => x.PetParentId).NotEqual(Guid.Empty).WithMessage("Recipient required.");
        RuleFor(x => x.Channel).IsInEnum().WithMessage("Unknown channel.");
        RuleFor(x => x.Body).NotEmpty().WithMessage("Message body required.").MaximumLength(4000);
        RuleFor(x => x.Subject).MaximumLength(200);
        RuleFor(x => x.Subject)
            .NotEmpty().WithMessage("Subject required for Email messages.")
            .When(x => x.Channel == Pettle.Domain.Messages.MessageChannel.Email);
    }
}
