namespace Pettle.Application.Reports;

public record DateRange(DateOnly From, DateOnly To)
{
    /// <summary>Unbounded. Expressed as the widest possible dates rather than nulls so every
    /// existing `>= From && <= To` filter keeps working untouched — there is no "all time" branch
    /// to forget in one query and remember in another.</summary>
    public static DateRange AllTime => new(DateOnly.MinValue, DateOnly.MaxValue);

    public bool IsAllTime => From == DateOnly.MinValue && To == DateOnly.MaxValue;
}

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
    IReadOnlyDictionary<string, int> CountByType,
    // How much of Paid, within this range, settled a bill on a *different* calendar day (IST) than
    // the bill's own invoice date — an advance-then-balance booking, or any payment collected after
    // the day it was raised. Surfaced so a report scoped to "the last 12 months" can show that not
    // everything in Paid was same-day cash, the same way the dashboard's Payments card does for
    // "today" specifically.
    decimal PaidOnDifferentDay = 0,
    int PaidOnDifferentDayCount = 0);
public record BookingsReport(int Total, int Completed, int Cancelled, int NoShow, IReadOnlyList<BookingsBreakdown> ByServiceType);
/// <summary>FirstOnboarding/LastOnboarding are the dates the client data itself actually spans —
/// what "all time" resolves to in practice, so the figure can name its own period instead of
/// leaving the reader to guess how far back it reaches. Null when there are no clients yet.</summary>
public record ClientsReport(int Active, int Archived, int NewInRange, IReadOnlyList<TopClient> TopClients, int Blacklisted = 0,
    DateOnly? FirstOnboarding = null, DateOnly? LastOnboarding = null);
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
    decimal PurchaseOrdersDue = 0,
    /// <summary>What was billed in the period across all three revenue lines — Sales, Bookings
    /// and Subscriptions. This is revenue in the ordinary accounting sense: earned when the work
    /// is billed, not when the customer happens to pay. It is deliberately the sum of the three
    /// cards beside it, so the figure can be checked by adding them up.</summary>
    decimal RevenueBilled = 0,
    /// <summary>Cash actually received in the period — collections, not revenue.</summary>
    decimal RevenueCollected = 0,
    /// <summary>The part of RevenueCollected that settled bills raised *in* this period.</summary>
    decimal RevenueForPeriodBills = 0,
    /// <summary>The part that settled bills raised *before* this period.
    ///
    /// Without this split Revenue looks broken: it rarely equals Sales + Bookings, because those
    /// cards report what was *billed* in the period while Revenue reports what was *received*.
    /// Cash arrives against older bills, this period's bills get paid on later days, and some of
    /// them are not paid at all. Reporting the buckets separately is what turns an apparent
    /// mismatch into an explanation. RevenueForPeriodBills + RevenueFromEarlierBills need not
    /// equal RevenueCollected — the remainder is cash with no invoice behind it, i.e. a
    /// subscription purchase.</summary>
    decimal RevenueFromEarlierBills = 0
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
