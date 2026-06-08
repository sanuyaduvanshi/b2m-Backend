using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Pettle.Application.Common;
using Pettle.Application.Common.Errors;
using Pettle.Application.MyBusiness;
using Pettle.Domain.Clients;
using Pettle.Domain.Identity;
using Pettle.Domain.Kennels;
using Pettle.Domain.MyBusiness;
using Pettle.Domain.Tenancy;
using Pettle.Infrastructure.Identity;
using Pettle.Infrastructure.Persistence;

namespace Pettle.Infrastructure.MyBusiness;

public class MyBusinessService : IMyBusinessService
{
    private readonly PettleDbContext _db;
    private readonly ICurrentUser _user;
    private readonly UserManager<ApplicationUser> _userManager;
    public MyBusinessService(PettleDbContext db, ICurrentUser user, UserManager<ApplicationUser> userManager)
    {
        _db = db; _user = user; _userManager = userManager;
    }

    public async Task<TenantProfileDto?> GetProfileAsync(CancellationToken ct = default)
    {
        if (_user.TenantId is null) return null;
        return await _db.Tenants.AsNoTracking()
            .Where(t => t.Id == _user.TenantId)
            .Select(t => new TenantProfileDto(t.Id, t.Name, t.Slug, t.LogoUrl, t.PrimaryColor, t.SecondaryColor, t.AccentColor,
                t.Currency, t.Locale, t.TimeZone, t.IdleSessionMinutes))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<TenantProfileDto?> UpdateProfileAsync(UpdateTenantProfileRequest req, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return null;
        var t = await _db.Tenants.FirstOrDefaultAsync(x => x.Id == _user.TenantId, ct);
        if (t is null) return null;
        t.Name = req.Name; t.LogoUrl = req.LogoUrl;
        t.PrimaryColor = req.PrimaryColor; t.SecondaryColor = req.SecondaryColor; t.AccentColor = req.AccentColor;
        t.Currency = req.Currency; t.Locale = req.Locale; t.TimeZone = req.TimeZone;
        t.IdleSessionMinutes = req.IdleSessionMinutes;
        await _db.SaveChangesAsync(ct);
        return await GetProfileAsync(ct);
    }

    public async Task<IReadOnlyList<ServiceItemListItem>> ListServicesAsync(CancellationToken ct = default)
    {
        if (_user.TenantId is null) return Array.Empty<ServiceItemListItem>();
        return await _db.ServiceItems.AsNoTracking().Include(s => s.Category)
            .Where(s => s.TenantId == _user.TenantId)
            .OrderBy(s => s.Vertical).ThenBy(s => s.Name)
            .Select(s => new ServiceItemListItem(s.Id, s.Name, s.Vertical, s.Category!.Name, s.BasePrice, s.TaxPercent, s.IsActive, s.Variants.Count))
            .ToListAsync(ct);
    }

    public async Task<ServiceItemDetail?> GetServiceAsync(Guid id, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return null;
        return await _db.ServiceItems.AsNoTracking().Include(s => s.Category).Include(s => s.Variants)
            .Where(s => s.Id == id && s.TenantId == _user.TenantId)
            .Select(s => new ServiceItemDetail(s.Id, s.Name, s.Description, s.Vertical, s.CategoryId, s.Category!.Name,
                s.BasePrice, s.TaxPercent, s.TaxId, s.DurationMinutes, s.IsActive,
                s.Variants.OrderBy(v => v.Name).Select(v => new ServiceVariantDto(v.Id, v.Name, v.Price, v.SizeClass, v.Notes)).ToList()))
            .FirstOrDefaultAsync(ct);
    }

    private async Task ValidateCategoryAsync(Guid? categoryId, CancellationToken ct)
    {
        if (categoryId is null) return;
        var ok = await _db.ServiceCategories.AnyAsync(c => c.Id == categoryId && c.TenantId == _user.TenantId, ct);
        if (!ok) throw AppException.Validation("Invalid category",
            new Dictionary<string, string[]> { ["categoryId"] = new[] { "That category doesn't belong to this business." } });
    }

    public async Task<ServiceItemListItem> CreateServiceAsync(CreateOrUpdateServiceRequest req, CancellationToken ct = default)
    {
        await ValidateCategoryAsync(req.CategoryId, ct);
        var s = new ServiceItem
        {
            Name = req.Name, Description = req.Description, Vertical = req.Vertical,
            CategoryId = req.CategoryId, BasePrice = req.BasePrice, TaxPercent = req.TaxPercent,
            TaxId = req.TaxId, DurationMinutes = req.DurationMinutes, IsActive = req.IsActive
        };
        foreach (var v in req.Variants ?? Array.Empty<ServiceVariantInput>())
            s.Variants.Add(new ServiceVariant { TenantId = s.TenantId, Name = v.Name, Price = v.Price, SizeClass = v.SizeClass, Notes = v.Notes });
        _db.ServiceItems.Add(s);
        await _db.SaveChangesAsync(ct);
        return new ServiceItemListItem(s.Id, s.Name, s.Vertical, null, s.BasePrice, s.TaxPercent, s.IsActive, s.Variants.Count);
    }

    public async Task<ServiceItemListItem?> UpdateServiceAsync(Guid id, CreateOrUpdateServiceRequest req, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return null;
        var s = await _db.ServiceItems.Include(x => x.Variants).FirstOrDefaultAsync(x => x.Id == id && x.TenantId == _user.TenantId, ct);
        if (s is null) return null;
        await ValidateCategoryAsync(req.CategoryId, ct);
        s.Name = req.Name; s.Description = req.Description; s.Vertical = req.Vertical;
        s.CategoryId = req.CategoryId; s.BasePrice = req.BasePrice; s.TaxPercent = req.TaxPercent;
        s.TaxId = req.TaxId; s.DurationMinutes = req.DurationMinutes; s.IsActive = req.IsActive;

        // Replace-all variants when the caller sends a variant list; leave untouched when null.
        if (req.Variants is not null)
        {
            _db.ServiceVariants.RemoveRange(s.Variants);
            s.Variants.Clear();
            foreach (var v in req.Variants)
                s.Variants.Add(new ServiceVariant { TenantId = s.TenantId, Name = v.Name, Price = v.Price, SizeClass = v.SizeClass, Notes = v.Notes });
        }
        await _db.SaveChangesAsync(ct);
        return new ServiceItemListItem(s.Id, s.Name, s.Vertical, null, s.BasePrice, s.TaxPercent, s.IsActive, s.Variants.Count);
    }

    public async Task<bool> DeleteServiceAsync(Guid id, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return false;
        var s = await _db.ServiceItems.Include(x => x.Variants).FirstOrDefaultAsync(x => x.Id == id && x.TenantId == _user.TenantId, ct);
        if (s is null) return false;
        _db.ServiceVariants.RemoveRange(s.Variants);
        _db.ServiceItems.Remove(s);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<ServiceCategoryDto>> ListServiceCategoriesAsync(CancellationToken ct = default)
    {
        if (_user.TenantId is null) return Array.Empty<ServiceCategoryDto>();
        return await _db.ServiceCategories.AsNoTracking()
            .Where(c => c.TenantId == _user.TenantId)
            .OrderBy(c => c.Name)
            .Select(c => new ServiceCategoryDto(c.Id, c.Name, c.Description, c.IsActive,
                _db.ServiceItems.Count(s => s.CategoryId == c.Id && s.TenantId == _user.TenantId)))
            .ToListAsync(ct);
    }

    public async Task<ServiceCategoryDto> CreateServiceCategoryAsync(CreateServiceCategoryRequest req, CancellationToken ct = default)
    {
        var name = req.Name.Trim();
        var dupe = await _db.ServiceCategories.AnyAsync(c => c.TenantId == _user.TenantId && c.Name.ToLower() == name.ToLower(), ct);
        if (dupe) throw AppException.Conflict($"A category named “{name}” already exists.");
        var cat = new ServiceCategory { Name = name, Description = req.Description, IsActive = true };
        _db.ServiceCategories.Add(cat);
        await _db.SaveChangesAsync(ct);
        return new ServiceCategoryDto(cat.Id, cat.Name, cat.Description, cat.IsActive, 0);
    }

    public async Task<IReadOnlyList<StaffListItem>> ListStaffAsync(CancellationToken ct = default)
    {
        if (_user.TenantId is null) return Array.Empty<StaffListItem>();
        return await _db.Staffs.AsNoTracking()
            .Where(s => s.TenantId == _user.TenantId)
            .OrderBy(s => s.Name)
            .Select(s => new StaffListItem(s.Id, s.Name, s.RoleLabel, s.Vertical, s.Phone, s.Email, s.IsActive))
            .ToListAsync(ct);
    }

    public async Task<StaffListItem> CreateStaffAsync(CreateOrUpdateStaffRequest req, CancellationToken ct = default)
    {
        var s = new Staff
        {
            Name = req.Name, Phone = req.Phone, Email = req.Email,
            RoleLabel = req.RoleLabel, Vertical = req.Vertical, UserId = req.UserId, IsActive = req.IsActive
        };
        _db.Staffs.Add(s);
        await _db.SaveChangesAsync(ct);
        return new StaffListItem(s.Id, s.Name, s.RoleLabel, s.Vertical, s.Phone, s.Email, s.IsActive);
    }

    public async Task<StaffListItem?> UpdateStaffAsync(Guid id, CreateOrUpdateStaffRequest req, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return null;
        var s = await _db.Staffs.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == _user.TenantId, ct);
        if (s is null) return null;
        s.Name = req.Name; s.Phone = req.Phone; s.Email = req.Email;
        s.RoleLabel = req.RoleLabel; s.Vertical = req.Vertical; s.UserId = req.UserId; s.IsActive = req.IsActive;
        await _db.SaveChangesAsync(ct);
        return new StaffListItem(s.Id, s.Name, s.RoleLabel, s.Vertical, s.Phone, s.Email, s.IsActive);
    }

    public async Task<IReadOnlyList<TaxListItem>> ListTaxesAsync(CancellationToken ct = default)
    {
        if (_user.TenantId is null) return Array.Empty<TaxListItem>();
        return await _db.Taxes.AsNoTracking()
            .Where(t => t.TenantId == _user.TenantId)
            .OrderBy(t => t.Name)
            .Select(t => new TaxListItem(t.Id, t.Name, t.Kind, t.Percent, t.IsInclusive, t.EffectiveFrom, t.IsActive))
            .ToListAsync(ct);
    }

    public async Task<TaxListItem> CreateTaxAsync(CreateOrUpdateTaxRequest req, CancellationToken ct = default)
    {
        var t = new Tax
        {
            Name = req.Name, Kind = req.Kind, Percent = req.Percent, IsInclusive = req.IsInclusive,
            EffectiveFrom = req.EffectiveFrom, EffectiveTo = req.EffectiveTo, IsActive = req.IsActive
        };
        _db.Taxes.Add(t);
        await _db.SaveChangesAsync(ct);
        return new TaxListItem(t.Id, t.Name, t.Kind, t.Percent, t.IsInclusive, t.EffectiveFrom, t.IsActive);
    }

    public async Task<TaxListItem?> UpdateTaxAsync(Guid id, CreateOrUpdateTaxRequest req, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return null;
        var t = await _db.Taxes.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == _user.TenantId, ct);
        if (t is null) return null;
        t.Name = req.Name; t.Kind = req.Kind; t.Percent = req.Percent; t.IsInclusive = req.IsInclusive;
        t.EffectiveFrom = req.EffectiveFrom; t.EffectiveTo = req.EffectiveTo; t.IsActive = req.IsActive;
        await _db.SaveChangesAsync(ct);
        return new TaxListItem(t.Id, t.Name, t.Kind, t.Percent, t.IsInclusive, t.EffectiveFrom, t.IsActive);
    }

