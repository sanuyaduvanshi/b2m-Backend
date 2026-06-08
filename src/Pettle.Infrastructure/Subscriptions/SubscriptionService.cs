using Microsoft.EntityFrameworkCore;
using Pettle.Application.Clients;
using Pettle.Application.Common;
using Pettle.Application.Common.Errors;
using Pettle.Application.Subscriptions;
using Pettle.Domain.Subscriptions;
using Pettle.Infrastructure.Persistence;

namespace Pettle.Infrastructure.Subscriptions;

public class SubscriptionService : ISubscriptionService
{
    private readonly PettleDbContext _db;
    private readonly ICurrentUser _user;
    public SubscriptionService(PettleDbContext db, ICurrentUser user) { _db = db; _user = user; }

    public async Task<IReadOnlyList<PackageListItem>> ListPackagesAsync(CancellationToken ct = default)
    {
        if (_user.TenantId is null) return Array.Empty<PackageListItem>();
        return await _db.SubscriptionPackages.AsNoTracking()
            .Where(p => p.TenantId == _user.TenantId)
            .OrderBy(p => p.Name)
            .Select(p => new PackageListItem(p.Id, p.Name, p.ValidityDays, p.Price, p.TaxPercent, p.IsTaxInclusive, p.IsActive))
            .ToListAsync(ct);
    }

    public async Task<PackageListItem> CreatePackageAsync(CreateOrUpdatePackageRequest req, CancellationToken ct = default)
    {
        var p = new SubscriptionPackage
        {
            Name = req.Name, Description = req.Description, ValidityDays = req.ValidityDays,
            Price = req.Price, TaxPercent = req.TaxPercent, IsTaxInclusive = req.IsTaxInclusive, IsActive = req.IsActive
        };
        _db.SubscriptionPackages.Add(p);
        await _db.SaveChangesAsync(ct);
        return new PackageListItem(p.Id, p.Name, p.ValidityDays, p.Price, p.TaxPercent, p.IsTaxInclusive, p.IsActive);
    }

