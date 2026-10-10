using Pettle.Application.Emr;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Pettle.Infrastructure.Emr;

/// <summary>The printable prescription: patient block, diagnosis, and the Treatment table (Medicines / Morning /
/// Afternoon / Night / Comments).</summary>
public static class EmrPdfRenderer
{
    private static readonly byte[]? LogoBytes = LoadLogo();

    private static byte[]? LoadLogo()
    {
        using var stream = typeof(EmrPdfRenderer).Assembly.GetManifestResourceStream("Pettle.Infrastructure.Assets.logo.png");
        if (stream is null) return null;
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

    private static string Dash(string? s) => string.IsNullOrWhiteSpace(s) ? "-" : s;

    public static byte[] Render(EmrDetail d, string tenantName)
    {
        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        if (LogoBytes is not null)
                        {
                            row.ConstantItem(48).Height(48).Image(LogoBytes).FitArea();
                            row.ConstantItem(10);
                        }
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text(tenantName).FontSize(18).Bold();
                            c.Item().Text("PRESCRIPTION").FontSize(10).FontColor(Colors.Grey.Darken1);
                        });
                        row.ConstantItem(180).Column(c => c.Item().AlignRight().Text($"Date: {d.VisitDate:dd MMM yyyy}").Bold());
                    });
                    col.Item().PaddingTop(10).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                });

                page.Content().PaddingTop(15).Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("Parent").FontSize(9).FontColor(Colors.Grey.Darken1);
                            c.Item().Text(d.ParentName).Bold();
                            c.Item().Text(Dash(d.ParentPhone));
                        });
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().AlignRight().Text($"Pet: {Dash(d.PetName)}{(string.IsNullOrWhiteSpace(d.PetBreed) ? "" : $" ({d.PetBreed})")}");
                            c.Item().AlignRight().Text($"Weight: {(d.PetWeightKg is > 0 ? $"{d.PetWeightKg:0.##} kg" : "-")}");
                            if (!string.IsNullOrWhiteSpace(d.DoctorName)) c.Item().AlignRight().Text($"Doctor: {d.DoctorName}");
                        });
                    });

                    if (!string.IsNullOrWhiteSpace(d.Complaint))
                        col.Item().PaddingTop(12).Text(t => { t.Span("Complaint: ").Bold(); t.Span(d.Complaint); });
                    if (!string.IsNullOrWhiteSpace(d.Diagnosis))
                        col.Item().PaddingTop(6).Text(t => { t.Span("Diagnosis: ").Bold(); t.Span(d.Diagnosis); });

                    col.Item().PaddingTop(14).Text("Treatment").Bold().FontSize(11);
                    col.Item().PaddingTop(4).Table(table =>
                    {
                        table.ColumnsDefinition(cd =>
                        {
                            cd.ConstantColumn(24);
                            cd.RelativeColumn(4);
                            cd.ConstantColumn(44);
                            cd.ConstantColumn(44);
                            cd.ConstantColumn(44);
                            cd.RelativeColumn(3);
                        });
                        static IContainer Head(IContainer c) => c.Background(Colors.Grey.Lighten3).Border(0.5f).BorderColor(Colors.Grey.Medium).Padding(4);
                        static IContainer Cell(IContainer c) => c.Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(4);
                        table.Header(h =>
                        {
                            h.Cell().Element(Head).Text("#").Bold();
                            h.Cell().Element(Head).Text("MEDICINES").Bold();
                            h.Cell().Element(Head).AlignCenter().Text("MOR").Bold();
                            h.Cell().Element(Head).AlignCenter().Text("AFT").Bold();
                            h.Cell().Element(Head).AlignCenter().Text("NGT").Bold();
                            h.Cell().Element(Head).Text("COMMENTS").Bold();
                        });
                        var n = 1;
                        foreach (var m in d.Medicines)
                        {
                            table.Cell().Element(Cell).Text((n++).ToString());
                            table.Cell().Element(Cell).Text(m.MedicineName);
                            table.Cell().Element(Cell).AlignCenter().Text(Dash(m.Morning));
                            table.Cell().Element(Cell).AlignCenter().Text(Dash(m.Afternoon));
                            table.Cell().Element(Cell).AlignCenter().Text(Dash(m.Night));
                            table.Cell().Element(Cell).Text(m.Comments ?? "");
                        }
                    });

                    if (!string.IsNullOrWhiteSpace(d.Advice))
                        col.Item().PaddingTop(12).Text(t => { t.Span("Advice: ").Bold(); t.Span(d.Advice); });
                    if (d.NextVisitDate.HasValue)
                        col.Item().PaddingTop(6).Text(t => { t.Span("Next visit: ").Bold(); t.Span($"{d.NextVisitDate:dd MMM yyyy}"); });
                });
            });
        });
        return doc.GeneratePdf();
    }
}
