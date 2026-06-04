using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Pettle.Domain.Invoices;
using Pettle.Infrastructure.Persistence;
using static Pettle.MigrationTool.ImportHelpers;

namespace Pettle.MigrationTool;

public class PaymentsImporter
{
    private readonly PettleDbContext _db;
    private readonly ILogger<PaymentsImporter> _log;

    public PaymentsImporter(PettleDbContext db, ILogger<PaymentsImporter> log)
    {
        _db = db;
        _log = log;
    }

    public async Task<ImportResult> ImportAsync(Guid tenantId, string xlsxPath, bool dryRun, CancellationToken ct)
    {
        var result = new ImportResult();

        var invoicesByLegacy = (await _db.Invoices.IgnoreQueryFilters()
            .Where(i => i.TenantId == tenantId && i.LegacyInvoiceNo != null)
            .Select(i => new { i.Id, i.LegacyInvoiceNo })
            .ToListAsync(ct))
            .ToDictionary(x => x.LegacyInvoiceNo!, x => x.Id, StringComparer.Ordinal);

        // Dedupe by (invoiceId, time, amount, mode) signature — no LegacyPaymentId on the source.
        var existingKeys = (await _db.Payments.AsNoTracking()
            .Where(p => p.TenantId == tenantId)
            .Select(p => new { p.InvoiceId, p.PaymentTime, p.Amount, p.Mode })
            .ToListAsync(ct))
            .Select(x => MakeKey(x.InvoiceId, x.PaymentTime, x.Amount, x.Mode))
            .ToHashSet(StringComparer.Ordinal);

        _log.LogInformation("Payments cache: {Invoices} invoices, {Existing} existing payments.",
            invoicesByLegacy.Count, existingKeys.Count);

        int batched = 0;
        foreach (var row in XlsxReader.ReadSheet(xlsxPath, "Payments"))
        {
            ct.ThrowIfCancellationRequested();
            if (row.AllEmpty()) continue;

            var legacyInvoiceNo = row.GetOrNull("Invoice ID");
            if (legacyInvoiceNo is null) { result.Inc("skipped_no_invoice_id"); continue; }
            if (!invoicesByLegacy.TryGetValue(legacyInvoiceNo, out var invoiceId))
            {
                result.Inc("skipped_unknown_invoice");
                continue;
            }

            try
            {
                var time = ParseDateTime(row.Get("Time")) ?? DateTimeOffset.UtcNow;
                var amount = ParseDecimal(row.Get("Amount"));
                var mode = ParsePaymentMode(row.Get("Mode"));
                var key = MakeKey(invoiceId, time, amount, mode);
                if (existingKeys.Contains(key)) { result.Inc("skipped_existing"); continue; }

                _db.Payments.Add(new Payment
                {
                    TenantId = tenantId,
                    InvoiceId = invoiceId,
                    PaymentTime = time,
                    Amount = amount,
                    Mode = mode,
                    Source = ParsePaymentSource(row.Get("Source")),
                    TransactionId = row.GetOrNull("Transaction ID"),
                });
                existingKeys.Add(key);
                result.Inc("payments_created");
                batched++;

                if (batched >= 1000)
                {
                    if (!dryRun) { await _db.SaveChangesAsync(ct); _db.ChangeTracker.Clear(); }
                    batched = 0;
                }
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Payment row {Row} (Inv {Inv}) failed.", row.RowNumber, legacyInvoiceNo);
                result.Errors++;
            }
        }

        if (!dryRun && batched > 0) await _db.SaveChangesAsync(ct);
        return result;
    }

    private static string MakeKey(Guid invoiceId, DateTimeOffset time, decimal amount, PaymentMode mode)
        => $"{invoiceId}|{time.UtcDateTime.Ticks}|{amount}|{(int)mode}";
}
