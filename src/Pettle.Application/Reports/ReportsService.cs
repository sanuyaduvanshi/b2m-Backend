namespace Pettle.Application.Reports;

public record DateRange(DateOnly From, DateOnly To);

public record RevenuePoint(DateOnly Date, decimal Amount);
public record BookingsBreakdown(string ServiceType, int Count, decimal Revenue);

public record RevenueReport(decimal Total, decimal Paid, decimal Due, IReadOnlyList<RevenuePoint> Daily, IReadOnlyDictionary<string, decimal> ByPaymentMode);
public record BookingsReport(int Total, int Completed, int Cancelled, int NoShow, IReadOnlyList<BookingsBreakdown> ByServiceType);
public record ClientsReport(int Active, int Archived, int NewInRange, IReadOnlyList<TopClient> TopClients);
public record TopClient(Guid Id, string Name, string Phone, int Bookings, decimal Spend);
public record InventoryReport(int TotalSkus, int LowStock, int ExpiringSoon, decimal InventoryValue);

public record ExpenseSlice(string Label, decimal Amount);
public record ExpensesReport(decimal Total, int Count, IReadOnlyList<ExpenseSlice> ByCategory, IReadOnlyList<ExpenseSlice> ByMode);

/// <summary>Money in (payments collected) vs money out (expenses) for the range.</summary>
public record ProfitReport(decimal Collected, decimal Expenses, decimal Net);

public record ReportsOverview(
    decimal RevenueInRange,
    int BookingsInRange,
    int NewClientsInRange,
    int InvoicesInRange
);

public interface IReportsService
{
    Task<ReportsOverview> OverviewAsync(DateRange range, CancellationToken ct = default);
    Task<RevenueReport> RevenueAsync(DateRange range, CancellationToken ct = default);
    Task<BookingsReport> BookingsAsync(DateRange range, CancellationToken ct = default);
    Task<ClientsReport> ClientsAsync(DateRange range, CancellationToken ct = default);
    Task<InventoryReport> InventoryAsync(CancellationToken ct = default);
    Task<ExpensesReport> ExpensesAsync(DateRange range, CancellationToken ct = default);
    Task<ProfitReport> ProfitAsync(DateRange range, CancellationToken ct = default);
}
