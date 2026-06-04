using FluentValidation;

namespace Pettle.Application.Reminders;

public class CreateReminderValidator : AbstractValidator<CreateReminderRequest>
{
    public CreateReminderValidator()
    {
        RuleFor(x => x.Type).IsInEnum().WithMessage("Unknown reminder type.");
        RuleFor(x => x.Title).NotEmpty().WithMessage("Title is required.").MaximumLength(200);
        RuleFor(x => x.Message).MaximumLength(2000);
        RuleFor(x => x.DueDate)
            .Must(d => d >= DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-7))
            .WithMessage("Due date cannot be more than 7 days in the past.");
    }
}

public class SnoozeRequestValidator : AbstractValidator<SnoozeRequest>
{
    public SnoozeRequestValidator()
    {
        RuleFor(x => x.Until)
            .GreaterThan(DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("Snooze date must be in the future.")
            .LessThanOrEqualTo(DateOnly.FromDateTime(DateTime.UtcNow).AddYears(1))
            .WithMessage("Snooze date cannot be more than 1 year out.");
    }
}
