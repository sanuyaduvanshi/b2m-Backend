using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;
using Pettle.Api.Authorization;
using Pettle.Api.Middleware;
using Pettle.Api.Validation;
using Pettle.Application.Common.Errors;
using Pettle.Infrastructure;
using Pettle.Infrastructure.Identity;
using Pettle.Infrastructure.Persistence;
using Pettle.Infrastructure.Seed;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddPettleInfrastructure(builder.Configuration);

builder.Services.AddAuthorization();
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionHandler>();

builder.Services.AddCors(opt => opt.AddDefaultPolicy(p => p
    .WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? new[] { "http://localhost:5173" })
    .AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

builder.Services.AddValidatorsFromAssemblyContaining<Pettle.Application.Auth.IAuthService>(ServiceLifetime.Scoped);
builder.Services.AddValidatorsFromAssemblyContaining<Program>(ServiceLifetime.Scoped);

builder.Services.AddControllers(opt => opt.Filters.Add<ValidationFilter>())
    .AddJsonOptions(opt =>
    {
        // Serialize enums as their string names (e.g. "Paid", "CheckedOut") instead of integers.
        opt.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    })
    .ConfigureApiBehaviorOptions(opt =>
    {
        // Replace default ModelState 400 with our standard envelope
        opt.InvalidModelStateResponseFactory = ctx =>
        {
            var fields = ctx.ModelState
                .Where(kv => kv.Value!.Errors.Count > 0)
                .ToDictionary(
                    kv => string.IsNullOrEmpty(kv.Key) ? "_" : char.ToLowerInvariant(kv.Key[0]) + kv.Key[1..],
                    kv => kv.Value!.Errors.Select(e => string.IsNullOrEmpty(e.ErrorMessage) ? "Invalid value." : e.ErrorMessage).ToArray());
            return new BadRequestObjectResult(new { error = "validation_error", message = "Validation failed", fields });
        };
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Pettle API", Version = "v1" });
    var jwtScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste only the JWT (no 'Bearer ' prefix)."
    };
    c.AddSecurityDefinition("Bearer", jwtScheme);
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }, Array.Empty<string>() }
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PettleDbContext>();
    var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

    // Auto-apply pending EF migrations on startup so each deploy upgrades the schema
    // without a manual `dotnet ef database update`. Decoupled from seeding so it runs
    // even when seeding is disabled. Set Database:AutoMigrate=false to opt out.
    if (builder.Configuration.GetValue("Database:AutoMigrate", true))
    {
        try
        {
            await db.Database.MigrateAsync();
            app.Logger.LogInformation("Database migrations applied (or already up to date).");
        }
        catch (Exception ex) { app.Logger.LogError(ex, "Database migration failed"); }
    }

    if (builder.Configuration.GetValue("Seed:RunOnStartup", true))
    {
        try { await Seeder.SeedAsync(db, users); }
        catch (Exception ex) { app.Logger.LogError(ex, "Seeding failed"); }
    }
}

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseCors();

// Serve uploaded files (SKU images, etc.) from /app/uploads at /uploads
var uploadsPath = Path.Combine("/app/uploads");
Directory.CreateDirectory(uploadsPath);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(uploadsPath),
    RequestPath = "/uploads"
});

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/", () => Results.Redirect("/swagger"));

app.Run();
