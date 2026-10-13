using ClosedXML.Excel;
using Pettle.Application.Inventory;

namespace Pettle.Infrastructure.Inventory;

/// <summary>Batch-wise SKU (Medicine) report as a real .xlsx: purchase price read batch by batch (ex-GST,
/// "140 + 100"), Closing value = sum of qty × that batch's own price. Mirrors the on-screen CSV/PDF logic.</summary>
public static class SkuBatchReportWorkbook
{
    public const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    public static string FileName() => $"b2m-sku-report-{DateTime.Now:yyyy-MM-dd}.xlsx";

    public static byte[] Build(IReadOnlyList<SkuBatchReportRow> rows, string tenantName)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("SKU Report");
        ws.Cell(1, 1).Value = $"{tenantName} — SKU / Medicine report (batch-wise, ex-GST)";
        ws.Cell(1, 1).Style.Font.Bold = true; ws.Cell(1, 1).Style.Font.FontSize = 13;
        ws.Cell(2, 1).Value = $"Generated {DateTime.Now:dd-MM-yyyy HH:mm}";
        ws.Cell(2, 1).Style.Font.FontColor = XLColor.FromHtml("#8A7F78");

        const int head = 4;
        var headers = new[] { "S.No", "Product name", "Item code", "Purchase price (batch-wise)", "Qty", "Purchase tax", "Closing value (ex-GST)" };
        for (var c = 0; c < headers.Length; c++) ws.Cell(head, c + 1).Value = headers[c];
        var hr = ws.Range(head, 1, head, headers.Length);
        hr.Style.Font.Bold = true; hr.Style.Font.FontColor = XLColor.White;
        hr.Style.Fill.BackgroundColor = XLColor.FromHtml("#4A2418");

        var row = head;
        foreach (var (r, i) in rows.Select((r, i) => (r, i)))
        {
            var parts = r.Batches.Select(b => (b.Qty, Price: b.PurchasePrice, Tax: b.TaxPercent)).ToList();
            var inBatches = parts.Sum(p => p.Qty);
            if (r.StockOnHand > inBatches) parts.Add((r.StockOnHand - inBatches, r.MasterCost, r.TaxPercent));
            if (parts.Count == 0) parts.Add((0, r.MasterCost, r.TaxPercent));

            var prices = parts.GroupBy(p => Math.Round(p.Price, 2)).Select(g => g.Key).ToList();
            var taxes = parts.Select(p => p.Tax).Distinct().ToList();
            row++;
            ws.Cell(row, 1).Value = i + 1;
            ws.Cell(row, 2).Value = r.Name;
            ws.Cell(row, 3).Value = r.Code;
            ws.Cell(row, 4).Value = string.Join(" + ", prices.Select(p => p.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)));
            ws.Cell(row, 5).Value = parts.Sum(p => p.Qty);
            ws.Cell(row, 6).Value = string.Join(" / ", taxes.Select(t => $"{t:0.##}%"));
            ws.Cell(row, 7).Value = Math.Round(parts.Sum(p => p.Qty * p.Price), 2);
            ws.Cell(row, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            ws.Cell(row, 7).Style.NumberFormat.Format = "#,##0.00";
        }
        var last = row;
        row++;
        ws.Cell(row, 4).Value = "TOTAL";
        if (last > head)
        {
            ws.Cell(row, 5).FormulaA1 = $"SUM(E{head + 1}:E{last})";
            ws.Cell(row, 7).FormulaA1 = $"SUM(G{head + 1}:G{last})";
        }
        ws.Cell(row, 7).Style.NumberFormat.Format = "#,##0.00";
        var tr = ws.Range(row, 1, row, headers.Length);
        tr.Style.Font.Bold = true; tr.Style.Border.TopBorder = XLBorderStyleValues.Thin;
        ws.Cell(row, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

        ws.Column(1).Width = 7; ws.Column(2).Width = 38; ws.Column(3).Width = 16;
        ws.Column(4).Width = 30; ws.Column(5).Width = 9; ws.Column(6).Width = 14; ws.Column(7).Width = 22;
        ws.SheetView.FreezeRows(head);
        if (last > head) ws.Range(head, 1, last, headers.Length).SetAutoFilter();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }
}