    public async Task<PackageListItem?> UpdatePackageAsync(Guid id, CreateOrUpdatePackageRequest req, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return null;
        var p = await _db.SubscriptionPackages.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == _user.TenantId, ct);
        if (p is null) return null;
        p.Name = req.Name; p.Description = req.Description; p.ValidityDays = req.ValidityDays;
        p.Price = req.Price; p.TaxPercent = req.TaxPercent; p.IsTaxInclusive = req.IsTaxInclusive; p.IsActive = req.IsActive;
        await _db.SaveChangesAsync(ct);
        return new PackageListItem(p.Id, p.Name, p.ValidityDays, p.Price, p.TaxPercent, p.IsTaxInclusive, p.IsActive);
    }

    public async Task<PagedResult<IssuedListItem>> ListIssuedAsync(string? search, IssuedSubscriptionStatus? status, int page, int pageSize, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return new PagedResult<IssuedListItem>(Array.Empty<IssuedListItem>(), 0, page, pageSize);
        var q = _db.IssuedSubscriptions.AsNoTracking()
            .Include(i => i.Package).Include(i => i.PetParent)
            .Where(i => i.TenantId == _user.TenantId);
        if (status.HasValue) q = q.Where(i => i.Status == status.Value);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            q = q.Where(i => i.PetParent!.Name.ToLower().Contains(s) || i.PetParent!.Phone.Contains(s) || i.Package!.Name.ToLower().Contains(s));
        }
        var total = await q.CountAsync(ct);
        var p = Math.Max(page, 1); var sz = Math.Clamp(pageSize, 1, 200);
        var items = await q.OrderByDescending(i => i.IssuedOn).Skip((p - 1) * sz).Take(sz)
            .Select(i => new IssuedListItem(
                i.Id, i.Package!.Name, i.PetParentId, i.PetParent!.Name, i.PetParent!.Phone,
                i.IssuedOn, i.ValidUntil, i.RemainingSessions, i.TotalSessions,
                i.Status, i.PaymentStatus, i.AmountPaid, i.AmountDue))
            .ToListAsync(ct);
        return new PagedResult<IssuedListItem>(items, total, p, sz);
    }

    public async Task<IssuedListItem> IssueAsync(IssueSubscriptionRequest req, CancellationToken ct = default)
    {
        if (_user.TenantId is null) throw AppException.Forbidden();
        var pkg = await _db.SubscriptionPackages.FirstOrDefaultAsync(p => p.Id == req.PackageId && p.TenantId == _user.TenantId, ct)
            ?? throw AppException.Validation("Invalid package",
                new Dictionary<string, string[]> { ["packageId"] = new[] { "Package not found in this business." } });
        if (!pkg.IsActive)
            throw AppException.BusinessRule("This package is inactive and cannot be issued.");
        var parent = await _db.PetParents.FirstOrDefaultAsync(p => p.Id == req.PetParentId && p.TenantId == _user.TenantId, ct)
            ?? throw AppException.Validation("Invalid pet parent",
                new Dictionary<string, string[]> { ["petParentId"] = new[] { "Pet parent not found in this business." } });
        if (req.AmountPaid > pkg.Price + 0.01m)
            throw AppException.Validation("Amount paid exceeds package price",
                new Dictionary<string, string[]> { ["amountPaid"] = new[] { $"Amount paid ₹{req.AmountPaid:F2} > price ₹{pkg.Price:F2}." } });

        var issued = new IssuedSubscription
        {
            PackageId = pkg.Id,
            PetParentId = parent.Id,
            IssuedOn = req.IssuedOn ?? DateOnly.FromDateTime(DateTime.UtcNow),
            ValidUntil = (req.IssuedOn ?? DateOnly.FromDateTime(DateTime.UtcNow)).AddDays(pkg.ValidityDays),
            TotalSessions = req.TotalSessions,
            RemainingSessions = req.TotalSessions,
            AmountPaid = req.AmountPaid,
            AmountDue = Math.Max(0, pkg.Price - req.AmountPaid),
            PaymentStatus = req.AmountPaid >= pkg.Price ? IssuedPaymentStatus.Paid : req.AmountPaid > 0 ? IssuedPaymentStatus.PartiallyPaid : IssuedPaymentStatus.Pending
        };
        _db.IssuedSubscriptions.Add(issued);
        await _db.SaveChangesAsync(ct);
        return new IssuedListItem(issued.Id, pkg.Name, parent.Id, parent.Name, parent.Phone,
            issued.IssuedOn, issued.ValidUntil, issued.RemainingSessions, issued.TotalSessions,
            issued.Status, issued.PaymentStatus, issued.AmountPaid, issued.AmountDue);
    }

    public async Task<bool> FreezeAsync(Guid id, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return false;
        var s = await _db.IssuedSubscriptions.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == _user.TenantId, ct);
        if (s is null) return false;
        if (s.Status == IssuedSubscriptionStatus.Frozen)
            throw AppException.BusinessRule("Subscription is already frozen.");
        if (s.Status == IssuedSubscriptionStatus.Expired || s.Status == IssuedSubscriptionStatus.Cancelled)
            throw AppException.BusinessRule($"Cannot freeze a {s.Status} subscription.");
        s.Status = IssuedSubscriptionStatus.Frozen;
        s.FrozenAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> CancelAsync(Guid id, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return false;
        var s = await _db.IssuedSubscriptions.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == _user.TenantId, ct);
        if (s is null) return false;
        if (s.Status == IssuedSubscriptionStatus.Cancelled)
            throw AppException.BusinessRule("Subscription is already cancelled.");
        if (s.Status == IssuedSubscriptionStatus.Expired)
            throw AppException.BusinessRule("Cannot cancel an expired subscription.");
        s.Status = IssuedSubscriptionStatus.Cancelled;
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
