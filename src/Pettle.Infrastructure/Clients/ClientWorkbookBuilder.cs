using ClosedXML.Excel;
using Pettle.Application.Clients;
using Pettle.Domain.Clients;

namespace Pettle.Infrastructure.Clients;

/// <summary>Builds the Client Database workbook.
///
/// A CSV can only ever be one flat grid, so the totals had to sit on top of the data where Excel's
/// sort and autofilter treat them as rows. A real workbook puts them on their own sheet instead:
/// Summary reads like a report, Clients is a clean table you can filter and sort without the
/// header interfering.
/// </summary>
public static class ClientWorkbookBuilder
{
    // The brand palette, same as the printed report's.
    private static readonly XLColor Brand = XLColor.FromHtml("#4A2418");
    private static readonly XLColor BrandAlt = XLColor.FromHtml("#E88530");
    private static readonly XLColor Accent = XLColor.FromHtml("#4FA8B5");
    private static readonly XLColor Danger = XLColor.FromHtml("#C0392B");
    private static readonly XLColor Good = XLColor.FromHtml("#1D9E75");
    private static readonly XLColor Warn = XLColor.FromHtml("#BA7517");
    private static readonly XLColor Ink = XLColor.FromHtml("#241A15");
    private static readonly XLColor Muted = XLColor.FromHtml("#8A7F78");
    private static readonly XLColor Line = XLColor.FromHtml("#ECE7E1");
    private static readonly XLColor Zebra = XLColor.FromHtml("#FAF7F4");

    private const string Money = "₹#,##0.00";
    private const string MoneyWhole = "₹#,##0";

