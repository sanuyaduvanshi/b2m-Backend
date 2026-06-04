using Pettle.Domain.Common;

namespace Pettle.Domain.Expenses;

public class ExpenseCategory : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}

public class Expense : SoftDeletableTenantEntity
{
    public DateTimeOffset Time { get; set; }
    public string Description { get; set; } = string.Empty;
    public Guid? CategoryId { get; set; }
    public ExpenseCategory? Category { get; set; }
    public string? CategoryName { get; set; }
    public string PaymentMode { get; set; } = "Cash";
    public decimal Amount { get; set; }
    public decimal AmountIncTax { get; set; }
    public string? ReceiptUrl { get; set; }
    public Guid? RelatedPurchaseOrderId { get; set; }
    public string? Notes { get; set; }
}
