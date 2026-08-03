namespace Pettle.Application.Reports;

public record DateRange(DateOnly From, DateOnly To);

public record RevenuePoint(DateOnly Date, decimal Amount);
public record BookingsBreakdown(string ServiceType, int Count, decimal Revenue);

public record ExpenseSlice(string Label, decimal Amount);

public record RevenueReport(
    decimal Total, decimal Paid, decimal Due,
    IReadOnlyList<RevenuePoint> Daily,
    IReadOnlyDictionary<string, decimal> ByPaymentMode,
    IReadOnlyList<ExpenseSlice> ByType,
    IReadOnlyList<ExpenseSlice> ByStatus,
    int Count,
    IReadOnlyDictionary<string, int> CountByType);
public record BookingsReport(int Total, int Completed, int Cancelled, int NoShow, IReadOnlyList<BookingsBreakdown> ByServiceType);
public record ClientsReport(int Active, int Archived, int NewInRange, IReadOnlyList<TopClient> TopClients, int Blacklisted = 0);
public record TopClient(Guid Id, string Name, string Phone, int Bookings, decimal Spend);
public record InventoryReport(int TotalSkus, int LowStock, int ExpiringSoon, decimal InventoryValue, IReadOnlyList<ExpenseSlice> ByCategory, int OutOfStock, int ListedInApp);

public record ExpensesReport(decimal Total, int Count, IReadOnlyList<ExpenseSlice> ByCategory, IReadOnlyList<ExpenseSlice> ByMode);

/// <summary>Money in (payments collected) vs money out (expenses) for the range.</summary>
public record ProfitReport(decimal Collected, decimal Expenses, decimal Net);

public record ReportsOverview(
    decimal RevenueInRange,
    int BookingsInRange,
    int NewClientsInRange,
    int InvoicesInRange
);

/// <summary>One calendar month's revenue/expenses/net — the Reports Overview trend chart.</summary>
public record MonthlyPoint(string Month, decimal Revenue, decimal Expenses, decimal Net);

/// <summary>Dashboard's period-filterable KPI row — one line per business vertical so "Sale"
/// (walk-in/POS) revenue is never blended into booking or subscription revenue. Purchase Orders
/// track spend (cost), not revenue, and are reported separately for that reason.</summary>
public record PeriodSummary(
    decimal SalesRevenue, int SalesCount,
    int TotalBookings, decimal BookingsRevenue,
    int TotalSubscriptions, decimal SubscriptionsRevenue,
    int TotalPurchaseOrders, decimal PurchaseOrdersValue,
    decimal SalesDue = 0, decimal BookingsDue = 0, decimal SubscriptionsDue = 0,
    decimal PurchaseOrdersDue = 0
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
    Task<IReadOnlyList<MonthlyPoint>> MonthlyAsync(DateRange range, CancellationToken ct = default);
    Task<PeriodSummary> PeriodSummaryAsync(DateRange range, CancellationToken ct = default);
}
