using FluentValidation;

namespace Pettle.Application.Validation;

/// <summary>
/// Reusable validation patterns/extensions for common formats.
/// Apply <c>.When(...)</c> at the call site for optional fields.
/// </summary>
public static class CommonRules
{
    // +91XXXXXXXXXX, or +<1-3 digit country><7-14 digits>, or bare 7-14 digits (legacy data)
    public const string PhonePattern = @"^\+?\d{7,16}$";

    public const string HexColorPattern = @"^#?([0-9a-fA-F]{3}|[0-9a-fA-F]{6})$";

    // India GSTIN: 2-digit state + 10-char PAN + entity + Z + check
    public const string GstinPattern = @"^[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z][1-9A-Z]Z[0-9A-Z]$";

    public const string IndianPostalPattern = @"^\d{6}$";

    public static IRuleBuilderOptions<T, string?> ValidPhoneFormat<T>(this IRuleBuilder<T, string?> rb)
        => rb.Matches(PhonePattern).WithMessage("Enter a valid phone number (digits, optional + and country code).");

    public static IRuleBuilderOptions<T, string?> ValidEmailFormat<T>(this IRuleBuilder<T, string?> rb)
        => rb.EmailAddress().WithMessage("Enter a valid email.");

    public static IRuleBuilderOptions<T, string?> ValidHexColorFormat<T>(this IRuleBuilder<T, string?> rb)
        => rb.Matches(HexColorPattern).WithMessage("Use a hex colour like #4A2418.");

    public static IRuleBuilderOptions<T, string?> ValidGstinFormat<T>(this IRuleBuilder<T, string?> rb)
        => rb.Matches(GstinPattern).WithMessage("Enter a valid 15-character GSTIN.");

    public static IRuleBuilderOptions<T, string?> ValidIndianPostalFormat<T>(this IRuleBuilder<T, string?> rb)
        => rb.Matches(IndianPostalPattern).WithMessage("PIN code must be 6 digits.");

    public static IRuleBuilderOptions<T, decimal> NonNegativeAmount<T>(this IRuleBuilder<T, decimal> rb)
        => rb.GreaterThanOrEqualTo(0).WithMessage("Amount cannot be negative.");

    public static IRuleBuilderOptions<T, decimal> PositiveAmount<T>(this IRuleBuilder<T, decimal> rb)
        => rb.GreaterThan(0).WithMessage("Amount must be greater than zero.");

    public static IRuleBuilderOptions<T, decimal> ValidTaxPercent<T>(this IRuleBuilder<T, decimal> rb)
        => rb.InclusiveBetween(0, 100).WithMessage("Tax percent must be between 0 and 100.");

    public static IRuleBuilderOptions<T, decimal?> ValidOptionalTaxPercent<T>(this IRuleBuilder<T, decimal?> rb)
        => rb.InclusiveBetween(0, 100).WithMessage("Tax percent must be between 0 and 100.");
}
