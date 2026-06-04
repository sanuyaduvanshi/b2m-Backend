using FluentValidation;

namespace Pettle.Application.Calendar;

public class RescheduleValidator : AbstractValidator<RescheduleRequest>
{
    public RescheduleValidator()
    {
        RuleFor(x => x.NewStartDate).NotEqual(default(DateOnly)).WithMessage("New date required.");

        RuleFor(x => x.NewEndDate)
            .GreaterThanOrEqualTo(x => x.NewStartDate)
            .When(x => x.NewEndDate.HasValue)
            .WithMessage("End date must be on or after start date.");

        RuleFor(x => x.NewEndTime)
            .GreaterThan(x => x.NewStartTime)
            .When(x => x.NewStartTime.HasValue && x.NewEndTime.HasValue)
            .WithMessage("End time must be after start time.");
    }
}
