namespace Pettle.Application.Common.Errors;

public class AppException : Exception
{
    public string Code { get; }
    public IReadOnlyDictionary<string, string[]>? FieldErrors { get; }

    public AppException(string code, string message, IReadOnlyDictionary<string, string[]>? fieldErrors = null)
        : base(message)
    {
        Code = code;
        FieldErrors = fieldErrors;
    }

    public static AppException Validation(string message, IReadOnlyDictionary<string, string[]> fields)
        => new("validation_error", message, fields);

    public static AppException NotFound(string what) => new("not_found", $"{what} not found");
    public static AppException Forbidden(string message = "Access denied") => new("forbidden", message);
    public static AppException Conflict(string message) => new("conflict", message);
    public static AppException BusinessRule(string message) => new("business_rule", message);
}
