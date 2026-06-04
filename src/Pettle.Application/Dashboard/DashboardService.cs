namespace Pettle.Application.Dashboard;

public record DashboardKpis(
    int UpcomingBookings,
    int ActiveBookings,
    int CheckInDue,
    int CheckOutDue,
    decimal RevenueToday,
    int PendingReminders,
    int LowStockSkus,
    int OutstandingInvoices
);

public record ReminderTile(Guid Id, string Title, string? ContactName, string? Phone, string Type, DateOnly DueDate);

public record RevenueDay(DateOnly Date, decimal Amount);

public record RecentBookingTile(
    Guid Id,
    string? LegacyBookingId,
    DateOnly BookingDate,
    string ParentName,
    string Phone,
    string ServiceTypes,
    decimal TotalBillingAmount,
    string Status
);

public record DashboardOverview(
    DashboardKpis Kpis,
    IReadOnlyList<ReminderTile> TopReminders,
    IReadOnlyList<RevenueDay> RevenueTrend,
    IReadOnlyList<RecentBookingTile> RecentBookings
);

public interface IDashboardService
{
    Task<DashboardOverview> OverviewAsync(CancellationToken ct = default);
}
