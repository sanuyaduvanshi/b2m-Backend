using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Pettle.Domain.MyBusiness;
using Pettle.Infrastructure.Persistence;
using static Pettle.MigrationTool.ImportHelpers;

namespace Pettle.MigrationTool;

/// <summary>
/// Imports the B2M service catalogue (catalogue/&lt;tenant&gt;/...) into ServiceCategory/ServiceItem/ServiceVariant.
/// Source pricing models are heterogeneous; we map the useful core (service name + base price + size/coat variants)
/// onto the simple catalogue entities and drop the exotic boarding modifiers the domain can't represent.
/// Idempotent: a ServiceItem is keyed by (Vertical, Name).
/// </summary>
public class CatalogueImporter
{
    private readonly PettleDbContext _db;
    private readonly ILogger<CatalogueImporter> _log;

    public CatalogueImporter(PettleDbContext db, ILogger<CatalogueImporter> log)
    {
        _db = db;
        _log = log;
    }

    public async Task<ImportResult> ImportAsync(Guid tenantId, string catalogueRoot, bool dryRun, CancellationToken ct)
    {
        var result = new ImportResult();
        if (!Directory.Exists(catalogueRoot))
        {
            _log.LogError("Catalogue root not found: {Path}", catalogueRoot);
            result.Errors++;
            return result;
        }

        // Categories (auto-create by name)
        var cats = await _db.ServiceCategories.IgnoreQueryFilters().Where(c => c.TenantId == tenantId).ToListAsync(ct);
        var catByName = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in cats) catByName.TryAdd(c.Name, c.Id);

        Guid CategoryId(string name)
        {
            if (catByName.TryGetValue(name, out var id)) return id;
            var cat = new ServiceCategory { TenantId = tenantId, Name = name };
            _db.ServiceCategories.Add(cat);
            catByName[name] = cat.Id;
            result.Inc("categories_created");
            return cat.Id;
        }

        // Existing items keyed by (Vertical|Name) for idempotency.
        var existing = (await _db.ServiceItems.IgnoreQueryFilters()
            .Where(s => s.TenantId == tenantId)
            .Select(s => new { s.Vertical, s.Name })
            .ToListAsync(ct))
            .Select(x => $"{x.Vertical}|{x.Name.ToLowerInvariant()}")
            .ToHashSet(StringComparer.Ordinal);

        var newItems = new Dictionary<string, ServiceItem>(StringComparer.Ordinal);

        ServiceItem? GetOrCreateItem(string vertical, string name, string category, string? description, decimal basePrice)
        {
            var key = $"{vertical}|{name.ToLowerInvariant()}";
            if (existing.Contains(key)) { result.Inc("skipped_existing_items"); return null; }
            if (newItems.TryGetValue(key, out var present)) return present;
            var item = new ServiceItem
            {
                TenantId = tenantId,
                CategoryId = CategoryId(category),
                Name = name,
                Description = description,
                Vertical = vertical,
                BasePrice = basePrice,
                IsActive = true,
            };
            _db.ServiceItems.Add(item);
            newItems[key] = item;
            result.Inc("items_created");
            return item;
        }

        // ---- Grooming: one file per service; DEFAULT row = item, BREED_SIZE_AND_COAT_PRICE rows = variants ----
        var groomingDir = Path.Combine(catalogueRoot, "grooming");
        if (Directory.Exists(groomingDir))
        {
            foreach (var file in Directory.EnumerateFiles(groomingDir, "*.csv"))
            {
                foreach (var (idx, row) in ReadCsv(file))
                {
                    if (Bool(Cell(idx, row, "Is Archive"))) continue;
                    var name = Trim(Cell(idx, row, "Service Name"));
                    if (name is null) continue;
                    var rowType = Cell(idx, row, "Row Type") ?? "";
                    var item = GetOrCreateItem("Grooming", name, "Grooming", Trim(Cell(idx, row, "Service Description")),
                        ParseDecimal(Cell(idx, row, "Default Price") ?? "0"));
                    if (item is null) continue; // already existed → skip whole file's variants too

                    if (rowType.Contains("BREED_SIZE", StringComparison.OrdinalIgnoreCase))
                    {
                        var size = Trim(Cell(idx, row, "Breed Size"));
                        var coat = Trim(Cell(idx, row, "Coat"));
                        var price = ParseDecimal(Cell(idx, row, "Amount") ?? "0");
                        if (price <= 0 && size is null && coat is null) continue;
                        item.Variants.Add(new ServiceVariant
                        {
                            TenantId = tenantId,
                            Name = string.Join(" / ", new[] { size, coat }.Where(x => x is not null)),
                            SizeClass = size,
                            Price = price,
                        });
                        result.Inc("variants_created");
                    }
                }
            }
        }

