using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Pettle.Application.Audit;
using Pettle.Application.Auth;
using Pettle.Application.BookingRequests;
using Pettle.Application.Bookings;
using Pettle.Application.Calendar;
using Pettle.Application.ClientEnquiries;
using Pettle.Application.Clients;
using Pettle.Application.Common;
using Pettle.Application.DailyTasks;
using Pettle.Application.Dashboard;
using Pettle.Application.Expenses;
using Pettle.Application.Inventory;
using Pettle.Application.Invoices;
using Pettle.Application.Kennels;
using Pettle.Application.Messages;
using Pettle.Application.MyBusiness;
using Pettle.Application.Reminders;
using Pettle.Application.Reports;
using Pettle.Application.Subscriptions;
using Pettle.Infrastructure.Auth;
using Pettle.Infrastructure.BookingRequests;
using Pettle.Infrastructure.Bookings;
using Pettle.Infrastructure.Calendar;
using Pettle.Infrastructure.ClientEnquiries;
using Pettle.Infrastructure.Clients;
using Pettle.Infrastructure.DailyTasks;
using Pettle.Infrastructure.Dashboard;
using Pettle.Infrastructure.Expenses;
using Pettle.Infrastructure.Identity;
using Pettle.Infrastructure.Inventory;
using Pettle.Infrastructure.Invoices;
using Pettle.Infrastructure.Kennels;
using Pettle.Infrastructure.Messages;
using Pettle.Infrastructure.MyBusiness;
using Pettle.Infrastructure.Persistence;
using Pettle.Infrastructure.Reminders;
using Pettle.Infrastructure.Reports;
using Pettle.Infrastructure.Subscriptions;
using Pettle.Infrastructure.Tenancy;
using QuestPDF.Infrastructure;

namespace Pettle.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddPettleInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var conn = config.GetConnectionString("Postgres")
                   ?? throw new InvalidOperationException("Missing ConnectionStrings:Postgres");

        services.AddDbContext<PettleDbContext>(opt =>
        {
            opt.UseNpgsql(conn, npg => npg.MigrationsHistoryTable("__EFMigrationsHistory", "pettle"));
        });

        services.AddIdentityCore<ApplicationUser>(opt =>
        {
            opt.Password.RequiredLength = 8;
            opt.Password.RequireNonAlphanumeric = true;
            opt.User.RequireUniqueEmail = true;
        })
        .AddRoles<ApplicationRole>()
        .AddEntityFrameworkStores<PettleDbContext>()
        .AddDefaultTokenProviders();

        services.Configure<JwtOptions>(config.GetSection(JwtOptions.SectionName));
        var jwt = config.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
                  ?? throw new InvalidOperationException("Missing Jwt section");

        services.AddHttpContextAccessor();
        services.AddHttpClient();
        services.AddScoped<ICurrentUser, CurrentUser>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IClientService, ClientService>();
        services.AddScoped<IBookingService, BookingServiceImpl>();
        services.AddScoped<IInvoiceService, InvoiceService>();
        services.AddScoped<IInventoryService, InventoryService>();
        services.AddScoped<IExpenseService, ExpenseService>();
        services.AddScoped<ISubscriptionService, Pettle.Infrastructure.Subscriptions.SubscriptionService>();
        services.AddScoped<IKennelService, KennelService>();
        services.AddScoped<IReminderService, ReminderService>();
        services.AddScoped<IMyBusinessService, MyBusinessService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IReportsService, ReportsService>();
        services.AddScoped<IDailyTaskService, DailyTaskService>();
        services.AddScoped<IBookingRequestService, BookingRequestService>();
        services.AddScoped<IClientEnquiryService, ClientEnquiryService>();
        services.AddScoped<IMessageTemplateService, MessageTemplateService>();
        services.AddScoped<IMessageService, MessageService>();
        services.AddScoped<ICalendarService, CalendarService>();
        services.AddScoped<IAuditService, Pettle.Infrastructure.Audit.AuditService>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(opt =>
            {
                opt.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Secret)),
                    ClockSkew = TimeSpan.FromSeconds(30)
                };
            });

        return services;
    }
}
