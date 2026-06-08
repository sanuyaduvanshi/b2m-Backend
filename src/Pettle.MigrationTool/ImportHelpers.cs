using System.Globalization;
using Pettle.Domain.Bookings;
using Pettle.Domain.Clients;
using Pettle.Domain.Invoices;
using Pettle.Domain.Inventory;
using Pettle.Domain.Subscriptions;

namespace Pettle.MigrationTool;

public static class ImportHelpers
{
    private static readonly string[] DateFormats =
    {
        "MMM dd, yyyy",        // "Apr 06, 2025"
        "MMMM dd, yyyy",       // "April 06, 2025"
        "yyyy-MM-dd",
        "yyyy-MM-dd HH:mm:ss", // XlsxReader stringified datetimes
        "dd/MM/yyyy",
        "MM/dd/yyyy",
        "dd-MM-yyyy",          // invoices "13-05-2026"
    };

    private static readonly string[] DateTimeFormats =
    {
        "h:mm tt, dd MMM yyyy",     // payments "4:59 PM, 13 May 2026"
        "h:mm tt, dd MMMM yyyy",
        "yyyy-MM-dd HH:mm:ss",      // XlsxReader stringified datetimes
        "yyyy-MM-ddTHH:mm:ss.fffZ", // subscriptions "2025-09-14T13:18:21.218Z"
        "yyyy-MM-ddTHH:mm:ss",
        "yyyy-MM-dd",
        "MMM dd, yyyy",
        "dd-MM-yyyy",
    };

    private static readonly string[] TimeFormats =
    {
        "h:mm tt",          // "9:00 AM"
        "hh:mm tt",         // "09:00 AM"
        "H:mm",
        "HH:mm",
        "HH:mm:ss",
    };

    public static string? Clean(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        s = s.Trim();
        return s.Length == 0 ? null : s;
    }

    public static DateOnly? ParseDate(string? raw)
    {
        var s = Clean(raw);
        if (s is null) return null;
        if (DateTime.TryParseExact(s, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            return DateOnly.FromDateTime(dt);
        if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out dt))
            return DateOnly.FromDateTime(dt);
        return null;
    }

    public static decimal ParseDecimal(string? raw)
    {
        var s = Clean(raw);
        if (s is null) return 0m;
        // Some exports use 1,234.56 with grouping; allow both.
        s = s.Replace(",", "");
        return decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : 0m;
    }

    public static string NormalisePhone(string? raw)
    {
        var s = Clean(raw);
        if (s is null) return string.Empty;
        var leadingPlus = s.StartsWith("+");
        var digits = new string(s.Where(char.IsDigit).ToArray());
        return leadingPlus ? "+" + digits : digits;
    }

    /// <summary>
    /// Country-code-agnostic phone match key: the last 10 digits (Indian mobile length),
    /// so "919010550752" and "9010550752" reconcile to the same key when matching across export files.
    /// </summary>
    public static string PhoneKey(string? raw)
    {
        var digits = new string((Clean(raw) ?? "").Where(char.IsDigit).ToArray());
        return digits.Length > 10 ? digits[^10..] : digits;
    }

    public static bool ParseYesNo(string? raw)
    {
        var s = Clean(raw)?.ToLowerInvariant();
        return s is "yes" or "true" or "y" or "1" or "done" or "accepted";
    }

    public static PetSpecies ParseSpecies(string? raw)
    {
        var s = Clean(raw)?.ToLowerInvariant();
        return s switch
        {
            "dog" => PetSpecies.Dog,
            "cat" => PetSpecies.Cat,
            "bird" => PetSpecies.Bird,
            "rabbit" or "bunny" => PetSpecies.Rabbit,
            null => PetSpecies.Other,
            _ => PetSpecies.Other,
        };
    }

    public static PetGender? ParseGender(string? raw)
    {
        var s = Clean(raw)?.ToLowerInvariant();
        return s switch
        {
            "male" or "m" => PetGender.Male,
            "female" or "f" => PetGender.Female,
            "unknown" => PetGender.Unknown,
            _ => null,
        };
    }

    public static BreedSize? ParseBreedSize(string? raw)
    {
        var s = Clean(raw)?.ToLowerInvariant();
        return s switch
        {
            "small" or "s" => BreedSize.Small,
            "medium" or "m" => BreedSize.Medium,
            "large" or "l" => BreedSize.Large,
            "extra large" or "xl" or "extralarge" => BreedSize.ExtraLarge,
            _ => null,
        };
    }

    public static ClientStatus ParseClientStatus(string? raw)
    {
        var s = Clean(raw)?.ToLowerInvariant();
        return s switch
        {
            "archived" => ClientStatus.Archived,
            "blacklisted" or "blocked" => ClientStatus.Blacklisted,
            _ => ClientStatus.Active,
        };
    }

