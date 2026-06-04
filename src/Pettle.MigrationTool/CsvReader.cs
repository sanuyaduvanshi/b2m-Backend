namespace Pettle.MigrationTool;

/// <summary>
/// Minimal RFC 4180 CSV reader — handles quoted fields with embedded commas and ""-escaped quotes.
/// Streams rows so we don't pull a 1300-row file into memory all at once.
/// </summary>
public static class CsvReader
{
    public static IEnumerable<string[]> Read(TextReader reader)
    {
        var field = new System.Text.StringBuilder();
        var row = new List<string>();
        bool inQuotes = false;

        while (true)
        {
            var next = reader.Read();
            if (next == -1)
            {
                if (field.Length > 0 || row.Count > 0)
                {
                    row.Add(field.ToString());
                    yield return row.ToArray();
                }
                yield break;
            }

            var c = (char)next;

            if (inQuotes)
            {
                if (c == '"')
                {
                    // Lookahead for escaped quote
                    if (reader.Peek() == '"') { field.Append('"'); reader.Read(); }
                    else inQuotes = false;
                }
                else field.Append(c);
            }
            else
            {
                switch (c)
                {
                    case '"' when field.Length == 0:
                        inQuotes = true; break;
                    case ',':
                        row.Add(field.ToString()); field.Clear(); break;
                    case '\r':
                        // Swallow; assume CRLF or bare LF
                        break;
                    case '\n':
                        row.Add(field.ToString()); field.Clear();
                        yield return row.ToArray();
                        row = new List<string>();
                        break;
                    default:
                        field.Append(c); break;
                }
            }
        }
    }
}
