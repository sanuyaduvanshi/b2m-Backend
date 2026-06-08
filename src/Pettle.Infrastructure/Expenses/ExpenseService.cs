using Microsoft.EntityFrameworkCore;
using Pettle.Application.Clients;
using Pettle.Application.Common;
using Pettle.Application.Common.Errors;
using Pettle.Application.Expenses;
using Pettle.Domain.Expenses;
using Pettle.Infrastructure.Persistence;

namespace Pettle.Infrastructure.Expenses;

public class ExpenseService : IExpenseService
{
    private readonly PettleDbContext _db;
    private readonly ICurrentUser _user;

    public ExpenseService(PettleDbContext db, ICurrentUser user) { _db = db; _user = user; }

    public async Task<PagedResult<ExpenseListItem>> ListAsync(string? search, DateOnly? from, DateOnly? to, int page, int pageSize, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return new PagedResult<ExpenseListItem>(Array.Empty<ExpenseListItem>(), 0, page, pageSize);
        var q = _db.Expenses.AsNoTracking().Include(e => e.Category).Where(e => e.TenantId == _user.TenantId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            q = q.Where(e => e.Description.ToLower().Contains(s) || (e.CategoryName != null && e.CategoryName.ToLower().Contains(s)));
        }
        if (from.HasValue) q = q.Where(e => e.Time >= from.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        if (to.HasValue) q = q.Where(e => e.Time <= to.Value.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc));

        var total = await q.CountAsync(ct);
        var p = Math.Max(page, 1); var sz = Math.Clamp(pageSize, 1, 200);
        var items = await q.OrderByDescending(e => e.Time).Skip((p - 1) * sz).Take(sz)
            .Select(e => new ExpenseListItem(e.Id, e.Time, e.Description, e.Category!.Name, e.PaymentMode, e.Amount, e.AmountIncTax, e.CategoryId, e.Notes))
            .ToListAsync(ct);
        return new PagedResult<ExpenseListItem>(items, total, p, sz);
    }

    public async Task<ExpenseListItem> CreateAsync(CreateOrUpdateExpenseRequest req, CancellationToken ct = default)
    {
        if (_user.TenantId is null) throw AppException.Forbidden();
        if (req.CategoryId.HasValue)
        {
            var catOk = await _db.ExpenseCategories.AnyAsync(c => c.Id == req.CategoryId && c.TenantId == _user.TenantId, ct);
            if (!catOk) throw AppException.Validation("Invalid category",
                new Dictionary<string, string[]> { ["categoryId"] = new[] { "Category not found in this business." } });
        }
        var e = new Expense
        {
            Time = req.Time, Description = req.Description, CategoryId = req.CategoryId,
            PaymentMode = req.PaymentMode, Amount = req.Amount, AmountIncTax = req.AmountIncTax, Notes = req.Notes
        };
        _db.Expenses.Add(e);
        await _db.SaveChangesAsync(ct);
        return new ExpenseListItem(e.Id, e.Time, e.Description, null, e.PaymentMode, e.Amount, e.AmountIncTax, e.CategoryId, e.Notes);
    }

    public async Task<ExpenseListItem?> UpdateAsync(Guid id, CreateOrUpdateExpenseRequest req, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return null;
        var e = await _db.Expenses.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == _user.TenantId, ct);
        if (e is null) return null;
        e.Time = req.Time; e.Description = req.Description; e.CategoryId = req.CategoryId;
        e.PaymentMode = req.PaymentMode; e.Amount = req.Amount; e.AmountIncTax = req.AmountIncTax; e.Notes = req.Notes;
        await _db.SaveChangesAsync(ct);
        return new ExpenseListItem(e.Id, e.Time, e.Description, null, e.PaymentMode, e.Amount, e.AmountIncTax, e.CategoryId, e.Notes);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return false;
        var e = await _db.Expenses.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == _user.TenantId, ct);
        if (e is null) return false;
        _db.Expenses.Remove(e);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<ExpenseCategoryDto>> ListCategoriesAsync(CancellationToken ct = default)
    {
        if (_user.TenantId is null) return Array.Empty<ExpenseCategoryDto>();
        return await _db.ExpenseCategories.AsNoTracking()
            .Where(c => c.TenantId == _user.TenantId)
            .OrderBy(c => c.Name)
            .Select(c => new ExpenseCategoryDto(c.Id, c.Name, c.IsActive))
            .ToListAsync(ct);
    }

    public async Task<ExpenseCategoryDto> CreateCategoryAsync(string name, CancellationToken ct = default)
    {
        var c = new ExpenseCategory { Name = name };
        _db.ExpenseCategories.Add(c);
        await _db.SaveChangesAsync(ct);
        return new ExpenseCategoryDto(c.Id, c.Name, c.IsActive);
    }
}
