using System.Text.RegularExpressions;

namespace Pettle.Application.Common;

public static class TextHelpers
{
    /// <summary>Turns a PascalCase enum name like "CheckedIn" into "Checked In" for user-facing messages,
    /// so status/type values read as plain English instead of raw code identifiers.</summary>
    public static string Humanize(this Enum value)
        => Regex.Replace(value.ToString(), "(?<!^)([A-Z])", " $1");
}
