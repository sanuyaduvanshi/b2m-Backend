using System.Text.RegularExpressions;

namespace Pettle.Application.Messages;

/// <summary>
/// Extracts and substitutes {{placeholder}} variables in message-template bodies.
/// Variable names are normalised to snake_case lower (e.g. {{ Pet Name }} → "pet_name").
/// </summary>
public static class TemplateVariables
{
    private static readonly Regex PlaceholderRegex = new(@"\{\{\s*([a-zA-Z][a-zA-Z0-9_ \-]*?)\s*\}\}", RegexOptions.Compiled);

    public static IReadOnlyList<string> Extract(string? body)
    {
        if (string.IsNullOrWhiteSpace(body)) return Array.Empty<string>();
        var found = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match m in PlaceholderRegex.Matches(body))
        {
            var name = Normalise(m.Groups[1].Value);
            if (seen.Add(name)) found.Add(name);
        }
        return found;
    }

    public static string Substitute(string body, IReadOnlyDictionary<string, string>? values)
    {
        if (string.IsNullOrEmpty(body) || values is null || values.Count == 0) return body;
        // Build a case-insensitive lookup keyed on the normalised form.
        var lookup = values.ToDictionary(kv => Normalise(kv.Key), kv => kv.Value);
        return PlaceholderRegex.Replace(body, m =>
        {
            var key = Normalise(m.Groups[1].Value);
            return lookup.TryGetValue(key, out var v) ? v : m.Value;
        });
    }

    private static string Normalise(string raw)
        => raw.Trim().Replace(' ', '_').Replace('-', '_').ToLowerInvariant();
}
