using Pettle.Application.Invoices;
using Pettle.Domain.Invoices;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Pettle.Infrastructure.Invoices;

public static class InvoicePdfRenderer
{
    private static readonly string Rupee = "₹";

    public static byte[] Render(InvoiceDetail inv, string tenantName, string? logoUrl)
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
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text(tenantName).FontSize(18).Bold();
                            c.Item().Text(inv.InvoiceType == InvoiceType.Sale ? "TAX INVOICE" : "SERVICE INVOICE")
                                .FontSize(10).FontColor(Colors.Grey.Darken1);
                        });
                        row.ConstantItem(180).Column(c =>
                        {
                            c.Item().AlignRight().Text($"Invoice #{inv.InvoiceNumber}").Bold();
                            c.Item().AlignRight().Text($"Date: {inv.InvoiceDate:dd MMM yyyy}");
                        });
                    });
                    col.Item().PaddingTop(10).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                });

                page.Content().PaddingTop(15).Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("Billed to").FontSize(9).FontColor(Colors.Grey.Darken1);
                            c.Item().Text(inv.ParentName).Bold();
                            if (!string.IsNullOrWhiteSpace(inv.Phone)) c.Item().Text(inv.Phone);
                            if (!string.IsNullOrWhiteSpace(inv.PetNameSnapshot)) c.Item().Text($"Pet: {inv.PetNameSnapshot}");
                        });
                    });

                    col.Item().PaddingTop(15).Table(table =>
                    {
                        table.ColumnsDefinition(cd =>
                        {
                            cd.RelativeColumn(4);
                            cd.RelativeColumn(1);
                            cd.RelativeColumn(1.3f);
                            cd.RelativeColumn(1.3f);
                            cd.RelativeColumn(1.3f);
                        });

                        table.Header(h =>
                        {
                            h.Cell().Element(HeaderCell).Text("Item");
                            h.Cell().Element(HeaderCell).AlignRight().Text("Qty");
                            h.Cell().Element(HeaderCell).AlignRight().Text("Rate");
                            h.Cell().Element(HeaderCell).AlignRight().Text("Discount");
                            h.Cell().Element(HeaderCell).AlignRight().Text("Total");
                        });

                        foreach (var line in inv.Lines)
                        {
                            table.Cell().Element(BodyCell).Text(line.BillItemName);
                            table.Cell().Element(BodyCell).AlignRight().Text(line.Quantity.ToString("0.##"));
                            table.Cell().Element(BodyCell).AlignRight().Text($"{Rupee}{line.UnitAmount:0.00}");
                            table.Cell().Element(BodyCell).AlignRight().Text(line.Discount > 0 ? $"{Rupee}{line.Discount:0.00}" : "-");
                            table.Cell().Element(BodyCell).AlignRight().Text($"{Rupee}{line.Total:0.00}");
                        }

                        static IContainer HeaderCell(IContainer c) => c.DefaultTextStyle(x => x.Bold().FontColor(Colors.Grey.Darken2))
                            .PaddingVertical(6).BorderBottom(1).BorderColor(Colors.Grey.Lighten1);
                        static IContainer BodyCell(IContainer c) => c.PaddingVertical(6).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3);
                    });

                    var taxTotal = inv.IgstAmount + inv.CgstAmount + inv.SgstAmount;
                    col.Item().PaddingTop(15).AlignRight().Width(220).Column(c =>
                    {
                        TotalsRow(c, "Base amount", inv.BaseAmount);
                        if (inv.AddOnAmount > 0) TotalsRow(c, "Add-ons", inv.AddOnAmount);
                        if (inv.AdditionalAmount > 0) TotalsRow(c, "Additional charges", inv.AdditionalAmount);
                        if (inv.DiscountAmount > 0) TotalsRow(c, "Discount", -inv.DiscountAmount);
                        if (inv.CgstAmount > 0) TotalsRow(c, "CGST", inv.CgstAmount);
                        if (inv.SgstAmount > 0) TotalsRow(c, "SGST", inv.SgstAmount);
                        if (inv.IgstAmount > 0) TotalsRow(c, "IGST", inv.IgstAmount);
                        c.Item().PaddingTop(4).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                        TotalsRow(c, "Total", inv.Revenue, bold: true);
                        TotalsRow(c, "Paid", inv.Paid);
                        TotalsRow(c, "Due", inv.Due, bold: true, danger: inv.Due > 0);
                    });

                    if (!string.IsNullOrWhiteSpace(inv.Notes))
                    {
                        col.Item().PaddingTop(20).Column(c =>
                        {
                            c.Item().Text("Notes").FontSize(9).FontColor(Colors.Grey.Darken1);
                            c.Item().Text(inv.Notes);
                        });
                    }
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Thank you for choosing ").FontColor(Colors.Grey.Darken1);
                    text.Span(tenantName).FontColor(Colors.Grey.Darken1).Bold();
                });
            });
        });

        return doc.GeneratePdf();
    }

    private static void TotalsRow(ColumnDescriptor col, string label, decimal amount, bool bold = false, bool danger = false)
    {
        col.Item().Row(row =>
        {
            var labelText = row.RelativeItem().Text(label).FontColor(danger ? Colors.Red.Darken1 : Colors.Black);
            var amountText = row.ConstantItem(100).AlignRight().Text($"{Rupee}{amount:0.00}")
                .FontColor(danger ? Colors.Red.Darken1 : Colors.Black);
            if (bold)
            {
                labelText.Bold();
                amountText.Bold();
            }
        });
    }
}
