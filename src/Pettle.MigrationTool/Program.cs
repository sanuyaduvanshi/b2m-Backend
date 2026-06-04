using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Pettle.Application.Common;
using Pettle.Infrastructure.Persistence;
using Pettle.MigrationTool;

var cli = ParseArgs(args);

if (cli.Help || cli.TenantSlug is null || (cli.File is null && cli.ExportRoot is null && cli.Import is null))
{
    PrintHelp();
    return cli.Help ? 0 : 1;
}

var builder = Host.CreateApplicationBuilder();
builder.Configuration
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true)
    .AddJsonFile("appsettings.Development.json", optional: true)
    .AddEnvironmentVariables();

var conn = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException(
        "Missing ConnectionStrings:Postgres — set it in appsettings.json or ConnectionStrings__Postgres env var.");

var importUser = new ImportUser();
builder.Services.AddSingleton<ICurrentUser>(importUser);
builder.Services.AddDbContext<PettleDbContext>(opt =>
    opt.UseNpgsql(conn, npg => npg.MigrationsHistoryTable("__EFMigrationsHistory", "pettle")));
builder.Services.AddScoped<ClientsImporter>();
builder.Services.AddScoped<PurchaseOrdersImporter>();
builder.Services.AddScoped<ExpensesImporter>();
builder.Services.AddScoped<SubscriptionsImporter>();
builder.Services.AddScoped<BookingsImporter>();
builder.Services.AddScoped<InvoicesImporter>();
builder.Services.AddScoped<PaymentsImporter>();

using var host = builder.Build();
using var scope = host.Services.CreateScope();
var sp = scope.ServiceProvider;
var log = sp.GetRequiredService<ILoggerFactory>().CreateLogger("MigrationTool");
var db = sp.GetRequiredService<PettleDbContext>();

var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Slug == cli.TenantSlug);
if (tenant is null)
{
    log.LogError("Tenant with slug '{Slug}' not found.", cli.TenantSlug);
    return 2;
}
log.LogInformation("Tenant resolved: {Name} ({Id})", tenant.Name, tenant.Id);
importUser.TenantId = tenant.Id;

// ---- determine which importers to run + the file each one uses ----
var jobs = ResolveJobs(cli);
if (jobs.Count == 0)
{
    log.LogError("Nothing to import. Provide --import=<name>, or --file=<path> for the default clients flow.");
    return 1;
}

var overall = new Dictionary<string, string>();
int exitCode = 0;
var wall = System.Diagnostics.Stopwatch.StartNew();

foreach (var (name, filePath) in jobs)
{
    if (!File.Exists(filePath))
    {
        log.LogError("[{Name}] file not found: {Path}", name, filePath);
        overall[name] = $"file not found: {filePath}";
        exitCode = 3;
        continue;
    }

    log.LogInformation("==== {Name}: {Path}{Dry} ====", name, filePath, cli.DryRun ? " (DRY RUN)" : "");
    var sw = System.Diagnostics.Stopwatch.StartNew();
    try
    {
        string summary = name switch
        {
            "clients"       => (await sp.GetRequiredService<ClientsImporter>().ImportAsync(tenant.Id, filePath, cli.DryRun, CancellationToken.None)).ToString(),
            "pos"           => (await sp.GetRequiredService<PurchaseOrdersImporter>().ImportAsync(tenant.Id, filePath, cli.DryRun, CancellationToken.None)).ToString(),
            "expenses"      => (await sp.GetRequiredService<ExpensesImporter>().ImportAsync(tenant.Id, filePath, cli.DryRun, CancellationToken.None)).ToString(),
            "subscriptions" => (await sp.GetRequiredService<SubscriptionsImporter>().ImportAsync(tenant.Id, filePath, cli.DryRun, CancellationToken.None)).ToString(),
            "bookings"      => (await sp.GetRequiredService<BookingsImporter>().ImportAsync(tenant.Id, filePath, cli.DryRun, CancellationToken.None)).ToString(),
            "invoices"      => (await sp.GetRequiredService<InvoicesImporter>().ImportAsync(tenant.Id, filePath, cli.DryRun, CancellationToken.None)).ToString(),
            "payments"      => (await sp.GetRequiredService<PaymentsImporter>().ImportAsync(tenant.Id, filePath, cli.DryRun, CancellationToken.None)).ToString(),
            _ => $"(unknown importer '{name}')",
        };
        sw.Stop();
        overall[name] = $"{summary}  [{sw.Elapsed:mm\\:ss}]";
        log.LogInformation("[{Name}] done: {Summary}", name, summary);
    }
    catch (Exception ex)
    {
        sw.Stop();
        log.LogError(ex, "[{Name}] FAILED after {Elapsed}", name, sw.Elapsed);
        overall[name] = $"FAILED: {ex.Message}";
        exitCode = 4;
        if (cli.StopOnError) break;
    }
}

wall.Stop();

Console.WriteLine();
Console.WriteLine("================ Migration summary ================");
foreach (var kv in overall) Console.WriteLine($"  {kv.Key,-15} {kv.Value}");
Console.WriteLine($"Total elapsed: {wall.Elapsed:mm\\:ss}.{wall.ElapsedMilliseconds % 1000:000}");
if (cli.DryRun) Console.WriteLine("(no rows were persisted)");
return exitCode;

