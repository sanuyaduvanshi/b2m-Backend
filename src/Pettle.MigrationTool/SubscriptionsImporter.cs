using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Pettle.Domain.Subscriptions;
using Pettle.Infrastructure.Persistence;
using static Pettle.MigrationTool.ImportHelpers;

namespace Pettle.MigrationTool;

public class SubscriptionsImporter
{
    private readonly PettleDbContext _db;
    private readonly ILogger<SubscriptionsImporter> _log;

    public SubscriptionsImporter(PettleDbContext db, ILogger<SubscriptionsImporter> log)
    {
        _db = db;
        _log = log;
    }

    public async Task<ImportResult> ImportAsync(Guid tenantId, string xlsxPath, bool dryRun, CancellationToken ct)
    {
        var result = new ImportResult();

        // Dedupe by (Name + Price): the export can carry two packages with the same name but different prices.
        var existingKeys = (await _db.SubscriptionPackages.IgnoreQueryFilters()
            .Where(p => p.TenantId == tenantId)
            .Select(p => new { p.Name, p.Price })
            .ToListAsync(ct))
            .Select(x => PkgKey(x.Name, x.Price))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var row in XlsxReader.ReadSheet(xlsxPath, "Subscriptions"))
        {
            ct.ThrowIfCancellationRequested();
            if (row.AllEmpty()) continue;
            var name = row.GetOrNull("Name");
            if (name is null) { result.Inc("skipped_no_name"); continue; }
            var price = ParseDecimal(row.Get("Price"));
            var key = PkgKey(name, price);
            if (existingKeys.Contains(key)) { result.Inc("skipped_existing"); continue; }

            try
            {
                var pkg = new SubscriptionPackage
                {
                    TenantId = tenantId,
                    Name = name,
                    Description = row.GetOrNull("Description"),
                    ValidityDays = ParseInt(row.Get("Validity")),
                    Price = price,
                    IsTaxInclusive = ParseYesNo(row.Get("Is Tax Inclusive")) || row.GetOrNull("Is Tax Inclusive")?.Equals("True", StringComparison.OrdinalIgnoreCase) == true,
                    TaxPercent = ParseDecimal(row.Get("Tax 1 percentage")),
                };

                for (int i = 1; i <= 6; i++)
                {
                    var svcName = row.GetOrNull($"Service {i} name");
                    if (svcName is null) continue;
                    pkg.Services.Add(new SubscriptionPackageService
                    {
                        TenantId = tenantId,
                        ServiceName = svcName,
                        Discount = ParseDecimal(row.Get($"Service {i} discount")),
                        DiscountType = ParseDiscountType(row.Get($"Service {i} discount type")),
                        DaysOrSessions = TryInt(row.Get($"Service {i} days/sessions")),
                        BoardingType = row.GetOrNull($"Service {i} boarding type"),
                    });
                    result.Inc("services_created");
                }

                // The export has at most one SKU category, sub-category, and SKU per package.
                var skuCat = row.GetOrNull("SKU category 1");
                if (skuCat is not null)
                {
                    pkg.Services.Add(new SubscriptionPackageService
                    {
                        TenantId = tenantId,
                        ServiceName = "SKU Category Discount",
                        SkuCategory = skuCat,
                        Discount = ParseDecimal(row.Get("SKU category 1 discount")),
                        DiscountType = ParseDiscountType(row.Get("SKU category 1 discount type")),
                    });
                    result.Inc("sku_category_rules");
                }
                var skuSubCat = row.GetOrNull("SKU sub category 1");
                if (skuSubCat is not null)
                {
                    pkg.Services.Add(new SubscriptionPackageService
                    {
                        TenantId = tenantId,
                        ServiceName = "SKU Subcategory Discount",
                        SkuSubCategory = skuSubCat,
                        Discount = ParseDecimal(row.Get("SKU sub category 1 discount")),
                        DiscountType = ParseDiscountType(row.Get("SKU sub category 1 discount type")),
                    });
                    result.Inc("sku_subcategory_rules");
                }
                var skuName = row.GetOrNull("SKU 1");
                if (skuName is not null)
                {
                    pkg.Services.Add(new SubscriptionPackageService
                    {
                        TenantId = tenantId,
                        ServiceName = $"SKU: {skuName}",
                        Discount = ParseDecimal(row.Get("SKU 1 discount")),
                        DiscountType = ParseDiscountType(row.Get("SKU 1 discount type")),
                    });
                    result.Inc("sku_rules");
                }

                _db.SubscriptionPackages.Add(pkg);
                existingKeys.Add(key);
                result.Inc("packages_created");
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Subscription row {Row} ({Name}) failed.", row.RowNumber, name);
                result.Errors++;
            }
        }

        if (!dryRun) await _db.SaveChangesAsync(ct);
        return result;
    }

    private static string PkgKey(string name, decimal price) => $"{name.Trim()}|{price}";
}