    public static DateTimeOffset? ParseDateTime(string? raw)
    {
        var s = Clean(raw);
        if (s is null) return null;
        if (DateTime.TryParseExact(s, DateTimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dt))
            return new DateTimeOffset(dt, TimeSpan.Zero);
        if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out dt))
            return new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc), TimeSpan.Zero);
        return null;
    }

    public static TimeOnly? ParseTime(string? raw)
    {
        var s = Clean(raw);
        if (s is null) return null;
        if (TimeOnly.TryParseExact(s, TimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var t)) return t;
        if (TimeOnly.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out t)) return t;
        // Fall back: maybe it's "Jan 08, 2025" or a full datetime — extract just the time portion.
        if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            return TimeOnly.FromDateTime(dt);
        return null;
    }

    public static decimal? TryDecimal(string? raw)
    {
        var s = Clean(raw);
        if (s is null) return null;
        s = s.Replace(",", "");
        return decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : null;
    }

    public static int? TryInt(string? raw)
    {
        var s = Clean(raw);
        if (s is null) return null;
        s = s.Replace(",", "");
        if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i)) return i;
        // Some cells are stored as numeric so come through as "1.0" / "138.0" — parse as decimal then truncate.
        if (decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var d)) return (int)d;
        return null;
    }

    public static int ParseInt(string? raw) => TryInt(raw) ?? 0;

    public static BookingStatus ParseBookingStatus(string? raw)
    {
        var s = Clean(raw)?.ToLowerInvariant();
        return s switch
        {
            "completed" or "checkedout" or "checked-out" or "checked out" => BookingStatus.CheckedOut,
            "checkedin" or "checked-in" or "checked in" or "ongoing" => BookingStatus.CheckedIn,
            "active" => BookingStatus.Active,
            "upcoming" or "scheduled" or "confirmed" => BookingStatus.Upcoming,
            "accepted" => BookingStatus.Accepted,
            "rejected" => BookingStatus.Rejected,
            "noshow" or "no show" or "no-show" => BookingStatus.NoShow,
            "cancelled" or "canceled" => BookingStatus.Cancelled,
            "requested" => BookingStatus.Requested,
            _ => BookingStatus.Upcoming,
        };
    }

    public static BookingPaymentStatus ParseBookingPaymentStatus(string? raw)
    {
        var s = Clean(raw)?.ToLowerInvariant();
        return s switch
        {
            "paid" or "fully paid" => BookingPaymentStatus.Paid,
            "partially paid" or "partial" => BookingPaymentStatus.PartiallyPaid,
            "refunded" => BookingPaymentStatus.Refunded,
            _ => BookingPaymentStatus.Pending,
        };
    }

    public static BookingSource ParseBookingSource(string? raw)
    {
        var s = Clean(raw)?.ToLowerInvariant();
        return s switch
        {
            "web" or "website" => BookingSource.Web,
            "app" or "parentapp" or "parent app" => BookingSource.ParentApp,
            "phone" => BookingSource.Phone,
            "thirdparty" or "third party" or "third-party" => BookingSource.ThirdParty,
            _ => BookingSource.WalkIn,
        };
    }

    public static BookingServiceType? ParseBookingServiceType(string? raw)
    {
        var s = Clean(raw)?.ToLowerInvariant();
        if (s is null) return null;
        if (s.Contains("boarding")) return BookingServiceType.Boarding;
        if (s.Contains("groom")) return BookingServiceType.Grooming;
        if (s.Contains("vet")) return BookingServiceType.Vet;
        if (s.Contains("day care") || s.Contains("daycare")) return BookingServiceType.DayCare;
        return null;
    }

    public static InvoiceType ParseInvoiceType(string? raw)
    {
        var s = Clean(raw)?.ToLowerInvariant();
        return s switch
        {
            "sale" => InvoiceType.Sale,
            "subscription" => InvoiceType.Subscription,
            "adjustment" => InvoiceType.Adjustment,
            "credit note" or "creditnote" or "credit-note" => InvoiceType.CreditNote,
            _ => InvoiceType.Booking,
        };
    }

    public static PaymentMode ParsePaymentMode(string? raw)
    {
        var s = Clean(raw)?.ToLowerInvariant();
        return s switch
        {
            "cash" => PaymentMode.Cash,
            "card" or "credit card" or "debit card" => PaymentMode.Card,
            "upi" or "gpay" or "googlepay" or "phonepe" or "paytm" => PaymentMode.Upi,
            "net banking" or "netbanking" or "bank transfer" or "neft" or "imps" or "rtgs" => PaymentMode.NetBanking,
            "wallet" => PaymentMode.Wallet,
            "cheque" or "check" => PaymentMode.Cheque,
            "credit" => PaymentMode.Credit,
            _ => PaymentMode.Other,
        };
    }

    public static PaymentSource ParsePaymentSource(string? raw)
    {
        var s = Clean(raw)?.ToLowerInvariant();
        return s switch
        {
            "online" => PaymentSource.Online,
            "gateway" => PaymentSource.Gateway,
            "app" => PaymentSource.App,
            _ => PaymentSource.WalkIn,
        };
    }

    public static PoStatus ParsePoStatus(string? raw)
    {
        var s = Clean(raw)?.ToLowerInvariant();
        return s switch
        {
            "received" or "completed" or "closed" => PoStatus.Received,
            "partially received" or "partial" => PoStatus.PartiallyReceived,
            "sent" or "open" => PoStatus.Sent,
            "cancelled" or "canceled" => PoStatus.Cancelled,
            _ => PoStatus.Draft,
        };
    }

    public static PoPaymentStatus ParsePoPaymentStatus(string? raw)
    {
        var s = Clean(raw)?.ToLowerInvariant();
        return s switch
        {
            "paid" => PoPaymentStatus.Paid,
            "partially paid" or "partial" => PoPaymentStatus.PartiallyPaid,
            _ => PoPaymentStatus.Unpaid,
        };
    }

    public static DiscountType ParseDiscountType(string? raw)
    {
        var s = Clean(raw)?.ToLowerInvariant();
        return s switch
        {
            "flatamount" or "flat" or "flat_amount" or "amount" => DiscountType.FlatAmount,
            _ => DiscountType.Percentage,
        };
    }

    public static InvoicePaymentStatus DerivePaymentStatus(decimal paid, decimal due)
    {
        if (paid <= 0m) return InvoicePaymentStatus.Pending;
        if (due > 0m) return InvoicePaymentStatus.PartiallyPaid;
        return InvoicePaymentStatus.Paid;
    }
}
