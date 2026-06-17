using Pettle.Application.Clients;
using Pettle.Domain.Expenses;

namespace Pettle.Application.Expenses;

public record ExpenseListItem(Guid Id, DateTimeOffset Time, string Description, string? CategoryName, string PaymentMode, decimal Amount, decimal AmountIncTax, Guid? CategoryId = null, string? Notes = null, string? ReceiptUrl = null);
public record ExpenseCategoryDto(Guid Id, string Name, bool IsActive);
public record CreateOrUpdateExpenseRequest(DateTimeOffset Time, string Description, Guid? CategoryId, string PaymentMode, decimal Amount, decimal AmountIncTax, string? Notes, string? ReceiptUrl = null);

public interface IExpenseService
{
    Task<PagedResult<ExpenseListItem>> ListAsync(string? search, DateOnly? from, DateOnly? to, int page, int pageSize, CancellationToken ct = default);
    Task<ExpenseListItem> CreateAsync(CreateOrUpdateExpenseRequest req, CancellationToken ct = default);
    Task<ExpenseListItem?> UpdateAsync(Guid id, CreateOrUpdateExpenseRequest req, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<ExpenseCategoryDto>> ListCategoriesAsync(CancellationToken ct = default);
    Task<ExpenseCategoryDto> CreateCategoryAsync(string name, CancellationToken ct = default);
}