    public static byte[] Build(IReadOnlyList<PetParentListItem> rows, string tenantName, string filterLine)
    {
        using var wb = new XLWorkbook();
        BuildSummarySheet(wb, rows, tenantName, filterLine);
        BuildClientsSheet(wb, rows);
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static void BuildSummarySheet(XLWorkbook wb, IReadOnlyList<PetParentListItem> rows, string tenantName, string filterLine)
    {
        var ws = wb.AddWorksheet("Summary");
        ws.ShowGridLines = false;
        ws.Column(1).Width = 3;
        ws.Column(2).Width = 34;
        ws.Column(3).Width = 20;
        ws.Column(4).Width = 3;

        // Banner
        var banner = ws.Range("B2:C4").Merge();
        banner.Value = $"{tenantName}\nClient Database";
        banner.Style.Fill.BackgroundColor = Brand;
        banner.Style.Font.FontColor = XLColor.White;
        banner.Style.Font.Bold = true;
        banner.Style.Font.FontSize = 15;
        banner.Style.Alignment.WrapText = true;
        banner.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        banner.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
        banner.Style.Alignment.Indent = 1;
        ws.Row(2).Height = 20;
        ws.Row(3).Height = 20;

        var meta = ws.Cell("B6");
        meta.Value = filterLine;
        meta.Style.Font.FontColor = Muted;
        meta.Style.Font.FontSize = 9;
        ws.Cell("C6").Value = $"Generated {DateTime.Now:dd MMM yyyy, h:mm tt}";
        ws.Cell("C6").Style.Font.FontColor = Muted;
        ws.Cell("C6").Style.Font.FontSize = 9;
        ws.Cell("C6").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

        var active = rows.Count(r => r.Status == ClientStatus.Active);
        var archived = rows.Count(r => r.Status == ClientStatus.Archived);
        var blacklisted = rows.Count(r => r.Status == ClientStatus.Blacklisted);
        var pets = rows.Sum(r => r.PetCount);
        // Only positive balances are owed to us. Netting credits in would report a business that
        // holds advances as being owed less than it is — or, with enough credit, a negative total.
        var outstanding = rows.Sum(r => Math.Max(0, r.OutstandingBalance));
        var advance = rows.Sum(r => Math.Max(0, -r.OutstandingBalance));
        var withDues = rows.Count(r => r.OutstandingBalance > 0);
        var inCredit = rows.Count(r => r.OutstandingBalance < 0);
        var wallet = rows.Sum(r => r.WalletBalance);
        var withPets = rows.Count(r => r.PetCount > 0);
        var everBooked = rows.Count(r => r.LatestBookingDate.HasValue);

        var row = 8;
        Section(ws, ref row, "Clients");
        Stat(ws, ref row, "Total clients", rows.Count, Brand, bold: true);
        Stat(ws, ref row, "Active", active, Good);
        Stat(ws, ref row, "Archived", archived, Warn);
        Stat(ws, ref row, "Blacklisted", blacklisted, Danger);
        row++;

        Section(ws, ref row, "Pets & activity");
        Stat(ws, ref row, "Total pets", pets, Accent);
        Stat(ws, ref row, "Clients with at least one pet", withPets, Accent);
        Stat(ws, ref row, "Clients who have ever booked", everBooked, Accent);
        row++;

        Section(ws, ref row, "Money");
        Stat(ws, ref row, "Clients with dues", withDues, withDues > 0 ? Danger : Muted);
        StatMoney(ws, ref row, "Total outstanding (owed to us)", outstanding, outstanding > 0 ? Danger : Muted, bold: true);
        Stat(ws, ref row, "Clients in credit", inCredit, inCredit > 0 ? Good : Muted);
        StatMoney(ws, ref row, "Total advance held", advance, advance > 0 ? Good : Muted);
        StatMoney(ws, ref row, "Total wallet balance", wallet, Muted);

        row += 2;
        var note = ws.Cell(row, 2);
        note.Value = "Figures cover only the clients on the Clients sheet — the same rows and filters as the screen.";
        note.Style.Font.FontColor = Muted;
        note.Style.Font.FontSize = 8.5;
        note.Style.Font.Italic = true;
        ws.Range(row, 2, row, 3).Merge();

        ws.SheetView.FreezeRows(6);
    }

    private static void Section(IXLWorksheet ws, ref int row, string title)
    {
        var c = ws.Cell(row, 2);
        c.Value = title.ToUpperInvariant();
        c.Style.Font.Bold = true;
        c.Style.Font.FontSize = 9;
        c.Style.Font.FontColor = BrandAlt;
        ws.Range(row, 2, row, 3).Style.Border.BottomBorder = XLBorderStyleValues.Thin;
        ws.Range(row, 2, row, 3).Style.Border.BottomBorderColor = Line;
        row++;
    }

    private static void Stat(IXLWorksheet ws, ref int row, string label, int value, XLColor tone, bool bold = false)
        => WriteStat(ws, ref row, label, value, tone, bold, null);

    private static void StatMoney(IXLWorksheet ws, ref int row, string label, decimal value, XLColor tone, bool bold = false)
        => WriteStat(ws, ref row, label, value, tone, bold, Money);

    private static void WriteStat(IXLWorksheet ws, ref int row, string label, object value, XLColor tone, bool bold, string? format)
    {
        var l = ws.Cell(row, 2);
        l.Value = label;
        l.Style.Font.FontColor = Ink;
        l.Style.Font.FontSize = 10;

        var v = ws.Cell(row, 3);
        if (value is decimal d) v.Value = d; else v.Value = (int)value;
        if (format is not null) v.Style.NumberFormat.Format = format;
        v.Style.Font.FontColor = tone;
        v.Style.Font.Bold = true;
        v.Style.Font.FontSize = bold ? 13 : 11;
        v.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

        ws.Range(row, 2, row, 3).Style.Border.BottomBorder = XLBorderStyleValues.Hair;
        ws.Range(row, 2, row, 3).Style.Border.BottomBorderColor = Line;
        ws.Row(row).Height = bold ? 20 : 16;
        row++;
    }

    private static void BuildClientsSheet(XLWorkbook wb, IReadOnlyList<PetParentListItem> rows)
    {
        var ws = wb.AddWorksheet("Clients");

        string[] headers = { "Client", "Phone", "Email", "Location", "Pets", "Pet names", "Breeds",
                             "Outstanding", "Wallet", "Registered", "Last booking", "Status" };
        for (var i = 0; i < headers.Length; i++)
        {
            var c = ws.Cell(1, i + 1);
            c.Value = headers[i];
            c.Style.Fill.BackgroundColor = Brand;
            c.Style.Font.FontColor = XLColor.White;
            c.Style.Font.Bold = true;
            c.Style.Font.FontSize = 10;
            c.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        }
        ws.Row(1).Height = 22;

        var r = 2;
        foreach (var c in rows)
        {
            ws.Cell(r, 1).Value = c.Name;
            ws.Cell(r, 2).Value = c.Phone;
            ws.Cell(r, 3).Value = c.Email ?? "";
            ws.Cell(r, 4).Value = string.IsNullOrWhiteSpace(c.City) ? (c.AddressLine1 ?? "") : c.City;
            ws.Cell(r, 5).Value = c.PetCount;
            ws.Cell(r, 6).Value = string.Join(", ", c.PetNames ?? Array.Empty<string>());
            ws.Cell(r, 7).Value = string.Join(", ", c.PetBreeds);

            var due = ws.Cell(r, 8);
            due.Value = c.OutstandingBalance;
            due.Style.NumberFormat.Format = Money;
            // Red when owed, green when the client is in credit — the sign alone is easy to miss
            // in a column of numbers.
            if (c.OutstandingBalance > 0) { due.Style.Font.FontColor = Danger; due.Style.Font.Bold = true; }
            else if (c.OutstandingBalance < 0) due.Style.Font.FontColor = Good;

            ws.Cell(r, 9).Value = c.WalletBalance;
            ws.Cell(r, 9).Style.NumberFormat.Format = Money;

            // Written as real dates, not text, so Excel can sort and filter them by date.
            if (c.OnboardingDate is { } on)
            {
                ws.Cell(r, 10).Value = on.ToDateTime(TimeOnly.MinValue);
                ws.Cell(r, 10).Style.DateFormat.Format = "dd-MMM-yyyy";
            }
            if (c.LatestBookingDate is { } lb)
            {
                ws.Cell(r, 11).Value = lb.ToDateTime(TimeOnly.MinValue);
                ws.Cell(r, 11).Style.DateFormat.Format = "dd-MMM-yyyy";
            }

            var st = ws.Cell(r, 12);
            st.Value = c.Status.ToString();
            st.Style.Font.Bold = true;
            st.Style.Font.FontColor = c.Status switch
            {
                ClientStatus.Active => Good,
                ClientStatus.Blacklisted => Danger,
                _ => Warn,
            };

            if (r % 2 == 0) ws.Range(r, 1, r, headers.Length).Style.Fill.BackgroundColor = Zebra;
            r++;
        }

        var last = Math.Max(r - 1, 1);

        // Totals sit under the data on this sheet, where a spreadsheet reader expects them; the
        // narrative version lives on Summary.
        if (rows.Count > 0)
        {
            var t = r + 1;
            ws.Cell(t, 1).Value = $"Total — {rows.Count} client{(rows.Count == 1 ? "" : "s")}";
            ws.Cell(t, 5).Value = rows.Sum(x => x.PetCount);
            ws.Cell(t, 8).Value = rows.Sum(x => Math.Max(0, x.OutstandingBalance));
            ws.Cell(t, 8).Style.NumberFormat.Format = Money;
            ws.Cell(t, 9).Value = rows.Sum(x => x.WalletBalance);
            ws.Cell(t, 9).Style.NumberFormat.Format = Money;
            var trow = ws.Range(t, 1, t, headers.Length);
            trow.Style.Font.Bold = true;
            trow.Style.Fill.BackgroundColor = XLColor.FromHtml("#F3EDE8");
            trow.Style.Border.TopBorder = XLBorderStyleValues.Medium;
            trow.Style.Border.TopBorderColor = Brand;
        }

        ws.SheetView.FreezeRows(1);
        ws.Range(1, 1, last, headers.Length).SetAutoFilter();
        ws.Columns(1, headers.Length).AdjustToContents(1, 200, 8, 42);
        ws.Column(6).Width = Math.Min(ws.Column(6).Width, 28);
        ws.Column(7).Width = Math.Min(ws.Column(7).Width, 28);
    }

    /// <summary>Filename Excel and the browser both accept, stamped so successive downloads don't
    /// overwrite each other.</summary>
    public static string FileName() => $"clients-{DateTime.Now:yyyy-MM-dd}.xlsx";

    public const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
}
