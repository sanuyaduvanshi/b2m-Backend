using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Pettle.Domain.Identity;
using Pettle.Domain.Tenancy;
using Pettle.Infrastructure.Identity;
using Pettle.Infrastructure.Persistence;

namespace Pettle.Infrastructure.Seed;

public static class Seeder
{
    public static async Task SeedAsync(PettleDbContext db, UserManager<ApplicationUser> users)
    {
        // Schema migration is handled at startup (Program.cs, Database:AutoMigrate) — seeding only inserts data.
        Tenant? tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Slug == "b2m-vet-care");
        if (tenant is null)
        {
            tenant = new Tenant
            {
                Name = "B2M VET CARE",
                Slug = "b2m-vet-care",
                PrimaryColor = "#4A2418",
                SecondaryColor = "#E8853D",
                AccentColor = "#4FA8B5",
                Currency = "INR",
                Locale = "en-IN",
                TimeZone = "Asia/Kolkata"
            };
            db.Tenants.Add(tenant);
            await db.SaveChangesAsync();
        }

        Branch? branch = await db.Branches.FirstOrDefaultAsync(b => b.TenantId == tenant.Id);
        if (branch is null)
        {
            branch = new Branch { TenantId = tenant.Id, Name = "Main Branch", City = "Hyderabad", Country = "India" };
            db.Branches.Add(branch);
            await db.SaveChangesAsync();
        }

        var existingRoles = await db.AppRoles.Where(r => r.TenantId == tenant.Id).ToListAsync();
        var rolesByName = existingRoles.ToDictionary(r => r.Name, r => r);

        foreach (var name in SystemRoles.All)
        {
            if (rolesByName.ContainsKey(name)) continue;
            var role = new Role
            {
                TenantId = tenant.Id, Name = name, IsSystemRole = true, Description = $"System role: {name}",
                RestrictToOwnRecords = name == SystemRoles.Receptionist
            };
            db.AppRoles.Add(role);
            rolesByName[name] = role;
        }
        await db.SaveChangesAsync();

        await EnsurePermissionsAsync(db, rolesByName);

        var ownerEmail = "owner@b2m.local";
        var owner = await users.FindByEmailAsync(ownerEmail);
        if (owner is null)
        {
            owner = new ApplicationUser
            {
                UserName = ownerEmail,
                Email = ownerEmail,
                EmailConfirmed = true,
                DisplayName = "B2M Owner",
                DefaultTenantId = tenant.Id,
                DefaultBranchId = branch.Id,
                IsActive = true
            };
            var created = await users.CreateAsync(owner, "Change!Me2026");
            if (!created.Succeeded)
                throw new InvalidOperationException(string.Join("; ", created.Errors.Select(e => e.Description)));
        }

