using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Pettle.Domain.Inventory;
using Pettle.Infrastructure.Persistence;

namespace Pettle.MigrationTool;

/// <summary>
/// One-time bridge for B2M Vet Care's VasyERP-imported <see cref="Product"/> catalogue: Products
/// deliberately never participate in SKU stock/POs/sales (see the entity's own doc comment), so
/// none of them were sellable through the POS, which only ever searches <see cref="Sku"/>. This
/// creates one Sku per Product that doesn't already have a same-Code Sku, so the whole catalogue
/// becomes sellable. The source Products are left untouched — this only adds Sku rows.
///
/// Field mapping decisions (confirmed with the client, 2026-09-29):
///  - Sku.TaxPercent  &lt;- Product.SalesTaxPercent (what actually applies at the POS register)
///  - Sku.CostPrice   &lt;- Product.LandingCost, falling back to PurchasePrice when LandingCost is 0
///  - Sku.StockOnHand &lt;- Product.Quantity, set directly with no SkuBatch (matches SkusImporter's
///    own established pattern for the original 498 SKUs — FifoBatchDeductor tolerates a SKU with
///    stock but no batches; batches are for FIFO/expiry detail, not a precondition for selling)
///  - Category/SubCategory map onto SkuCategory's parent/child hierarchy (same as SkusImporter);
///    Brand maps onto SkuBrand, which has no parent concept, so SubBrand has nowhere to go.
/// Idempotent: safe to re-run — skips any Product whose Code already has a Sku.
/// </summary>
public class ProductsToSkusMigrator
{
    private readonly PettleDbContext _db;
    private readonly ILogger<ProductsToSkusMigrator> _log;

    public ProductsToSkusMigrator(PettleDbContext db, ILogger<ProductsToSkusMigrator> log)
    {
        _db = db;
        _log = log;
    }

    public async Task<ImportResult> MigrateAsync(Guid tenantId, bool dryRun, CancellationToken ct)
    {
        var result = new ImportResult();

        var existingSkuCodes = (await _db.Skus.IgnoreQueryFilters()
            .Where(s => s.TenantId == tenantId)
            .Select(s => s.Code)
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

        var brandList = await _db.SkuBrands.Where(b => b.TenantId == tenantId).ToListAsync(ct);
        var brandKey = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        foreach (var b in brandList) brandKey[b.Name.Trim().ToLowerInvariant()] = b.Id;

        var products = await _db.Products.AsNoTracking()
            .Where(p => p.TenantId == tenantId && !p.IsDeleted)
            .ToListAsync(ct);

        int batched = 0;
        foreach (var p in products)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                if (existingSkuCodes.Contains(p.Code)) { result.Inc("skipped_existing_sku"); continue; }

                var categoryId = ResolveCategory(tenantId, p.Category, p.SubCategory, catKey, result);
                var brandId = ResolveBrand(tenantId, p.Brand, brandKey, result);

                var cost = p.LandingCost > 0 ? p.LandingCost : p.PurchasePrice;
                var stock = (int)Math.Round(p.Quantity);

                _db.Skus.Add(new Sku
                {
                    TenantId = tenantId,
                    Code = p.Code,
                    Name = p.Name,
                    Description = p.Description ?? p.ShortDescription,
                    CategoryId = categoryId,
                    BrandId = brandId,
                    Unit = string.IsNullOrWhiteSpace(p.Unit) ? "PCS" : p.Unit,
                    MrpPrice = p.MrpPrice,
                    SellingPrice = p.SellingPrice,
                    CostPrice = cost,
                    TaxPercent = p.SalesTaxPercent,
                    HsnSacCode = p.HsnCode,
                    ReorderLevel = 0,
                    StockOnHand = stock < 0 ? 0 : stock,
                    TrackExpiry = false,
                    IsActive = p.IsActive,
                });
                existingSkuCodes.Add(p.Code);
                result.Inc("skus_created");
                batched++;

                if (batched >= 500) { if (!dryRun) await _db.SaveChangesAsync(ct); batched = 0; }
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Product {Code} failed to migrate to SKU: {Msg}", p.Code, ex.Message);
                result.Errors++;
            }
        }

        if (!dryRun && batched > 0) await _db.SaveChangesAsync(ct);
        return result;
    }

    private Guid? ResolveCategory(Guid tenantId, string? category, string? sub, Dictionary<string, Guid> cache, ImportResult result)
    {
        category = category?.Trim(); sub = sub?.Trim();
        if (string.IsNullOrEmpty(category) && string.IsNullOrEmpty(sub)) return null;

        Guid? parentId = null;
        if (!string.IsNullOrEmpty(category))
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

        if (string.IsNullOrEmpty(sub)) return parentId;

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

    private Guid? ResolveBrand(Guid tenantId, string? brand, Dictionary<string, Guid> cache, ImportResult result)
    {
        brand = brand?.Trim();
        if (string.IsNullOrEmpty(brand)) return null;

        var key = brand.ToLowerInvariant();
        if (cache.TryGetValue(key, out var id)) return id;

        var b = new SkuBrand { TenantId = tenantId, Name = brand };
        _db.SkuBrands.Add(b);
        cache[key] = b.Id;
        result.Inc("brands_created");
        return b.Id;
    }

    private static string CatKey(string? parent, string child) => $"{parent?.Trim()?.ToLowerInvariant()}>{child.Trim().ToLowerInvariant()}";
}
