using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Pettle.Application.Common;
using Pettle.Domain.Audit;
using Pettle.Domain.Common;

namespace Pettle.Infrastructure.Persistence;

/// <summary>Turns tracked changes into <see cref="AuditEntry"/> rows.
///
/// Deliberately selective: only the entities where "who changed this, and when" is a question
/// somebody actually asks — money, bookings, clients, stock and access control. Catalogue rows,
/// settings and the message/reminder churn are left out, and so is anything happening outside a
/// signed-in request (the seeder and the bulk importer), because a 4,000-row import writing 4,000
/// audit rows would slow the import down for a log nobody would ever read.
/// </summary>
internal static class AuditCapture
{
    /// <summary>Entity type name → the module it belongs to, as staff would name it. Membership in
    /// this map is what makes an entity auditable at all.</summary>
    private static readonly Dictionary<string, string> Auditable = new(StringComparer.Ordinal)
    {
        ["Booking"] = "Bookings",
        ["BookingService"] = "Bookings",
        ["BookingRequest"] = "Booking Requests",
        ["Invoice"] = "Invoices",
        ["Payment"] = "Invoices",
        ["PetParent"] = "Clients",
        ["Pet"] = "Clients",
        ["IssuedSubscription"] = "Subscriptions",
        ["SubscriptionPackage"] = "Subscriptions",
        ["Sku"] = "Inventory",
        ["StockMovement"] = "Inventory",
        ["PurchaseOrder"] = "Inventory",
        ["Expense"] = "Expenses",
        ["Kennel"] = "Kennels",
        ["Role"] = "Access",
        ["RolePermission"] = "Access",
        ["UserBranch"] = "Access",
    };

    /// <summary>Never worth recording — either noise on every write, or a copy of data already in
    /// the row's own columns.</summary>
    private static readonly HashSet<string> IgnoredColumns = new(StringComparer.Ordinal)
    {
        "CreatedAt", "UpdatedAt", "CreatedById", "UpdatedById", "TenantId", "BranchId",
    };

