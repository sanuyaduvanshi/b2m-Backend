using ClosedXML.Excel;

namespace Pettle.MigrationTool;

/// <summary>
/// Streams rows from an .xlsx sheet as case-insensitive header→value dictionaries.
/// Cell values are returned as trimmed strings; dates/numbers are stringified via their native formatting.
/// </summary>
public static class XlsxReader
{
    public static IEnumerable<XlsxRow> ReadSheet(string filePath, string sheetName)
    {
        using var wb = new XLWorkbook(filePath);
        if (!wb.TryGetWorksheet(sheetName, out var sheet))
            throw new InvalidOperationException($"Sheet '{sheetName}' not found in {Path.GetFileName(filePath)}");

        var used = sheet.RangeUsed();
        if (used is null) yield break;

        var firstRow = used.FirstRow();
        var headers = new List<string>();
        foreach (var c in firstRow.Cells())
        {
            headers.Add(c.GetString().Trim());
        }

        int rowIdx = 1;
        foreach (var row in used.RowsUsed().Skip(1))
        {
            rowIdx++;
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            int dupCounter = 0;
            for (int i = 0; i < headers.Count; i++)
            {
                var h = headers[i];
                if (string.IsNullOrEmpty(h)) continue;
                var cell = row.Cell(i + 1);
                var val = CellToString(cell);
                // duplicate headers in source — keep first, expose dupes as "Header#2" etc.
                if (!dict.TryAdd(h, val))
                {
                    dupCounter++;
                    dict[$"{h}#{dupCounter + 1}"] = val;
                }
            }
            yield return new XlsxRow(rowIdx, dict);
        }
    }

    private static string CellToString(IXLCell cell)
    {
        if (cell.IsEmpty()) return string.Empty;
        return cell.DataType switch
        {
            XLDataType.DateTime  => cell.GetDateTime().ToString("yyyy-MM-dd HH:mm:ss"),
            XLDataType.TimeSpan  => cell.GetTimeSpan().ToString(),
            XLDataType.Number    => cell.GetDouble().ToString(System.Globalization.CultureInfo.InvariantCulture),
            XLDataType.Boolean   => cell.GetBoolean() ? "true" : "false",
            _                    => cell.GetString().Trim(),
        };
    }
}

public sealed record XlsxRow(int RowNumber, Dictionary<string, string> Cells)
{
    public string Get(string column)
        => Cells.TryGetValue(column, out var v) ? v : string.Empty;

    public string? GetOrNull(string column)
    {
        var v = Get(column);
        return string.IsNullOrWhiteSpace(v) ? null : v.Trim();
    }

    public bool AllEmpty() => Cells.Values.All(string.IsNullOrWhiteSpace);
}
