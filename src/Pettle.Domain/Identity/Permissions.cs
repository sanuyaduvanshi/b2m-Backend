namespace Pettle.Domain.Identity;

public static class Modules
{
    public const string Dashboard = "Dashboard";
    public const string MyBusiness = "MyBusiness";
    public const string Reminders = "Reminders";
    public const string DailyTasks = "DailyTasks";
    public const string BookingRequests = "BookingRequests";
    public const string BookingRecords = "BookingRecords";
    public const string ClientEnquiries = "ClientEnquiries";
    public const string ClientDatabase = "ClientDatabase";
    public const string Marketplace = "Marketplace";
    public const string Kennels = "Kennels";
    public const string Invoices = "Invoices";
    public const string Inventory = "Inventory";
    public const string Subscriptions = "Subscriptions";
    public const string Expenses = "Expenses";
    public const string Messages = "Messages";
    public const string Calendar = "Calendar";
    public const string Reports = "Reports";
    public const string AccessManagement = "AccessManagement";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Dashboard, MyBusiness, Reminders, DailyTasks,
        BookingRequests, BookingRecords, ClientEnquiries, ClientDatabase,
        Marketplace, Kennels, Invoices, Inventory,
        Subscriptions, Expenses, Messages, Calendar, Reports, AccessManagement
    };
}

public static class Actions
{
    public const string View = "View";
    public const string Create = "Create";
    public const string Edit = "Edit";
    public const string Delete = "Delete";
    public const string Approve = "Approve";
    public const string Export = "Export";

    public static readonly IReadOnlyList<string> All = new[] { View, Create, Edit, Delete, Approve, Export };
}

public static class SystemRoles
{
    public const string SystemAdmin = "SystemAdmin";
    public const string BusinessOwner = "BusinessOwner";
    public const string BranchManager = "BranchManager";
    public const string FrontDesk = "FrontDesk";
    public const string Receptionist = "Receptionist";
    public const string Groomer = "Groomer";
    public const string BoardingStaff = "BoardingStaff";
    public const string Veterinarian = "Veterinarian";
    public const string Accountant = "Accountant";
    public const string ReadOnly = "ReadOnly";

    public static readonly IReadOnlyList<string> All = new[]
    {
        SystemAdmin, BusinessOwner, BranchManager, FrontDesk, Receptionist,
        Groomer, BoardingStaff, Veterinarian, Accountant, ReadOnly
    };
}

public static class Permission
{
    public static string Key(string module, string action) => $"{module}.{action}";

    public static IEnumerable<string> AllPermissions()
    {
        foreach (var m in Modules.All)
            foreach (var a in Actions.All)
                yield return Key(m, a);
    }
}
