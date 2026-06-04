namespace Pettle.MigrationTool;

/// <summary>
/// Flexible counters for non-clients importers — each importer tags up its own keys.
/// </summary>
public class ImportResult
{
    public Dictionary<string, int> Counts { get; } = new();
    public int Errors { get; set; }

    public void Inc(string key, int by = 1)
        => Counts[key] = Counts.GetValueOrDefault(key) + by;

    public override string ToString()
    {
        var parts = Counts.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}={kv.Value}");
        return string.Join(" ", parts) + (Errors > 0 ? $" errors={Errors}" : " errors=0");
    }
}