        // ---- Resort services (Vet, Day Care): one row per service, FLAT base price ----
        var resortDir = Path.Combine(catalogueRoot, "resort_services");
        if (Directory.Exists(resortDir))
        {
            foreach (var file in Directory.EnumerateFiles(resortDir, "*.csv"))
            {
                var fileName = Path.GetFileNameWithoutExtension(file);
                var vertical = fileName.Contains("Day", StringComparison.OrdinalIgnoreCase) ? "DayCare" : "Vet";
                var category = vertical == "DayCare" ? "Day Care" : "Vet";
                foreach (var (idx, row) in ReadCsv(file))
                {
                    if (Bool(Cell(idx, row, "Is Archived"))) continue;
                    var name = Trim(Cell(idx, row, "Catalogue Name"));
                    if (name is null) continue;
                    GetOrCreateItem(vertical, name, category, Trim(Cell(idx, row, "Description")),
                        ParseDecimal(Cell(idx, row, "Base Price") ?? Cell(idx, row, "Tier Price") ?? "0"));
                }
            }
        }

        // ---- Boarding: BASE row sets price, BREED_SIZE rows become variants ----
        var boardingDir = Path.Combine(catalogueRoot, "boarding");
        if (Directory.Exists(boardingDir))
        {
            foreach (var file in Directory.EnumerateFiles(boardingDir, "*.csv"))
            {
                foreach (var (idx, row) in ReadCsv(file))
                {
                    var name = Trim(Cell(idx, row, "Catalogue Name"));
                    if (name is null) continue;
                    var rowType = Cell(idx, row, "Row Type") ?? "";
                    if (rowType.Equals("FLAGS", StringComparison.OrdinalIgnoreCase)) continue;

                    if (rowType.Equals("BASE", StringComparison.OrdinalIgnoreCase) || rowType.Equals("DAY_BASE", StringComparison.OrdinalIgnoreCase))
                    {
                        GetOrCreateItem("Boarding", name, "Boarding", null, ParseDecimal(Cell(idx, row, "Amount") ?? "0"));
                    }
                    else if (rowType.Contains("BREED_SIZE", StringComparison.OrdinalIgnoreCase))
                    {
                        var item = GetOrCreateItem("Boarding", name, "Boarding", null, 0);
                        var size = Trim(Cell(idx, row, "Breed Size"));
                        var price = ParseDecimal(Cell(idx, row, "Amount") ?? "0");
                        if (item is not null && (price > 0 || size is not null))
                        {
                            item.Variants.Add(new ServiceVariant { TenantId = tenantId, Name = size ?? "Size", SizeClass = size, Price = price });
                            result.Inc("variants_created");
                        }
                    }
                }
            }
        }

        // ---- Add-on services: imported as standalone items in an "Add-ons" category ----
        var addOnFile = Path.Combine(catalogueRoot, "add_on_services.csv");
        if (File.Exists(addOnFile))
        {
            foreach (var (idx, row) in ReadCsv(addOnFile))
            {
                var name = Trim(Cell(idx, row, "Name"));
                if (name is null) continue;
                GetOrCreateItem("AddOn", name, "Add-ons", Trim(Cell(idx, row, "Description")),
                    ParseDecimal(Cell(idx, row, "Base Amount") ?? "0"));
            }
        }

        if (!dryRun) await _db.SaveChangesAsync(ct);
        return result;
    }

    // ---- small CSV helpers (header-indexed) ----
    private static IEnumerable<(Dictionary<string, int> idx, string[] row)> ReadCsv(string path)
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

    private static string? Cell(Dictionary<string, int> idx, string[] row, string col)
        => idx.TryGetValue(col, out var i) && i < row.Length ? row[i] : null;

    private static string? Trim(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    private static bool Bool(string? s) => ParseYesNo(s) || string.Equals(s?.Trim(), "true", StringComparison.OrdinalIgnoreCase);
}
