using FluentValidation;
using Pettle.Application.Validation;
using Pettle.Domain.Bookings;

namespace Pettle.Application.Bookings;

public class CreateBookingValidator : AbstractValidator<CreateBookingRequest>
{
    public CreateBookingValidator()
    {
        RuleFor(x => x.PetParentId).NotEqual(Guid.Empty).WithMessage("Pet parent is required.");
        RuleFor(x => x.BookingDate)
            .NotEqual(default(DateOnly)).WithMessage("Booking date is required.")
            .Must(d => d >= DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-1) && d <= DateOnly.FromDateTime(DateTime.UtcNow).AddYears(2))
            .WithMessage("Booking date must be within ±2 years of today.");
        RuleFor(x => x.Source).IsInEnum().WithMessage("Unknown booking source.");
        RuleFor(x => x.Notes).MaximumLength(2000);
        RuleFor(x => x.AdditionalInstruction).MaximumLength(2000);
        RuleFor(x => x.GuestName).MaximumLength(160);
        RuleFor(x => x.GuestPhone).ValidPhoneFormat().When(x => !string.IsNullOrWhiteSpace(x.GuestPhone));
        RuleFor(x => x.Services).NotEmpty().WithMessage("At least one service is required.")
            .Must(s => s.Count <= 20).WithMessage("Maximum 20 services per booking.");
        RuleForEach(x => x.Services).SetValidator(new CreateBookingServiceLineValidator());
    }
}

public class CreateBookingServiceLineValidator : AbstractValidator<CreateBookingServiceLine>
{
    public CreateBookingServiceLineValidator()
    {
        RuleFor(x => x.ServiceType).IsInEnum().WithMessage("Unknown service type.");
        RuleFor(x => x.PetId).NotEqual(Guid.Empty).WithMessage("Pet is required.");
        RuleFor(x => x.ServiceName).NotEmpty().WithMessage("Service name is required.").MaximumLength(200);
        RuleFor(x => x.FinalAmount).NonNegativeAmount();
        RuleFor(x => x.Notes).MaximumLength(1000);

        // Service-type-specific date/time rules
        When(x => x.ServiceType == BookingServiceType.Boarding, () =>
        {
            RuleFor(x => x.CheckIn).NotNull().WithMessage("Check-in date required for boarding.");
            RuleFor(x => x.CheckOut).NotNull().WithMessage("Check-out date required for boarding.");
            RuleFor(x => x)
                .Must(x => !x.CheckIn.HasValue || !x.CheckOut.HasValue || x.CheckOut.Value > x.CheckIn.Value)
                .WithMessage("Check-out must be after check-in.")
                .OverridePropertyName(nameof(CreateBookingServiceLine.CheckOut));
        });

        When(x => x.ServiceType == BookingServiceType.Grooming
               || x.ServiceType == BookingServiceType.Vet
               || x.ServiceType == BookingServiceType.DayCare, () =>
        {
            RuleFor(x => x.CheckIn).NotNull().WithMessage("Date is required.");
            RuleFor(x => x)
                .Must(x => !x.StartTime.HasValue || !x.EndTime.HasValue || x.EndTime.Value > x.StartTime.Value)
                .WithMessage("End time must be after start time.")
                .OverridePropertyName(nameof(CreateBookingServiceLine.EndTime));
        });
    }
}

public class BookingStateChangeValidator : AbstractValidator<BookingStateChangeRequest>
{
    public BookingStateChangeValidator()
    {
        RuleFor(x => x.NewStatus).IsInEnum().WithMessage("Unknown status.");
        RuleFor(x => x.Reason).MaximumLength(500);
        RuleFor(x => x.Reason)
            .NotEmpty().When(x => x.NewStatus == BookingStatus.Cancelled || x.NewStatus == BookingStatus.NoShow || x.NewStatus == BookingStatus.Rejected)
            .WithMessage("A reason is required for Cancel / NoShow / Reject.");
    }

    /// <summary>
    /// Allowed transitions — caller in the service layer enforces this server-side
    /// (FluentValidation can't see the current DB state).
    /// </summary>
    public static readonly IReadOnlyDictionary<BookingStatus, BookingStatus[]> Transitions =
        new Dictionary<BookingStatus, BookingStatus[]>
        {
            [BookingStatus.Requested] = new[] { BookingStatus.Accepted, BookingStatus.Rejected, BookingStatus.Cancelled },
            [BookingStatus.Accepted] = new[] { BookingStatus.Upcoming, BookingStatus.Cancelled },
            [BookingStatus.Upcoming] = new[] { BookingStatus.CheckedIn, BookingStatus.NoShow, BookingStatus.Cancelled },
            [BookingStatus.CheckedIn] = new[] { BookingStatus.Active, BookingStatus.CheckedOut, BookingStatus.Cancelled },
            [BookingStatus.Active] = new[] { BookingStatus.CheckedOut, BookingStatus.Cancelled },
            [BookingStatus.CheckedOut] = Array.Empty<BookingStatus>(),
            [BookingStatus.NoShow] = Array.Empty<BookingStatus>(),
            [BookingStatus.Rejected] = Array.Empty<BookingStatus>(),
            [BookingStatus.Cancelled] = Array.Empty<BookingStatus>(),
        };

    public static bool IsAllowed(BookingStatus from, BookingStatus to)
        => from == to || (Transitions.TryGetValue(from, out var allowed) && allowed.Contains(to));
}
