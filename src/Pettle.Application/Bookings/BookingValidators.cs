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
        RuleFor(x => x.PetWeightKg).Must(w => w is >= 0.1m and <= 200m).WithMessage("Pet weight must be between 0.1 and 200 kg.")
            .When(x => x.PetWeightKg.HasValue);
        RuleForEach(x => x.AddOns).SetValidator(new CreateBookingAddOnLineValidator()).When(x => x.AddOns is not null);
        RuleForEach(x => x.InventoryItems).SetValidator(new CreateBookingInventoryItemLineValidator()).When(x => x.InventoryItems is not null);
    }
}

public class CreateBookingAddOnLineValidator : AbstractValidator<CreateBookingAddOnLine>
{
    public CreateBookingAddOnLineValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Add-on name required.").MaximumLength(200);
        RuleFor(x => x.Price).NonNegativeAmount();
        RuleFor(x => x.Count).GreaterThan(0).WithMessage("Quantity must be at least 1.");
        RuleFor(x => x.Discount).GreaterThanOrEqualTo(0).WithMessage("Discount can't be negative.");
    }
}

public class CreateBookingInventoryItemLineValidator : AbstractValidator<CreateBookingInventoryItemLine>
{
    public CreateBookingInventoryItemLineValidator()
    {
        RuleFor(x => x.SkuId).NotEqual(Guid.Empty).WithMessage("Item is required.");
        RuleFor(x => x.SkuName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Quantity).GreaterThan(0).WithMessage("Quantity must be at least 1.");
        RuleFor(x => x.FinalAmount).NonNegativeAmount();
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
            // Catches a mistyped/garbled check-out year (e.g. a date input segment fat-fingered)
            // slipping past the "after check-in" check and inflating FinalAmount (nights x rate,
            // computed client-side and trusted here) into an absurd number.
            RuleFor(x => x)
                .Must(x => !x.CheckIn.HasValue || !x.CheckOut.HasValue || x.CheckOut.Value.DayNumber - x.CheckIn.Value.DayNumber <= 180)
                .WithMessage("Stay is longer than 180 nights — check the dates.")
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
