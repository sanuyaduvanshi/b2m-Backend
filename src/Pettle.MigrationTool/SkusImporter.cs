using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Pettle.Domain.Inventory;
using Pettle.Infrastructure.Persistence;
using static Pettle.MigrationTool.ImportHelpers;

namespace Pettle.MigrationTool;

/// <summary>
/// Imports the standalone SKU master export (skus_YYYY-MM-DD.xlsx, sheet "SKUs").
/// Dedupes by LegacySkuId so a re-run is safe. Auto-creates Category/Subcategory hierarchy.
/// </summary>
public class SkusImporter
{
    private readonly PettleDbContext _db;
    private readonly ILogger<SkusImporter> _log;

    public SkusImporter(PettleDbContext db, ILogger<SkusImporter> log)
    {
        _db = db;
        _log = log;
    }

    public async Task<ImportResult> ImportAsync(Guid tenantId, string xlsxPath, bool dryRun, CancellationToken ct)
    {
        var result = new ImportResult();

        var existingLegacy = (await _db.Skus.IgnoreQueryFilters()
            .Where(s => s.TenantId == tenantId && s.LegacySkuId != null)
            .Select(s => s.LegacySkuId!)
            .ToListAsync(ct))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Category cache keyed by "parent" and "parent>child" so the hierarchy is reused across rows.
        var catList = await _db.SkuCategories.Where(c => c.TenantId == tenantId).ToListAsync(ct);
        var catById = catList.ToDictionary(c => c.Id);
        var catKey = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in catList)
        {
            var parentName = c.ParentId is { } pid && catById.TryGetValue(pid, out var p) ? p.Name : null;
            catKey[CatKey(parentName, c.Name)] = c.Id;
        }

        int batched = 0;
        foreach (var row in XlsxReader.ReadSheet(xlsxPath, "SKUs"))
        {
            ct.ThrowIfCancellationRequested();
            if (row.AllEmpty()) continue;

            try
            {
                var legacy = row.GetOrNull("SKU ID");
                var name = row.GetOrNull("Name");
                if (name is null) { result.Inc("skipped_no_name"); continue; }
                if (legacy is not null && existingLegacy.Contains(legacy)) { result.Inc("skipped_existing"); continue; }

                var categoryName = row.GetOrNull("Category");
                var subName = row.GetOrNull("Subcategory");
                Guid? categoryId = ResolveCategory(tenantId, categoryName, subName, catKey, result);

                var price = ParseDecimal(row.Get("Price"));
                var cost = ParseDecimal(row.Get("Cost Price"));
                var qty = (int)Math.Round(ParseDecimal(row.Get("Quantity")));
                var barcode = row.GetOrNull("Barcode");

                _db.Skus.Add(new Sku
                {
                    TenantId = tenantId,
                    LegacySkuId = legacy,
                    Code = barcode ?? legacy ?? name,
                    Name = name,
                    CategoryId = categoryId,
                    Unit = "ea",
                    MrpPrice = price,
                    SellingPrice = price,
                    CostPrice = cost,
                    TaxPercent = ParseTaxPercent(row.Get("Taxes")),
                    HsnSacCode = row.GetOrNull("HSN Code"),
                    StockOnHand = qty < 0 ? 0 : qty,
                    IsActive = IsActiveStatus(row.Get("SKU Status")),
                });
                if (legacy is not null) existingLegacy.Add(legacy);
                result.Inc("skus_created");
                batched++;

                if (batched >= 500)
                {
                    if (!dryRun) { await _db.SaveChangesAsync(ct); }
                    batched = 0;
                }
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "SKU row {Row} failed.", row.RowNumber);
                result.Errors++;
            }
        }

        if (!dryRun && batched > 0) await _db.SaveChangesAsync(ct);
        return result;
    }

    private Guid? ResolveCategory(Guid tenantId, string? category, string? sub, Dictionary<string, Guid> cache, ImportResult result)
    {
        if (category is null && sub is null) return null;

        Guid? parentId = null;
        if (category is not null)
        {
            var pk = CatKey(null, category);
            if (!cache.TryGetValue(pk, out var pid))
            {
                var cat = new SkuCategory { TenantId = tenantId, Name = category };
                _db.SkuCategories.Add(cat);
                pid = cat.Id;
                cache[pk] = pid;
                result.Inc("categories_created");
            }
            parentId = pid;
        }

        if (sub is null) return parentId;

        var ck = CatKey(category, sub);
        if (!cache.TryGetValue(ck, out var cid))
        {
            var child = new SkuCategory { TenantId = tenantId, Name = sub, ParentId = parentId };
            _db.SkuCategories.Add(child);
            cid = child.Id;
            cache[ck] = cid;
            result.Inc("categories_created");
        }
        return cid;
    }

    private static string CatKey(string? parent, string child) => $"{parent?.Trim()?.ToLowerInvariant()}>{child.Trim().ToLowerInvariant()}";

    private static bool IsActiveStatus(string? raw)
    {
        var s = Clean(raw)?.ToLowerInvariant();
        return s is null or "active" or "enabled" or "available" or "in stock" or "true" or "1";
    }

    /// <summary>Extracts the first numeric run from a tax label like "GST 18%" / "18" / "5 %".</summary>
    private static decimal ParseTaxPercent(string? raw)
    {
        var s = Clean(raw);
        if (s is null) return 0m;
        var m = Regex.Match(s, @"\d+(\.\d+)?");
        return m.Success && decimal.TryParse(m.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : 0m;
    }
}
