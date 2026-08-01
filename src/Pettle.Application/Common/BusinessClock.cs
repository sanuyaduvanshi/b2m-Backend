namespace Pettle.Application.Common;

/// <summary>Resolves "today" in the business's operating timezone (India Standard Time) rather
/// than UTC — the server has no inherent local timezone, and computing calendar-day boundaries
/// from raw UTC is off by one between 00:00–05:30 IST (audit H1/M4).</summary>
public static class BusinessClock
{
    private static readonly TimeZoneInfo Ist = ResolveIst();

    private static TimeZoneInfo ResolveIst()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata"); } // Linux/ICU id (prod)
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("India Standard Time"); } // Windows id (local dev)
    }

    public static DateOnly TodayIst()
        => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Ist));

    /// <summary>The true UTC instant of IST midnight starting the given business day — NOT the
    /// same as `d.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)`, which tags IST midnight as if
    /// it were already UTC midnight and is off by the full 5:30 offset. Every query that filters a
    /// DateOnly business-day range against a DateTimeOffset column (PaymentTime, Time, etc.) must
    /// go through this, or transactions within ~5:30 of midnight get attributed to the wrong day.</summary>
    public static DateTimeOffset StartOfDayUtc(DateOnly d)
        => TimeZoneInfo.ConvertTimeToUtc(d.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), Ist);

    /// <summary>The true UTC instant one tick before the next business day's IST midnight — the
    /// correct inclusive end-of-day bound for the same reason as <see cref="StartOfDayUtc"/>.</summary>
    public static DateTimeOffset EndOfDayUtc(DateOnly d)
        => StartOfDayUtc(d.AddDays(1)).AddTicks(-1);

    /// <summary>The IST calendar day a UTC instant falls on — for grouping payments/movements by
    /// "day" in reports. Grouping by `DateOnly.FromDateTime(utc.UtcDateTime)` instead buckets by
    /// the UTC calendar date, which silently reassigns anything paid before ~5:30 AM IST to the
    /// previous day's total.</summary>
    public static DateOnly ToIstDate(DateTimeOffset utc)
        => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utc.UtcDateTime, Ist));
}
