using FluentValidation;

namespace Pettle.Application.Emr;

public class EmrMedicineValidator : AbstractValidator<EmrMedicineDto>
{
    public EmrMedicineValidator()
    {
        RuleFor(x => x.MedicineName).NotEmpty().WithMessage("Medicine name is required.").MaximumLength(200);
        RuleFor(x => x.Morning).MaximumLength(60);
        RuleFor(x => x.Afternoon).MaximumLength(60);
        RuleFor(x => x.Night).MaximumLength(60);
        RuleFor(x => x.Comments).MaximumLength(500);
    }
}

public class CreateOrUpdateEmrValidator : AbstractValidator<CreateOrUpdateEmrRequest>
{
    public CreateOrUpdateEmrValidator()
    {
        RuleFor(x => x.PetId).NotEqual(Guid.Empty).WithMessage("Pick a pet.");
        RuleFor(x => x.VisitDate)
            .Must(d => d >= DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-5) && d <= DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1))
            .WithMessage("Visit date can't be in the future or more than 5 years back.");
        RuleFor(x => x.PetWeightKg).Must(w => w is >= 0.1m and <= 200m)
            .WithMessage("Pet weight must be between 0.1 and 200 kg.").When(x => x.PetWeightKg.HasValue);
        RuleFor(x => x.Complaint).MaximumLength(2000);
        RuleFor(x => x.Diagnosis).MaximumLength(2000);
        RuleFor(x => x.Advice).MaximumLength(2000);
        RuleFor(x => x.DoctorName).MaximumLength(160);
        RuleFor(x => x.NextVisitDate).Must((r, d) => d >= r.VisitDate)
            .WithMessage("Next visit can't be before the visit date.").When(x => x.NextVisitDate.HasValue);
        RuleFor(x => x.Medicines).Must(m => m is null || m.Count <= 50).WithMessage("Maximum 50 medicines per record.");
        RuleForEach(x => x.Medicines).SetValidator(new EmrMedicineValidator()).When(x => x.Medicines is not null);
    }
}