        var ownerRole = rolesByName[SystemRoles.BusinessOwner];
        var hasGrant = await db.UserBranches.AnyAsync(ub => ub.UserId == owner.Id && ub.BranchId == branch.Id);
        if (!hasGrant)
        {
            db.UserBranches.Add(new UserBranch
            {
                UserId = owner.Id,
                BranchId = branch.Id,
                TenantId = tenant.Id,
                RoleId = ownerRole.Id,
                IsPrimary = true
            });
            await db.SaveChangesAsync();
        }
    }

    private static async Task EnsurePermissionsAsync(PettleDbContext db, Dictionary<string, Role> rolesByName)
    {
        var all = Permission.AllPermissions().ToHashSet();
        var matrix = BuildMatrix(all);
        var existing = await db.RolePermissions.ToListAsync();
        var existingSet = existing.Select(p => (p.RoleId, p.PermissionKey)).ToHashSet();

        foreach (var (roleName, perms) in matrix)
        {
            if (!rolesByName.TryGetValue(roleName, out var role)) continue;
            foreach (var p in perms)
            {
                if (!existingSet.Contains((role.Id, p)))
                {
                    db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionKey = p });
                }
            }
        }
        await db.SaveChangesAsync();
    }

    private static Dictionary<string, HashSet<string>> BuildMatrix(HashSet<string> all)
    {
        var view = all.Where(p => p.EndsWith($".{Actions.View}")).ToHashSet();
        var frontDesk = AllowFor(all, new[]
        {
            (Modules.Dashboard, new[] { Actions.View }),
            (Modules.BookingRequests, Actions.All.ToArray()),
            (Modules.BookingRecords, Actions.All.ToArray()),
            (Modules.ClientDatabase, new[] { Actions.View, Actions.Create, Actions.Edit, Actions.Export }),
            (Modules.ClientEnquiries, Actions.All.ToArray()),
            (Modules.Messages, new[] { Actions.View, Actions.Create }),
            (Modules.Reminders, new[] { Actions.View, Actions.Edit }),
            (Modules.Calendar, new[] { Actions.View, Actions.Edit }),
            (Modules.Invoices, new[] { Actions.View, Actions.Create, Actions.Edit })
        });
        var groomer = AllowFor(all, new[]
        {
            (Modules.Dashboard, new[] { Actions.View }),
            (Modules.DailyTasks, new[] { Actions.View, Actions.Edit }),
            (Modules.BookingRecords, new[] { Actions.View, Actions.Edit }),
            (Modules.Calendar, new[] { Actions.View })
        });
        var boarding = AllowFor(all, new[]
        {
            (Modules.Dashboard, new[] { Actions.View }),
            (Modules.DailyTasks, new[] { Actions.View, Actions.Edit }),
            (Modules.BookingRecords, new[] { Actions.View, Actions.Edit }),
            (Modules.Kennels, Actions.All.ToArray()),
            (Modules.Calendar, new[] { Actions.View })
        });
        var vet = AllowFor(all, new[]
        {
            (Modules.Dashboard, new[] { Actions.View }),
            (Modules.BookingRecords, new[] { Actions.View, Actions.Edit }),
            (Modules.ClientDatabase, new[] { Actions.View, Actions.Edit }),
            (Modules.DailyTasks, new[] { Actions.View, Actions.Edit }),
            (Modules.Reminders, new[] { Actions.View })
        });
        var receptionist = AllowFor(all, new[]
        {
            (Modules.Dashboard, new[] { Actions.View }),
            (Modules.BookingRequests, new[] { Actions.View, Actions.Create, Actions.Edit }),
            (Modules.BookingRecords, new[] { Actions.View, Actions.Create, Actions.Edit }),
            (Modules.ClientDatabase, new[] { Actions.View, Actions.Create, Actions.Edit }),
            (Modules.Kennels, new[] { Actions.View }),
            (Modules.Invoices, new[] { Actions.View, Actions.Create, Actions.Edit })
        });
        var accountant = AllowFor(all, new[]
        {
            (Modules.Dashboard, new[] { Actions.View }),
            (Modules.Invoices, Actions.All.ToArray()),
            (Modules.Expenses, Actions.All.ToArray()),
            (Modules.Inventory, new[] { Actions.View, Actions.Export }),
            (Modules.Reports, Actions.All.ToArray())
        });

        return new Dictionary<string, HashSet<string>>
        {
            [SystemRoles.SystemAdmin] = all,
            [SystemRoles.BusinessOwner] = all,
            [SystemRoles.BranchManager] = all.Where(p => !p.StartsWith($"{Modules.AccessManagement}.")).ToHashSet(),
            [SystemRoles.FrontDesk] = frontDesk,
            [SystemRoles.Receptionist] = receptionist,
            [SystemRoles.Groomer] = groomer,
            [SystemRoles.BoardingStaff] = boarding,
            [SystemRoles.Veterinarian] = vet,
            [SystemRoles.Accountant] = accountant,
            [SystemRoles.ReadOnly] = view
        };
    }

    private static HashSet<string> AllowFor(HashSet<string> all, (string Module, string[] Actions)[] matrix)
    {
        var set = new HashSet<string>();
        foreach (var (m, acts) in matrix)
            foreach (var a in acts)
                set.Add(Permission.Key(m, a));
        return set.Intersect(all).ToHashSet();
    }
}
