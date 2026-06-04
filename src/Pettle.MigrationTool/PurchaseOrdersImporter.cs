using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Pettle.Domain.Inventory;
using Pettle.Infrastructure.Persistence;
using static Pettle.MigrationTool.ImportHelpers;

namespace Pettle.MigrationTool;

public class PurchaseOrdersImporter
{
    private readonly PettleDbContext _db;
    private readonly ILogger<PurchaseOrdersImporter> _log;

    public PurchaseOrdersImporter(PettleDbContext db, ILogger<PurchaseOrdersImporter> log)
    {
        _db = db;
        _log = log;
    }

    public async Task<ImportResult> ImportAsync(Guid tenantId, string csvPath, bool dryRun, CancellationToken ct)
    {
        var result = new ImportResult();

        using var reader = new StreamReader(csvPath);
        var rows = CsvReader.Read(reader).GetEnumerator();
        if (!rows.MoveNext()) { _log.LogWarning("PO CSV empty."); return result; }

        var headers = rows.Current;
        var idx = BuildIndex(headers);

        // Pre-load existing POs (idempotency) and vendor cache.
        var existingPoList = await _db.PurchaseOrders.IgnoreQueryFilters()
            .Where(p => p.TenantId == tenantId && p.LegacyPoNumber != null)
            .Select(p => p.LegacyPoNumber!)
            .ToListAsync(ct);
        var existingPo = new HashSet<string>(existingPoList, StringComparer.Ordinal);

        var vendorsList = await _db.Vendors.IgnoreQueryFilters()
            .Where(v => v.TenantId == tenantId)
            .Select(v => new { v.Id, v.Name })
            .ToListAsync(ct);
        var vendorByName = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        foreach (var v in vendorsList) vendorByName.TryAdd(v.Name, v.Id);

        int lineNo = 1;
        while (rows.MoveNext())
        {
            ct.ThrowIfCancellationRequested();
            lineNo++;
            var row = rows.Current;
            if (row.All(string.IsNullOrWhiteSpace)) continue;

            string Get(string col) => idx.TryGetValue(col, out var i) && i < row.Length ? row[i] : "";

            var legacyPo = Clean(Get("PO Number"));
            if (legacyPo is null) { result.Inc("skipped_no_po"); continue; }
            if (existingPo.Contains(legacyPo)) { result.Inc("skipped_existing"); continue; }

            try
            {
                var vendorName = Clean(Get("Vendor")) ?? "(unknown)";
                if (!vendorByName.TryGetValue(vendorName, out var vendorId))
                {
                    var vendor = new Vendor { TenantId = tenantId, Name = vendorName };
                    _db.Vendors.Add(vendor);
                    vendorId = vendor.Id;
                    vendorByName[vendorName] = vendorId;
                    result.Inc("vendors_created");
                }

                var purchaseDate = ParseDate(Get("Purchase Date")) ?? DateOnly.FromDateTime(DateTime.UtcNow);
                var po = new PurchaseOrder
                {
                    TenantId = tenantId,
                    LegacyPoNumber = legacyPo,
                    PoNumber = $"PO-{legacyPo}",
                    VendorId = vendorId,
                    PurchaseDate = purchaseDate,
                    Status = ParsePoStatus(Get("Status")),
                    PaymentStatus = ParsePoPaymentStatus(Get("Payment Status")),
                    VendorInvoiceNumber = Clean(Get("Vendor Invoice Number")),
                    NumberOfItems = ParseInt(Get("Number of Items")),
                    Adjustment = ParseDecimal(Get("Adjustment")),
                    Notes = Clean(Get("Notes")),
                };
                _db.PurchaseOrders.Add(po);
                existingPo.Add(legacyPo);
                result.Inc("pos_created");
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "PO line {Line} (PO #{Legacy}) failed.", lineNo, legacyPo);
                result.Errors++;
            }
        }

        if (!dryRun) await _db.SaveChangesAsync(ct);
        return result;
    }

    private static Dictionary<string, int> BuildIndex(string[] headers)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < headers.Length; i++)
        {
            var h = headers[i].Trim();
            if (h.Length > 0) map[h] = i;
        }
        return map;
    }
}