    /// <summary>Columns whose value should never be copied into the log.</summary>
    private static readonly HashSet<string> RedactedColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        "PasswordHash", "SecurityStamp", "ConcurrencyStamp", "Token", "RefreshToken",
    };

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = false };

    public static List<AuditEntry> Collect(ChangeTracker tracker, ICurrentUser? user, DateTimeOffset now)
    {
        // No signed-in actor means the seeder or the importer is running — nothing to attribute.
        if (user?.UserId is null || user.TenantId is null) return new List<AuditEntry>();

        var entries = new List<AuditEntry>();
        foreach (var entry in tracker.Entries<Entity>())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted)) continue;

            var typeName = entry.Metadata.ClrType.Name;
            if (!Auditable.TryGetValue(typeName, out var module)) continue;

            // The soft-delete interceptor rewrites a Delete into a Modified that only flips
            // IsDeleted — record it as the deletion it actually is.
            var isSoftDelete = entry.State == EntityState.Modified
                && entry.Entity is SoftDeletableTenantEntity
                && entry.Property(nameof(SoftDeletableTenantEntity.IsDeleted)).IsModified
                && entry.Property(nameof(SoftDeletableTenantEntity.IsDeleted)).CurrentValue is true;

            var action = entry.State switch
            {
                EntityState.Added => AuditAction.Create,
                EntityState.Deleted => AuditAction.Delete,
                _ => isSoftDelete ? AuditAction.Delete : AuditAction.Update,
            };

            var changed = ChangedColumns(entry, action);
            // An update that only touched bookkeeping columns isn't a change anyone did.
            if (action == AuditAction.Update && changed.Count == 0) continue;

            entries.Add(new AuditEntry
            {
                TenantId = user.TenantId.Value,
                BranchId = user.BranchId,
                EntityType = typeName,
                EntityId = entry.Entity.Id.ToString(),
                Action = action,
                Module = module,
                Summary = Describe(action, typeName, entry, changed),
                ChangedColumns = changed.Count == 0 ? null : string.Join(", ", changed),
                BeforeJson = action == AuditAction.Create ? null : Snapshot(entry, changed, original: true),
                AfterJson = action == AuditAction.Delete ? null : Snapshot(entry, changed, original: false),
                ActorUserId = user.UserId,
                ActorDisplayName = user.DisplayName ?? user.Email,
                ActorRoleName = user.RoleName,
                IpAddress = user.IpAddress,
                UserAgent = user.UserAgent,
                CreatedAt = now,
                CreatedById = user.UserId,
            });
        }
        return entries;
    }

    private static List<string> ChangedColumns(EntityEntry entry, AuditAction action)
    {
        if (action != AuditAction.Update) return new List<string>();
        return entry.Properties
            .Where(p => p.IsModified
                        && !IgnoredColumns.Contains(p.Metadata.Name)
                        && !Equals(p.OriginalValue, p.CurrentValue))
            .Select(p => p.Metadata.Name)
            .ToList();
    }

    /// <summary>For an update, only the columns that moved; for a create/delete, the whole row —
    /// minus bookkeeping and anything secret.</summary>
    private static string? Snapshot(EntityEntry entry, List<string> changed, bool original)
    {
        var props = entry.Properties.Where(p =>
            !IgnoredColumns.Contains(p.Metadata.Name)
            && !RedactedColumns.Contains(p.Metadata.Name)
            && (changed.Count == 0 || changed.Contains(p.Metadata.Name)));

        var dict = new Dictionary<string, object?>();
        foreach (var p in props)
        {
            var value = original ? p.OriginalValue : p.CurrentValue;
            if (value is null) { dict[p.Metadata.Name] = null; continue; }
            // Keep the payload small and readable — long free text is truncated, not dropped.
            dict[p.Metadata.Name] = value is string s && s.Length > 300 ? s[..300] + "…" : value;
        }
        return dict.Count == 0 ? null : JsonSerializer.Serialize(dict, Json);
    }

    /// <summary>The one-line story of the change. Uses whatever column best names the row so the
    /// log reads "Updated invoice INV-00042" rather than "Updated Invoice a5f4…".</summary>
    private static string Describe(AuditAction action, string typeName, EntityEntry entry, List<string> changed)
    {
        var verb = action switch
        {
            AuditAction.Create => "Created",
            AuditAction.Delete => "Deleted",
            _ => "Updated",
        };
        var noun = Humanise(typeName);
        var label = Label(entry);
        var what = label is null ? noun : $"{noun} {label}";
        return action == AuditAction.Update && changed.Count > 0
            ? $"{verb} {what} — changed {string.Join(", ", changed.Take(6))}{(changed.Count > 6 ? $" +{changed.Count - 6} more" : "")}"
            : $"{verb} {what}";
    }

    private static string? Label(EntityEntry entry)
    {
        foreach (var candidate in new[] { "InvoiceNumber", "PoNumber", "Name", "Title", "Description", "SkuCode", "PackageName" })
        {
            var prop = entry.Properties.FirstOrDefault(p => p.Metadata.Name == candidate);
            if (prop?.CurrentValue is string s && !string.IsNullOrWhiteSpace(s)) return s;
        }
        return null;
    }

    private static string Humanise(string typeName) => typeName switch
    {
        "BookingService" => "booking line",
        "BookingRequest" => "booking request",
        "PetParent" => "client",
        "IssuedSubscription" => "issued subscription",
        "SubscriptionPackage" => "subscription package",
        "StockMovement" => "stock movement",
        "PurchaseOrder" => "purchase order",
        "RolePermission" => "role permission",
        "UserBranch" => "user role assignment",
        "Sku" => "SKU",
        _ => typeName.ToLowerInvariant(),
    };
}
