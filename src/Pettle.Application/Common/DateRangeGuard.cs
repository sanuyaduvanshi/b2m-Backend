using Pettle.Application.Common.Errors;

namespace Pettle.Application.Common;

public static class DateRangeGuard
{
    public const int MaxDays = 366;

    public static (DateOnly From, DateOnly To) Resolve(DateOnly? from, DateOnly? to)
    {
        var t = to ?? BusinessClock.TodayIst();
        var f = from ?? t.AddDays(-30);
        if (f > t) throw AppException.Validation("Invalid date range", new Dictionary<string, string[]> { ["from"] = new[] { "From date must be before or equal to To date." } });
        var span = t.DayNumber - f.DayNumber + 1;
        if (span > MaxDays) throw AppException.Validation("Date range too large", new Dictionary<string, string[]> { ["to"] = new[] { $"Range cannot exceed {MaxDays} days." } });
        if (t > BusinessClock.TodayIst().AddDays(1))
            throw AppException.Validation("Future date not allowed", new Dictionary<string, string[]> { ["to"] = new[] { "To date cannot be in the future." } });
        return (f, t);
    }
}
