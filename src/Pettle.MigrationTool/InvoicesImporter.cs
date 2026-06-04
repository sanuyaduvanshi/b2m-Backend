using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Pettle.Domain.Invoices;
using Pettle.Infrastructure.Persistence;
using static Pettle.MigrationTool.ImportHelpers;

namespace Pettle.MigrationTool;

public class InvoicesImporter
{
    private readonly PettleDbContext _db;
    private readonly ILogger<InvoicesImporter> _log;

    // Source has tax slabs split across multiple columns; we sum them per type.
    // Trailing-space duplicates ("I GST ") get exposed as "<name>#2", "<name>#3" by XlsxReader.
    private static readonly string[] IgstColumns = {
        "I GST - 18%", "I GST", "I GST#2", "I GST#3",
    };
    private static readonly string[] SgstColumns = { "SGST 6%", "SGST 9%", "SGST 2.5" };
    private static readonly string[] CgstColumns = { "CGST 6%", "CGST 9%", "CGST 2.5" };

    public InvoicesImporter(PettleDbContext db, ILogger<InvoicesImporter> log)
    {
        _db = db;
        _log = log;
    }

    public async Task<ImportResult> ImportAsync(Guid tenantId, string xlsxPath, bool dryRun, CancellationToken ct)
    {
        var result = new ImportResult();

        // ----- caches -----
        var existingInvoices = (await _db.Invoices.IgnoreQueryFilters()
            .Where(i => i.TenantId == tenantId && i.LegacyInvoiceNo != null)
            .Select(i => new { i.Id, i.LegacyInvoiceNo })
            .ToListAsync(ct))
            .ToDictionary(x => x.LegacyInvoiceNo!, x => x.Id, StringComparer.Ordinal);

        var parentsByPhone = (await _db.PetParents.IgnoreQueryFilters()
            .Where(p => p.TenantId == tenantId && p.Phone != null)
            .Select(p => new { p.Id, p.Phone })
            .ToListAsync(ct))
            .GroupBy(p => p.Phone!, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.Ordinal);

        var bookingsByLegacy = (await _db.Bookings.IgnoreQueryFilters()
            .Where(b => b.TenantId == tenantId && b.LegacyBookingId != null)
            .Select(b => new { b.Id, b.LegacyBookingId })
            .ToListAsync(ct))
            .ToDictionary(x => x.LegacyBookingId!, x => x.Id, StringComparer.Ordinal);

        _log.LogInformation("Invoices cache: {Existing} existing invoices, {Parents} parents, {Bookings} bookings.",
            existingInvoices.Count, parentsByPhone.Count, bookingsByLegacy.Count);

        // ===== 1) Header sheet =====
        var newInvoices = new Dictionary<string, Invoice>(StringComparer.Ordinal);

        foreach (var row in XlsxReader.ReadSheet(xlsxPath, "Invoices"))
        {
            ct.ThrowIfCancellationRequested();
            if (row.AllEmpty()) continue;

            var invoiceNo = row.GetOrNull("Invoice No");
            if (invoiceNo is null) { result.Inc("skipped_no_invoice_no"); continue; }
            if (existingInvoices.ContainsKey(invoiceNo)) { result.Inc("skipped_existing"); continue; }

            try
            {
                var phone = NormalisePhone(row.Get("Phone Number"));
                Guid? parentId = null;
                if (!string.IsNullOrEmpty(phone) && parentsByPhone.TryGetValue(phone, out var pid))
                    parentId = pid;

                Guid? bookingId = null;
                var legacyBookingId = row.GetOrNull("Booking ID");
                if (legacyBookingId is not null && bookingsByLegacy.TryGetValue(legacyBookingId, out var bid))
                    bookingId = bid;

                var paid = ParseDecimal(row.Get("Paid"));
                var due = ParseDecimal(row.Get("Due"));

                var invoice = new Invoice
                {
                    TenantId = tenantId,
                    LegacyInvoiceNo = invoiceNo,
                    InvoiceNumber = invoiceNo,
                    InvoiceType = ParseInvoiceType(row.Get("Invoice Type")),
                    InvoiceDate = ParseDate(row.Get("Invoice Date")) ?? DateOnly.FromDateTime(DateTime.UtcNow),
                    BookingId = bookingId,
                    PetParentId = parentId,
                    ParentNameSnapshot = row.GetOrNull("Parent") ?? "(unknown)",
                    PhoneSnapshot = phone,
                    PetNameSnapshot = row.GetOrNull("Pet"),

                    BaseAmount = ParseDecimal(row.Get("Base Amount")),
                    AddOnAmount = ParseDecimal(row.Get("Add-On Amount")),
                    DiscountAmount = ParseDecimal(row.Get("Discount Amount")),
                    AdditionalAmount = ParseDecimal(row.Get("Additional Amount")),
                    AdditionalDiscountAmount = ParseDecimal(row.Get("Additional Discount Amount")),
                    IgstAmount = SumColumns(row, IgstColumns),
                    SgstAmount = SumColumns(row, SgstColumns),
                    CgstAmount = SumColumns(row, CgstColumns),
                    Revenue = ParseDecimal(row.Get("Revenue")),
                    Paid = paid,
                    Due = due,
                    PaymentStatus = DerivePaymentStatus(paid, due),
                };
                _db.Invoices.Add(invoice);
                newInvoices[invoiceNo] = invoice;
                existingInvoices[invoiceNo] = invoice.Id;
                result.Inc("invoices_created");

                if (newInvoices.Count % 500 == 0)
                {
                    if (!dryRun) { await _db.SaveChangesAsync(ct); _db.ChangeTracker.Clear(); newInvoices.Clear(); }
                    _log.LogInformation("Progress: {Count} invoices created.", existingInvoices.Count);
                }
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Invoice row {Row} ({No}) failed.", row.RowNumber, invoiceNo);
                result.Errors++;
            }
        }

        if (!dryRun) await _db.SaveChangesAsync(ct);

        // ===== 2) Breakdown (line items) =====
        // Resolve each line's parent invoice via the existingInvoices map (which now spans both
        // pre-loaded and newly-created invoices). We re-load id-only to avoid keeping huge tracked graphs.
        _db.ChangeTracker.Clear();
        existingInvoices = (await _db.Invoices.IgnoreQueryFilters()
            .Where(i => i.TenantId == tenantId && i.LegacyInvoiceNo != null)
            .Select(i => new { i.Id, i.LegacyInvoiceNo })
            .ToListAsync(ct))
            .ToDictionary(x => x.LegacyInvoiceNo!, x => x.Id, StringComparer.Ordinal);

        // Avoid duplicating line items if breakdown is re-imported — count existing lines per invoice.
        var existingLineCounts = (await _db.InvoiceLineItems.AsNoTracking()
            .Where(l => l.TenantId == tenantId)
            .GroupBy(l => l.InvoiceId)
            .Select(g => new { InvoiceId = g.Key, Count = g.Count() })
            .ToListAsync(ct))
            .ToDictionary(x => x.InvoiceId, x => x.Count);

        int batched = 0;
        foreach (var row in XlsxReader.ReadSheet(xlsxPath, "Breakdown"))
        {
            ct.ThrowIfCancellationRequested();
            if (row.AllEmpty()) continue;
            var invoiceNo = row.GetOrNull("Invoice No");
            if (invoiceNo is null) { result.Inc("breakdown_skipped_no_invoice_no"); continue; }
            if (!existingInvoices.TryGetValue(invoiceNo, out var invoiceId))
            {
                result.Inc("breakdown_skipped_unknown_invoice");
                continue;
            }
            // If this invoice already has line items from a prior run, skip — first import wins.
            if (existingLineCounts.TryGetValue(invoiceId, out var existingCount) && existingCount > 0)
            {
                result.Inc("breakdown_skipped_already_imported");
                continue;
            }

            try
            {
                _db.InvoiceLineItems.Add(new InvoiceLineItem
                {
                    TenantId = tenantId,
                    InvoiceId = invoiceId,
                    BillItemName = row.GetOrNull("Bill Item Name") ?? row.GetOrNull("SKU") ?? row.GetOrNull("Service") ?? "Item",
                    BillSection = row.GetOrNull("Bill Section"),
                    ServiceName = row.GetOrNull("Service"),
                    SkuName = row.GetOrNull("SKU"),
                    SkuLegacyId = row.GetOrNull("SKU ID"),
                    Category = row.GetOrNull("Category"),
                    SubCategory = row.GetOrNull("Sub Category"),
                    HsnSacCode = row.GetOrNull("HSN/SAC Code"),
                    Quantity = TryDecimal(row.Get("Quantity")) ?? 1m,
                    UnitAmount = ParseDecimal(row.Get("Amount")),
                    Discount = ParseDecimal(row.Get("Discount")),
                    Subtotal = ParseDecimal(row.Get("Subtotal")),
                    IgstAmount = SumColumns(row, IgstColumns),
                    SgstAmount = SumColumns(row, SgstColumns),
                    CgstAmount = SumColumns(row, CgstColumns),
                    Total = ParseDecimal(row.Get("Total")),
                    IsReturn = ParseYesNo(row.Get("Is Return")),
                    BreedSnapshot = row.GetOrNull("Breed"),
                    BreedSizeSnapshot = row.GetOrNull("Breed Size"),
                    CoatLengthSnapshot = row.GetOrNull("Coat Length"),
                    StaffName = row.GetOrNull("Staff Name"),
                });
                result.Inc("line_items_created");
                batched++;

                if (batched >= 1000)
                {
                    if (!dryRun) { await _db.SaveChangesAsync(ct); _db.ChangeTracker.Clear(); }
                    batched = 0;
                }
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Line-item row {Row} (Inv {No}) failed.", row.RowNumber, invoiceNo);
                result.Errors++;
            }
        }

        if (!dryRun && batched > 0) await _db.SaveChangesAsync(ct);
        return result;
    }

    private static decimal SumColumns(XlsxRow row, string[] columns)
    {
        decimal sum = 0m;
        foreach (var c in columns)
        {
            var v = row.GetOrNull(c);
            if (v is null) continue;
            sum += ParseDecimal(v);
        }
        return sum;
    }
}
