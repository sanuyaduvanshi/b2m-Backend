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

    public async Task<ImportResult> ImportAsync(Guid tenantId, string filePath, bool dryRun, CancellationToken ct)
    {
        var result = new ImportResult();

        var existingLegacy = (await _db.Skus.IgnoreQueryFilters()
            .Where(s => s.TenantId == tenantId && s.LegacySkuId != null)
            .Select(s => s.LegacySkuId!)
            .ToListAsync(ct))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var catList = await _db.SkuCategories.Where(c => c.TenantId == tenantId).ToListAsync(ct);
        var catById = catList.ToDictionary(c => c.Id);
        var catKey = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in catList)
        {
            var parentName = c.ParentId is { } pid && catById.TryGetValue(pid, out var p) ? p.Name : null;
            catKey[CatKey(parentName, c.Name)] = c.Id;
        }

        bool isCsv = filePath.EndsWith(".csv", StringComparison.OrdinalIgnoreCase);
        int batched = 0;

        if (isCsv)
        {
            foreach (var (idx, row) in ReadCsvRows(filePath))
            {
                ct.ThrowIfCancellationRequested();
                string? Get(string col) => idx.TryGetValue(col, out var i) && i < row.Length ? Clean(row[i]) : null;

                try
                {
                    var legacy = Get("SKU ID");
                    var name = Get("Name");
                    if (name is null) { result.Inc("skipped_no_name"); continue; }
                    if (legacy is not null && existingLegacy.Contains(legacy)) { result.Inc("skipped_existing"); continue; }

                    var categoryName = Get("Category");
                    var subName = Get("Sub Category");
                    Guid? categoryId = ResolveCategory(tenantId, categoryName, subName, catKey, result);

                    var price = ParseDecimal(Get("Selling Price"));
                    var cost = ParseDecimal(Get("Cost Price"));
                    var unit = Get("Measuring Unit") ?? "ea";
                    var reorder = TryInt(Get("Low Inventory Threshold Count")) ?? 0;

                    _db.Skus.Add(new Sku
                    {
                        TenantId = tenantId,
                        LegacySkuId = legacy,
                        Code = legacy ?? name,
                        Name = name,
                        CategoryId = categoryId,
                        Unit = unit,
                        MrpPrice = price,
                        SellingPrice = price,
                        CostPrice = cost,
                        TaxPercent = 0m,
                        HsnSacCode = Get("HSN Code"),
                        ReorderLevel = reorder < 0 ? 0 : reorder,
                        StockOnHand = 0,
                        IsActive = IsActiveStatus(Get("Status")),
                    });
                    if (legacy is not null) existingLegacy.Add(legacy);
                    result.Inc("skus_created");
                    batched++;

                    if (batched >= 500) { if (!dryRun) { await _db.SaveChangesAsync(ct); } batched = 0; }
                }
                catch (Exception ex)
                {
                    _log.LogError(ex, "SKU CSV row failed: {Msg}", ex.Message);
                    result.Errors++;
                }
            }
        }
        else
        {
            foreach (var row in XlsxReader.ReadSheet(filePath, "SKUs"))
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

                    if (batched >= 500) { if (!dryRun) { await _db.SaveChangesAsync(ct); } batched = 0; }
                }
                catch (Exception ex)
                {
                    _log.LogError(ex, "SKU row {Row} failed.", row.RowNumber);
                    result.Errors++;
                }
            }
        }

        if (!dryRun && batched > 0) await _db.SaveChangesAsync(ct);
        return result;
    }

    private static IEnumerable<(Dictionary<string, int> idx, string[] row)> ReadCsvRows(string path)
    {
        using var reader = new StreamReader(path);
        var rows = CsvReader.Read(reader).GetEnumerator();
        if (!rows.MoveNext()) yield break;
        var idx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var headers = rows.Current;
        for (int i = 0; i < headers.Length; i++)
        {
            var h = headers[i].Trim();
            if (h.Length > 0 && !idx.ContainsKey(h)) idx[h] = i;
        }
        while (rows.MoveNext())
        {
            var row = rows.Current;
            if (row.All(string.IsNullOrWhiteSpace)) continue;
            yield return (idx, row);
        }
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
