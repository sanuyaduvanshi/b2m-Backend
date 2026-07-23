using Pettle.Application.Subscriptions;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Pettle.Infrastructure.Subscriptions;

public static class SubscriptionInvoicePdfRenderer
{
    private static readonly string Rupee = "₹";

    /// <param name="logoBytes">Downloaded from the tenant's LogoUrl by the caller — kept out of
    /// this class so rendering stays a pure, side-effect-free function of already-fetched data.</param>
    public static byte[] Render(PublicSubscriptionInvoice inv, byte[]? logoBytes)
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
                        if (logoBytes is not null)
                        {
                            row.ConstantItem(48).Height(48).Image(logoBytes).FitArea();
                            row.ConstantItem(10);
                        }
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text(inv.TenantName).FontSize(18).Bold();
                            c.Item().Text("Subscription confirmation").FontSize(10).FontColor(Colors.Grey.Darken1);
                        });
                        row.ConstantItem(160).AlignRight().Text($"Invoice date: {inv.IssuedOn:dd MMM yyyy}");
                    });
                    col.Item().PaddingTop(10).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                });

                page.Content().PaddingTop(15).Column(col =>
                {
                    col.Item().Text($"Hi {inv.ParentName},");
                    col.Item().PaddingTop(6).Text(text =>
                    {
                        text.Span("Thank you for subscribing to our services! We have successfully assigned your subscription: ");
                        text.Span(inv.PackageName).Bold();
                        text.Span(".");
                    });

                    col.Item().PaddingTop(15).Background(Colors.Grey.Lighten4).Padding(12).Column(c =>
                    {
                        if (!string.IsNullOrWhiteSpace(inv.PackageDescription))
                            c.Item().PaddingBottom(8).Text($"“{inv.PackageDescription}”").Italic().FontColor(Colors.Grey.Darken2);

                        DetailRow(c, "Invoice date", inv.IssuedOn.ToString("dd MMM yyyy"));
                        DetailRow(c, "Valid until", inv.ValidUntil.ToString("dd MMM yyyy"));
                        DetailRow(c, "Package price", $"{Rupee}{inv.Price:0.00}");
                        c.Item().PaddingTop(4).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                        DetailRow(c, "Amount paid", $"{Rupee}{inv.AmountPaid:0.00}", bold: true);
                    });

                    col.Item().PaddingTop(20).Text("We truly appreciate your trust in our services and look forward to serving you!");
                    col.Item().PaddingTop(10).Text(text =>
                    {
                        text.Span("Best regards,\n");
                        text.Span(inv.TenantName).Bold();
                    });
                });

                page.Footer().AlignCenter().Text("Thank you for choosing ").FontColor(Colors.Grey.Darken1);
            });
        });

        return doc.GeneratePdf();
    }

    private static void DetailRow(ColumnDescriptor col, string label, string value, bool bold = false)
    {
        col.Item().Row(row =>
        {
            var labelText = row.RelativeItem().Text(label).FontColor(Colors.Grey.Darken1);
            var valueText = row.ConstantItem(140).AlignRight().Text(value);
            if (bold) { labelText.Bold(); valueText.Bold(); }
        });
    }
}
