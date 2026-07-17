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
}
