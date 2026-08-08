using Pettle.Application.Invoices;
using Pettle.Domain.Invoices;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Pettle.Infrastructure.Invoices;

public static class InvoicePdfRenderer
{
    private static readonly string Rupee = "₹";

    private static readonly byte[]? LogoBytes = LoadLogo();

    private static byte[]? LoadLogo()
    {
        var assembly = typeof(InvoicePdfRenderer).Assembly;
        using var stream = assembly.GetManifestResourceStream("Pettle.Infrastructure.Assets.logo.png");
        if (stream is null) return null;
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

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
                        if (LogoBytes is not null)
                        {
                            row.ConstantItem(48).Height(48).Image(LogoBytes).FitArea();
                            row.ConstantItem(10);
                        }
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

                    // A booking settled by the customer's package showed only "Paid ₹2,149" — from
                    // the bill alone there was no way to tell the money came off their plan rather
                    // than out of their pocket. Split it out so the deduction is visible.
                    var subPayments = inv.Payments
                        .Where(p => !string.IsNullOrWhiteSpace(p.SubscriptionPackageName)
                                    && p.Status == PaymentRecordStatus.Success)
                        .ToList();
                    var subCovered = subPayments.Sum(p => p.Amount);
                    var subName = subPayments.Select(p => p.SubscriptionPackageName).FirstOrDefault();
                    var paidOtherwise = inv.Paid - subCovered;

                    var taxTotal = inv.IgstAmount + inv.CgstAmount + inv.SgstAmount;
                    col.Item().PaddingTop(15).AlignRight().Width(260).Column(c =>
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
                        if (subCovered > 0)
                        {
                            // Shown as a minus so it reads as money coming off the bill, which is
                            // what actually happened to the customer's balance.
                            TotalsRow(c, "Less: paid by subscription", -subCovered, color: Colors.Green.Darken2);
                            if (paidOtherwise > 0.01m) TotalsRow(c, "Paid now", paidOtherwise);
                        }
                        else
                        {
                            TotalsRow(c, "Paid", inv.Paid);
                        }
                        TotalsRow(c, "Due", inv.Due, bold: true, danger: inv.Due > 0);
                    });

                    // Spelled out as its own panel, the way a statement shows what a deduction left
                    // behind: the customer's next question after "it came off my package" is
                    // always "so what's left on it".
                    if (inv.Subscription is { } plan)
                    {
                        col.Item().PaddingTop(14).Background(Colors.Green.Lighten5)
                            .Border(1).BorderColor(Colors.Green.Lighten3).Padding(12).Column(c =>
                            {
                                c.Item().Text("Paid from your subscription")
                                    .Bold().FontColor(Colors.Green.Darken3);
                                c.Item().PaddingTop(1).Text(plan.PetName is null
                                        ? plan.PackageName
                                        : $"{plan.PackageName} · for {plan.PetName}")
                                    .FontSize(11).FontColor(Colors.Grey.Darken3);
                                c.Item().PaddingTop(8).Row(r =>
                                {
                                    PlanFact(r, "Deducted from plan", $"{Rupee}{plan.CoveredAmount:0.00}");
                                    PlanFact(r, "Sessions left",
                                        plan.TotalSessions > 0 ? $"{plan.RemainingSessions} of {plan.TotalSessions}" : "—");
                                    PlanFact(r, "Valid till", plan.ValidUntil.ToString("dd MMM yyyy"));
                                });
                                c.Item().PaddingTop(8).Text(
                                    $"No cash was collected for {Rupee}{plan.CoveredAmount:0.00} — it came off your package balance.")
                                    .FontSize(9).FontColor(Colors.Green.Darken1);
                            });
                    }

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

    /// <summary>One labelled figure inside the subscription panel.</summary>
    private static void PlanFact(RowDescriptor row, string label, string value)
    {
        row.RelativeItem().Column(c =>
        {
            c.Item().Text(label).FontSize(8).FontColor(Colors.Grey.Darken1);
            c.Item().Text(value).FontSize(11).Bold().FontColor(Colors.Grey.Darken4);
        });
    }

    private static void TotalsRow(ColumnDescriptor col, string label, decimal amount,
        bool bold = false, bool danger = false, Color? color = null)
    {
        Color tone = danger ? Colors.Red.Darken1 : (color ?? Colors.Black);
        col.Item().Row(row =>
        {
            var labelText = row.RelativeItem().Text(label).FontColor(tone);
            var text = amount < 0 ? $"-{Rupee}{-amount:0.00}" : $"{Rupee}{amount:0.00}";
            var amountText = row.ConstantItem(100).AlignRight().Text(text).FontColor(tone);
            if (bold)
            {
                labelText.Bold();
                amountText.Bold();
            }
        });
    }
}