    public async Task<IReadOnlyList<ClientTagListItem>> ListClientTagsAsync(CancellationToken ct = default)
    {
        if (_user.TenantId is null) return Array.Empty<ClientTagListItem>();
        var rows = await _db.ClientTags.AsNoTracking()
            .Where(t => t.TenantId == _user.TenantId)
            .OrderBy(t => t.Name)
            .Select(t => new
            {
                t.Id, t.Name, t.Color, t.Description,
                Usage = _db.ClientTagAssignments.Count(a => a.ClientTagId == t.Id)
            })
            .ToListAsync(ct);
        return rows.Select(r => new ClientTagListItem(r.Id, r.Name, r.Color, r.Description, r.Usage)).ToList();
    }

    public async Task<ClientTagListItem> CreateClientTagAsync(CreateOrUpdateClientTagRequest req, CancellationToken ct = default)
    {
        var t = new ClientTag { Name = req.Name.Trim(), Color = req.Color, Description = req.Description };
        _db.ClientTags.Add(t);
        await _db.SaveChangesAsync(ct);
        return new ClientTagListItem(t.Id, t.Name, t.Color, t.Description, 0);
    }

    public async Task<ClientTagListItem?> UpdateClientTagAsync(Guid id, CreateOrUpdateClientTagRequest req, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return null;
        var t = await _db.ClientTags.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == _user.TenantId, ct);
        if (t is null) return null;
        t.Name = req.Name.Trim(); t.Color = req.Color; t.Description = req.Description;
        await _db.SaveChangesAsync(ct);
        var usage = await _db.ClientTagAssignments.CountAsync(a => a.ClientTagId == t.Id, ct);
        return new ClientTagListItem(t.Id, t.Name, t.Color, t.Description, usage);
    }

    public async Task<bool> DeleteClientTagAsync(Guid id, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return false;
        var t = await _db.ClientTags.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == _user.TenantId, ct);
        if (t is null) return false;
        var assignments = _db.ClientTagAssignments.Where(a => a.ClientTagId == id);
        _db.ClientTagAssignments.RemoveRange(assignments);
        _db.ClientTags.Remove(t);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ----- Access Management -----

    public async Task<IReadOnlyList<AccessUserListItem>> ListUsersAsync(CancellationToken ct = default)
    {
        if (_user.TenantId is null) return Array.Empty<AccessUserListItem>();
        var tid = _user.TenantId.Value;

        // Join UserBranches → Users for this tenant. A user may have multiple branch grants —
        // we surface the primary one (or the first if none flagged primary).
        var rows = await (
            from u in _db.Users.AsNoTracking()
            join ub in _db.UserBranches.AsNoTracking() on u.Id equals ub.UserId
            join r  in _db.AppRoles.AsNoTracking()    on ub.RoleId equals r.Id
            join b  in _db.Branches.AsNoTracking()    on ub.BranchId equals b.Id
            where ub.TenantId == tid
            orderby ub.IsPrimary descending, u.DisplayName
            select new
            {
                u.Id, u.Email, u.DisplayName, u.PhoneNumber, u.IsActive, u.CreatedAt, u.LastLoginAt,
                RoleId = r.Id, RoleName = r.Name, BranchId = b.Id, BranchName = b.Name, ub.IsPrimary
            }
        ).ToListAsync(ct);

        return rows
            .GroupBy(x => x.Id)
            .Select(g =>
            {
                var pick = g.OrderByDescending(x => x.IsPrimary).First();
                return new AccessUserListItem(
                    pick.Id, pick.Email ?? "", pick.DisplayName, pick.PhoneNumber,
                    pick.IsActive, pick.CreatedAt, pick.LastLoginAt,
                    pick.RoleId, pick.RoleName, pick.BranchId, pick.BranchName);
            })
            .OrderBy(x => x.DisplayName)
            .ToList();
    }

    public async Task<AccessLookups> GetAccessLookupsAsync(CancellationToken ct = default)
    {
        if (_user.TenantId is null) return new AccessLookups(Array.Empty<AccessRoleOption>(), Array.Empty<AccessBranchOption>());
        var tid = _user.TenantId.Value;

        var roles = await _db.AppRoles.AsNoTracking()
            .Where(r => r.TenantId == tid)
            .OrderBy(r => r.IsSystemRole ? 0 : 1).ThenBy(r => r.Name)
            .Select(r => new AccessRoleOption(r.Id, r.Name, r.Description, r.IsSystemRole))
            .ToListAsync(ct);

        var branches = await _db.Branches.AsNoTracking()
            .Where(b => b.TenantId == tid && b.IsActive)
            .OrderBy(b => b.Name)
            .Select(b => new AccessBranchOption(b.Id, b.Name))
            .ToListAsync(ct);

        return new AccessLookups(roles, branches);
    }

    public async Task<InviteUserResponse> InviteUserAsync(InviteUserRequest req, CancellationToken ct = default)
    {
        if (_user.TenantId is null) throw AppException.Forbidden();
        var tid = _user.TenantId.Value;

        // Validate role + branch belong to this tenant.
        var role = await _db.AppRoles.FirstOrDefaultAsync(r => r.Id == req.RoleId && r.TenantId == tid, ct)
                   ?? throw AppException.Validation("Invalid role",
                       new Dictionary<string, string[]> { ["roleId"] = new[] { "Role does not belong to this business." } });
        var branch = await _db.Branches.FirstOrDefaultAsync(b => b.Id == req.BranchId && b.TenantId == tid && b.IsActive, ct)
                   ?? throw AppException.Validation("Invalid branch",
                       new Dictionary<string, string[]> { ["branchId"] = new[] { "Branch does not belong to this business or is inactive." } });

        var existing = await _userManager.FindByEmailAsync(req.Email);
        if (existing is not null)
            throw AppException.Validation("User already exists",
                new Dictionary<string, string[]> { ["email"] = new[] { "A user with this email already exists." } });

        var tempPwd = GenerateTempPassword();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = req.Email,
            UserName = req.Email,
            DisplayName = req.DisplayName,
            PhoneNumber = req.PhoneNumber,
            EmailConfirmed = false,
            IsActive = true,
            DefaultTenantId = tid,
            DefaultBranchId = req.BranchId,
        };
        var create = await _userManager.CreateAsync(user, tempPwd);
        if (!create.Succeeded)
            throw AppException.Validation("Could not create user",
                new Dictionary<string, string[]> { ["_"] = create.Errors.Select(e => e.Description).ToArray() });

        _db.UserBranches.Add(new UserBranch
        {
            UserId = user.Id, BranchId = req.BranchId, RoleId = req.RoleId, TenantId = tid,
            IsPrimary = true,
        });
        await _db.SaveChangesAsync(ct);

        return new InviteUserResponse(user.Id, user.Email!, tempPwd);
    }

    public async Task<bool> SetUserActiveAsync(Guid userId, bool isActive, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return false;
        var tid = _user.TenantId.Value;
        // Ensure user belongs to this tenant
        var belongs = await _db.UserBranches.AnyAsync(x => x.UserId == userId && x.TenantId == tid, ct);
        if (!belongs) return false;
        var u = await _db.Users.FirstOrDefaultAsync(x => x.Id == userId, ct);
        if (u is null) return false;
        if (u.Id == _user.UserId && !isActive)
            throw AppException.BusinessRule("You cannot deactivate your own account.");
        u.IsActive = isActive;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> ChangeUserRoleAsync(Guid userId, UpdateUserRoleRequest req, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return false;
        var tid = _user.TenantId.Value;
        var role = await _db.AppRoles.FirstOrDefaultAsync(r => r.Id == req.RoleId && r.TenantId == tid, ct);
        if (role is null) return false;

        // Pick the user's primary UserBranch row (or any if no primary) and update its role.
        var query = _db.UserBranches.Where(x => x.UserId == userId && x.TenantId == tid);
        if (req.BranchId.HasValue) query = query.Where(x => x.BranchId == req.BranchId.Value);
        var ub = await query.OrderByDescending(x => x.IsPrimary).FirstOrDefaultAsync(ct);
        if (ub is null) return false;

        // EF needs delete + re-add since RoleId is part of the composite PK.
        var newUb = new UserBranch
        {
            UserId = ub.UserId, BranchId = ub.BranchId, TenantId = ub.TenantId,
            RoleId = role.Id, IsPrimary = ub.IsPrimary, GrantedAt = ub.GrantedAt,
        };
        _db.UserBranches.Remove(ub);
        await _db.SaveChangesAsync(ct);
        _db.UserBranches.Add(newUb);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ----- Tenant Settings (key→JSON) -----

    private static readonly HashSet<string> AllowedSettingKeys = new(StringComparer.Ordinal)
    {
        "parent-app", "push", "whatsapp", "invoice", "printer", "occasions", "inventory",
    };

    public async Task<TenantSettingDto> GetSettingAsync(string key, CancellationToken ct = default)
    {
        EnsureAllowedKey(key);
        if (_user.TenantId is null) return new TenantSettingDto(key, EmptyJsonObject(), DateTimeOffset.UtcNow);
        var s = await _db.TenantSettings.AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == _user.TenantId && x.Key == key, ct);
        if (s is null) return new TenantSettingDto(key, EmptyJsonObject(), DateTimeOffset.UtcNow);
        return new TenantSettingDto(s.Key, ParseValue(s.Value), s.UpdatedAt ?? s.CreatedAt);
    }

    public async Task<TenantSettingDto> SetSettingAsync(string key, JsonElement value, CancellationToken ct = default)
    {
        EnsureAllowedKey(key);
        if (_user.TenantId is null) throw AppException.Forbidden();
        var tid = _user.TenantId.Value;

        var s = await _db.TenantSettings.FirstOrDefaultAsync(x => x.TenantId == tid && x.Key == key, ct);
        var json = value.GetRawText();
        if (s is null)
        {
            s = new TenantSetting { TenantId = tid, Key = key, Value = json };
            _db.TenantSettings.Add(s);
        }
        else
        {
            s.Value = json;
        }
        // SaveChangesAsync override in PettleDbContext stamps UpdatedAt/UpdatedById automatically.
        await _db.SaveChangesAsync(ct);
        return new TenantSettingDto(key, ParseValue(s.Value), s.UpdatedAt ?? s.CreatedAt);
    }

    private static void EnsureAllowedKey(string key)
    {
        if (!AllowedSettingKeys.Contains(key))
            throw AppException.Validation("Unknown settings group",
                new Dictionary<string, string[]> { ["key"] = new[] { $"'{key}' is not a recognized settings group." } });
    }

    private static JsonElement EmptyJsonObject()
        => JsonDocument.Parse("{}").RootElement.Clone();

    private static JsonElement ParseValue(string json)
    {
        try { return JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json).RootElement.Clone(); }
        catch { return EmptyJsonObject(); }
    }

    // ----- Kennel Groups -----

    public async Task<IReadOnlyList<KennelGroupDto>> ListKennelGroupsAsync(CancellationToken ct = default)
    {
        if (_user.TenantId is null) return Array.Empty<KennelGroupDto>();
        return await _db.KennelGroups.AsNoTracking()
            .Where(g => g.TenantId == _user.TenantId)
            .OrderBy(g => g.SortOrder).ThenBy(g => g.Name)
            .Select(g => new KennelGroupDto(
                g.Id, g.Name, g.Color, g.SortOrder,
                g.Kennels.Where(k => !k.IsDeleted)
                    .OrderBy(k => k.SortOrder).ThenBy(k => k.Name)
                    .Select(k => new KennelUnitDto(k.Id, k.Name, k.Capacity, k.IsActive, k.SortOrder))
                    .ToList()
            ))
            .ToListAsync(ct);
    }

    public async Task<KennelGroupDto> CreateKennelGroupAsync(CreateOrUpdateKennelGroupRequest req, CancellationToken ct = default)
    {
        var g = new KennelGroup { Name = req.Name.Trim(), Color = req.Color, SortOrder = req.SortOrder ?? 0 };
        _db.KennelGroups.Add(g);
        await _db.SaveChangesAsync(ct);
        return new KennelGroupDto(g.Id, g.Name, g.Color, g.SortOrder, Array.Empty<KennelUnitDto>());
    }

    public async Task<KennelGroupDto?> UpdateKennelGroupAsync(Guid id, CreateOrUpdateKennelGroupRequest req, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return null;
        var g = await _db.KennelGroups.Include(x => x.Kennels)
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == _user.TenantId, ct);
        if (g is null) return null;
        g.Name = req.Name.Trim(); g.Color = req.Color;
        if (req.SortOrder.HasValue) g.SortOrder = req.SortOrder.Value;
        await _db.SaveChangesAsync(ct);
        return new KennelGroupDto(
            g.Id, g.Name, g.Color, g.SortOrder,
            g.Kennels.Where(k => !k.IsDeleted)
                .OrderBy(k => k.SortOrder).ThenBy(k => k.Name)
                .Select(k => new KennelUnitDto(k.Id, k.Name, k.Capacity, k.IsActive, k.SortOrder))
                .ToList());
    }

    public async Task<bool> DeleteKennelGroupAsync(Guid id, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return false;
        var g = await _db.KennelGroups.Include(x => x.Kennels)
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == _user.TenantId, ct);
        if (g is null) return false;
        // Detach units (don't hard-delete bookings/kennel rows)
        foreach (var k in g.Kennels) k.GroupId = null;
        _db.KennelGroups.Remove(g);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<KennelUnitDto?> CreateKennelUnitAsync(Guid groupId, CreateOrUpdateKennelUnitRequest req, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return null;
        var groupExists = await _db.KennelGroups.AnyAsync(x => x.Id == groupId && x.TenantId == _user.TenantId, ct);
        if (!groupExists) return null;
        var k = new Kennel
        {
            Name = req.Name.Trim(),
            Capacity = req.Capacity,
            IsActive = req.IsActive,
            GroupId = groupId,
            SortOrder = req.SortOrder ?? 0,
        };
        _db.Kennels.Add(k);
        await _db.SaveChangesAsync(ct);
        return new KennelUnitDto(k.Id, k.Name, k.Capacity, k.IsActive, k.SortOrder);
    }

    public async Task<KennelUnitDto?> UpdateKennelUnitAsync(Guid groupId, Guid unitId, CreateOrUpdateKennelUnitRequest req, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return null;
        var k = await _db.Kennels.FirstOrDefaultAsync(x => x.Id == unitId && x.GroupId == groupId && x.TenantId == _user.TenantId, ct);
        if (k is null) return null;
        k.Name = req.Name.Trim(); k.Capacity = req.Capacity; k.IsActive = req.IsActive;
        if (req.SortOrder.HasValue) k.SortOrder = req.SortOrder.Value;
        await _db.SaveChangesAsync(ct);
        return new KennelUnitDto(k.Id, k.Name, k.Capacity, k.IsActive, k.SortOrder);
    }

    public async Task<bool> DeleteKennelUnitAsync(Guid groupId, Guid unitId, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return false;
        var k = await _db.Kennels.FirstOrDefaultAsync(x => x.Id == unitId && x.GroupId == groupId && x.TenantId == _user.TenantId, ct);
        if (k is null) return false;
        k.IsDeleted = true;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ----- Intake Forms -----

    public async Task<IReadOnlyList<IntakeFormListItem>> ListIntakeFormsAsync(CancellationToken ct = default)
    {
        if (_user.TenantId is null) return Array.Empty<IntakeFormListItem>();
        var rows = await _db.IntakeForms.AsNoTracking()
            .Where(f => f.TenantId == _user.TenantId)
            .OrderBy(f => f.Type).ThenBy(f => f.Name)
            .Select(f => new { f.Id, f.Name, Type = f.Type.ToString(), f.Description, f.IsActive, f.FieldsJson })
            .ToListAsync(ct);
        return rows.Select(r => new IntakeFormListItem(r.Id, r.Name, r.Type, r.Description, r.IsActive, CountFields(r.FieldsJson))).ToList();
    }

    public async Task<IntakeFormDto?> GetIntakeFormAsync(Guid id, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return null;
        var f = await _db.IntakeForms.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == _user.TenantId, ct);
        if (f is null) return null;
        return new IntakeFormDto(f.Id, f.Name, f.Type.ToString(), f.Description, f.IsActive, ParseValue(f.FieldsJson));
    }

    public async Task<IntakeFormDto> CreateIntakeFormAsync(CreateOrUpdateIntakeFormRequest req, CancellationToken ct = default)
    {
        var f = new IntakeForm
        {
            Name = req.Name.Trim(),
            Type = ParseFormType(req.Type),
            Description = req.Description,
            IsActive = req.IsActive,
            FieldsJson = req.Fields.ValueKind == JsonValueKind.Undefined ? "[]" : req.Fields.GetRawText(),
        };
        _db.IntakeForms.Add(f);
        await _db.SaveChangesAsync(ct);
        return new IntakeFormDto(f.Id, f.Name, f.Type.ToString(), f.Description, f.IsActive, ParseValue(f.FieldsJson));
    }

    public async Task<IntakeFormDto?> UpdateIntakeFormAsync(Guid id, CreateOrUpdateIntakeFormRequest req, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return null;
        var f = await _db.IntakeForms.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == _user.TenantId, ct);
        if (f is null) return null;
        f.Name = req.Name.Trim();
        f.Type = ParseFormType(req.Type);
        f.Description = req.Description;
        f.IsActive = req.IsActive;
        f.FieldsJson = req.Fields.ValueKind == JsonValueKind.Undefined ? f.FieldsJson : req.Fields.GetRawText();
        await _db.SaveChangesAsync(ct);
        return new IntakeFormDto(f.Id, f.Name, f.Type.ToString(), f.Description, f.IsActive, ParseValue(f.FieldsJson));
    }

    public async Task<bool> DeleteIntakeFormAsync(Guid id, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return false;
        var f = await _db.IntakeForms.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == _user.TenantId, ct);
        if (f is null) return false;
        f.IsDeleted = true;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private static IntakeFormType ParseFormType(string raw) => raw?.Trim().ToLowerInvariant() switch
    {
        "boarding" => IntakeFormType.Boarding,
        "grooming" => IntakeFormType.Grooming,
        "vet" => IntakeFormType.Vet,
        "daycare" or "day care" or "day-care" => IntakeFormType.DayCare,
        "consent" => IntakeFormType.Consent,
        _ => IntakeFormType.Custom,
    };

    private static int CountFields(string json)
    {
        try { var d = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "[]" : json); return d.RootElement.ValueKind == JsonValueKind.Array ? d.RootElement.GetArrayLength() : 0; }
        catch { return 0; }
    }

    private static string GenerateTempPassword()
    {
        // 12 chars: 1 upper, 1 lower, 1 digit, 1 symbol guaranteed.
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ"; // omit I, O
        const string lower = "abcdefghijkmnpqrstuvwxyz"; // omit l, o
        const string digit = "23456789";                  // omit 0, 1
        const string symbol = "!@#$%&*?";
        var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
        var pool = upper + lower + digit + symbol;
        var chars = new char[12];
        chars[0] = Pick(upper, rng);
        chars[1] = Pick(lower, rng);
        chars[2] = Pick(digit, rng);
        chars[3] = Pick(symbol, rng);
        for (int i = 4; i < chars.Length; i++) chars[i] = Pick(pool, rng);
        // Shuffle
        for (int i = chars.Length - 1; i > 0; i--)
        {
            var jBytes = new byte[4];
            rng.GetBytes(jBytes);
            var j = (int)(BitConverter.ToUInt32(jBytes) % (uint)(i + 1));
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }
        return new string(chars);

        static char Pick(string s, System.Security.Cryptography.RandomNumberGenerator rng)
        {
            var b = new byte[4];
            rng.GetBytes(b);
            return s[(int)(BitConverter.ToUInt32(b) % (uint)s.Length)];
        }
    }
}