static List<(string name, string file)> ResolveJobs(CliArgs cli)
{
    // Default sequence (FK order). 'tasks' skipped — source export is empty.
    var allInOrder = new[] { "clients", "pos", "expenses", "subscriptions", "bookings", "invoices", "payments" };

    var jobs = new List<(string, string)>();
    var requested = (cli.Import ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(s => s.ToLowerInvariant()).ToList();

    if (requested.Count == 0)
    {
        // Legacy invocation: --tenant=b2m --file=clients.csv  → import clients only.
        if (cli.File is not null) jobs.Add(("clients", cli.File));
        return jobs;
    }

    if (requested.Contains("all")) requested = allInOrder.ToList();

    // Sort requested in our defined sequence so FKs resolve.
    var ordered = allInOrder.Where(requested.Contains).ToList();
    foreach (var name in ordered)
    {
        var file = ResolveDefaultPath(name, cli.ExportRoot);
        if (cli.File is not null && requested.Count == 1) file = cli.File;
        jobs.Add((name, file));
    }
    return jobs;
}

static string ResolveDefaultPath(string name, string? root)
{
    root ??= LocateExportRoot();
    return name switch
    {
        "clients"       => Path.Combine(root, "clients.csv"),
        "pos"           => Path.Combine(root, "purchase_orders", "B2M VET CARE.csv"),
        "expenses"      => Path.Combine(root, "expenses",      "B2M VET CARE.xlsx"),
        "subscriptions" => Path.Combine(root, "subscriptions", "B2M VET CARE.xlsx"),
        "bookings"      => Path.Combine(root, "bookings",      "B2M VET CARE.xlsx"),
        "invoices"      => Path.Combine(root, "invoices",      "B2M VET CARE.xlsx"),
        "payments"      => Path.Combine(root, "payments",      "B2M VET CARE.xlsx"),
        _ => string.Empty,
    };
}

static string LocateExportRoot()
{
    // Walk up from the current working dir (and from the assembly location) looking for the
    // doubly-nested 'bulk_export_2026-05-13' folder.
    var seeds = new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory };
    foreach (var seed in seeds)
    {
        var dir = new DirectoryInfo(seed);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "bulk_export_2026-05-13", "bulk_export_2026-05-13");
            if (Directory.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
    }
    throw new InvalidOperationException("Could not locate bulk_export_2026-05-13 folder. Pass --export-root=<path>.");
}

static CliArgs ParseArgs(string[] args)
{
    var r = new CliArgs();
    foreach (var arg in args)
    {
        if (arg is "-h" or "--help") { r.Help = true; continue; }
        if (arg == "--dry-run") { r.DryRun = true; continue; }
        if (arg == "--stop-on-error") { r.StopOnError = true; continue; }
        var eq = arg.IndexOf('=');
        if (eq < 0) continue;
        var key = arg.Substring(0, eq);
        var value = arg.Substring(eq + 1);
        switch (key)
        {
            case "--tenant":      r.TenantSlug = value; break;
            case "--file":        r.File = value; break;
            case "--import":      r.Import = value; break;
            case "--export-root": r.ExportRoot = value; break;
        }
    }
    return r;
}

static void PrintHelp() => Console.WriteLine("""
    Pettle data-migration tool

    Usage:
      dotnet run --project src/Pettle.MigrationTool -- --tenant=<slug> --import=<names> [options]

    Options:
      --tenant=<slug>         Tenant slug (e.g. b2m-vet-care). Resolved against Tenants.Slug.
      --import=<names>        Comma-separated importer names, OR 'all'.
                              Available: clients, pos, expenses, subscriptions, bookings, invoices, payments
                              ('all' runs them in the FK-safe order shown above.)
      --file=<path>           Override file path. Only honoured if a single importer is requested
                              (or for the legacy clients-only flow with no --import).
      --export-root=<path>    Root folder of B2M exports (defaults to repo's bulk_export_2026-05-13/...).
      --dry-run               Parse + project but do NOT persist.
      --stop-on-error         Halt on first importer that throws (default: continue).

    Examples:
      # Everything, in order:
      dotnet run --project src/Pettle.MigrationTool -- --tenant=b2m-vet-care --import=all

      # Just clients + bookings:
      dotnet run --project src/Pettle.MigrationTool -- --tenant=b2m-vet-care --import=clients,bookings

      # Custom file for clients only:
      dotnet run --project src/Pettle.MigrationTool -- --tenant=b2m-vet-care --import=clients --file=./my-clients.csv

    Connection string is read from ConnectionStrings:Postgres in appsettings.json,
    or the ConnectionStrings__Postgres environment variable.
    """);

internal class CliArgs
{
    public string? TenantSlug { get; set; }
    public string? File { get; set; }
    public string? Import { get; set; }
    public string? ExportRoot { get; set; }
    public bool DryRun { get; set; }
    public bool StopOnError { get; set; }
    public bool Help { get; set; }
}
