using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Pettle.Domain.Expenses;
using Pettle.Infrastructure.Persistence;
using static Pettle.MigrationTool.ImportHelpers;

namespace Pettle.MigrationTool;

public class ExpensesImporter
{
    private readonly PettleDbContext _db;
    private readonly ILogger<ExpensesImporter> _log;

    public ExpensesImporter(PettleDbContext db, ILogger<ExpensesImporter> log)
    {
        _db = db;
        _log = log;
    }

    public async Task<ImportResult> ImportAsync(Guid tenantId, string xlsxPath, bool dryRun, CancellationToken ct)
    {
        var result = new ImportResult();

        // No legacy id on Expense — dedupe by (Description, Time, Amount, Category) signature so a re-run is safe.
        // Category is part of the key so two same-amount/same-time rows under different categories are kept distinct.
        var existingList = await _db.Expenses.IgnoreQueryFilters()
            .Where(e => e.TenantId == tenantId)
            .Select(e => new { e.Description, e.Time, e.Amount, e.CategoryName })
            .ToListAsync(ct);
        var existingKeys = new HashSet<string>(
            existingList.Select(x => MakeKey(x.Description, x.Time, x.Amount, x.CategoryName)),
            StringComparer.Ordinal);

        // Cache categories (auto-create on first sight)
        var catList = await _db.ExpenseCategories.Where(c => c.TenantId == tenantId).ToListAsync(ct);
        var catsByName = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in catList) catsByName.TryAdd(c.Name, c.Id);

        foreach (var row in XlsxReader.ReadSheet(xlsxPath, "Expenses"))
        {
            ct.ThrowIfCancellationRequested();
            if (row.AllEmpty()) continue;

            try
            {
                var description = row.GetOrNull("Expense") ?? "(no description)";
                var when = ParseDateTime(row.Get("Time")) ?? DateTimeOffset.UtcNow;
                var amount = ParseDecimal(row.Get("Amount"));
                var catName = row.GetOrNull("Category");

                var key = MakeKey(description, when, amount, catName);
                if (existingKeys.Contains(key)) { result.Inc("skipped_existing"); continue; }
                Guid? catId = null;
                if (catName is not null)
                {
                    if (!catsByName.TryGetValue(catName, out var cid))
                    {
                        var cat = new ExpenseCategory { TenantId = tenantId, Name = catName };
                        _db.ExpenseCategories.Add(cat);
                        cid = cat.Id;
                        catsByName[catName] = cid;
                        result.Inc("categories_created");
                    }
                    catId = cid;
                }

                var amountIncTax = ParseDecimal(row.Get("Amount (Inc. Tax)"));
                if (amountIncTax == 0m) amountIncTax = amount; // default to net if missing

                _db.Expenses.Add(new Expense
                {
                    TenantId = tenantId,
                    Time = when,
                    Description = description,
                    CategoryId = catId,
                    CategoryName = catName,
                    PaymentMode = row.GetOrNull("Mode") ?? "Cash",
                    Amount = amount,
                    AmountIncTax = amountIncTax,
                });
                existingKeys.Add(key);
                result.Inc("expenses_created");
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Expense row {Row} failed.", row.RowNumber);
                result.Errors++;
            }
        }

        if (!dryRun) await _db.SaveChangesAsync(ct);
        return result;
    }

    private static string MakeKey(string desc, DateTimeOffset when, decimal amount, string? category)
        => $"{desc.Trim()}|{when.UtcDateTime:O}|{amount}|{category?.Trim()}";
}
