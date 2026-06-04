using FluentValidation;

namespace Pettle.Application.DailyTasks;

public class UpdateDailyTaskStatusValidator : AbstractValidator<UpdateDailyTaskStatusRequest>
{
    public UpdateDailyTaskStatusValidator()
    {
        RuleFor(x => x.Status).IsInEnum().WithMessage("Unknown status.");
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}
