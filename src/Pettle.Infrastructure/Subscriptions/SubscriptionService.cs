using Microsoft.EntityFrameworkCore;
using Pettle.Application.Clients;
using Pettle.Application.Common;
using Pettle.Application.Common.Errors;
using Pettle.Application.Invoices;
using Pettle.Application.Subscriptions;
using Pettle.Domain.Invoices;
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
        var packages = await _db.SubscriptionPackages.AsNoTracking()
            .Include(p => p.Services)
            .Where(p => p.TenantId == _user.TenantId)
            .OrderBy(p => p.Name)
            .ToListAsync(ct);

        var skuIds = packages.SelectMany(p => p.Services).Where(s => s.SkuId.HasValue).Select(s => s.SkuId!.Value).Distinct().ToList();
        var skuNames = skuIds.Count == 0 ? new Dictionary<Guid, string>() : await _db.Skus.AsNoTracking()
            .Where(s => skuIds.Contains(s.Id) && s.TenantId == _user.TenantId)
            .ToDictionaryAsync(s => s.Id, s => s.Name, ct);

        return packages.Select(p => ToListItem(p, skuNames)).ToList();
    }

    public async Task<PackageListItem> CreatePackageAsync(CreateOrUpdatePackageRequest req, CancellationToken ct = default)
    {
        var p = new SubscriptionPackage
        {
            Name = req.Name, Description = req.Description, ValidityDays = req.ValidityDays,
            Type = Enum.TryParse<SubscriptionPackageType>(req.Type, true, out var pt) ? pt : SubscriptionPackageType.Boarding,
            Price = req.Price, TaxPercent = req.TaxPercent, IsTaxInclusive = req.IsTaxInclusive, IsActive = req.IsActive
        };
        if (req.Services != null)
            foreach (var s in req.Services)
                p.Services.Add(MapService(s));
        _db.SubscriptionPackages.Add(p);
        await _db.SaveChangesAsync(ct);
        return await ToListItemWithSkuNamesAsync(p, ct);
    }

    public async Task<PackageListItem?> UpdatePackageAsync(Guid id, CreateOrUpdatePackageRequest req, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return null;
        // Load WITHOUT Services nav-prop so EF change tracker doesn't track old services
        var p = await _db.SubscriptionPackages
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == _user.TenantId, ct);
        if (p is null) return null;
        p.Name = req.Name; p.Description = req.Description; p.ValidityDays = req.ValidityDays;
        p.Type = Enum.TryParse<SubscriptionPackageType>(req.Type, true, out var pt) ? pt : SubscriptionPackageType.Boarding;
        p.Price = req.Price; p.TaxPercent = req.TaxPercent; p.IsTaxInclusive = req.IsTaxInclusive; p.IsActive = req.IsActive;
        // ExecuteDelete bypasses change tracker — then add new via DbSet (not nav prop)
        await _db.SubscriptionPackageServices.Where(s => s.PackageId == id).ExecuteDeleteAsync(ct);
        if (req.Services != null)
            foreach (var svc in req.Services)
            {
                var entity = MapService(svc);
                entity.PackageId = id;
                _db.SubscriptionPackageServices.Add(entity);
            }
        await _db.SaveChangesAsync(ct);
        // Reload with services for response
        var updated = await _db.SubscriptionPackages.Include(x => x.Services)
            .FirstAsync(x => x.Id == id, ct);
        return await ToListItemWithSkuNamesAsync(updated, ct);
    }

    private static SubscriptionPackageService MapService(PackageServiceItem s) => new()
    {
        ServiceName = s.ServiceName,
        Discount = s.Discount,
        DiscountType = Enum.TryParse<DiscountType>(s.DiscountType, true, out var dt) ? dt : DiscountType.Percentage,
        DaysOrSessions = s.DaysOrSessions,
        BoardingType = s.BoardingType,
        SkuCategory = s.SkuCategory,
        SkuSubCategory = s.SkuSubCategory,
        SkuId = s.SkuId,
        ItemKind = Enum.TryParse<PackageItemKind>(s.ItemKind, true, out var ik) ? ik : PackageItemKind.Service,
        AddOnCatalogueId = s.AddOnCatalogueId,
    };

    private async Task<PackageListItem> ToListItemWithSkuNamesAsync(SubscriptionPackage p, CancellationToken ct)
    {
        var skuIds = p.Services.Where(s => s.SkuId.HasValue).Select(s => s.SkuId!.Value).Distinct().ToList();
        var skuNames = skuIds.Count == 0 ? new Dictionary<Guid, string>() : await _db.Skus.AsNoTracking()
            .Where(s => skuIds.Contains(s.Id) && s.TenantId == _user.TenantId)
            .ToDictionaryAsync(s => s.Id, s => s.Name, ct);
        return ToListItem(p, skuNames);
    }

    private static PackageListItem ToListItem(SubscriptionPackage p, IReadOnlyDictionary<Guid, string> skuNames) => new(
        p.Id, p.Name, p.ValidityDays, p.Price, p.TaxPercent, p.IsTaxInclusive, p.IsActive,
        p.Services.Select(s => new PackageServiceItem(s.ServiceName, s.Discount, s.DiscountType.ToString(),
            s.DaysOrSessions, s.BoardingType, s.SkuCategory, s.SkuSubCategory, s.SkuId,
            s.SkuId.HasValue && skuNames.TryGetValue(s.SkuId.Value, out var n) ? n : null,
            s.ItemKind.ToString(), s.AddOnCatalogueId)).ToList(),
        p.Type.ToString());

    public async Task<bool> DeletePackageAsync(Guid id, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return false;
        var p = await _db.SubscriptionPackages.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == _user.TenantId, ct);
        if (p is null) return false;
        var inUse = await _db.IssuedSubscriptions.AnyAsync(i => i.PackageId == id && i.TenantId == _user.TenantId
            && i.Status != IssuedSubscriptionStatus.Cancelled && i.Status != IssuedSubscriptionStatus.Expired, ct);
        if (inUse)
            throw AppException.Conflict($"Cannot delete \"{p.Name}\" — it is issued to one or more active customers. Cancel those first, or mark the package inactive.");
        await _db.SubscriptionPackageServices.Where(s => s.PackageId == id).ExecuteDeleteAsync(ct);
        _db.SubscriptionPackages.Remove(p);
        await _db.SaveChangesAsync(ct);
        return true;
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
                i.Status, i.PaymentStatus, i.AmountPaid, i.AmountDue, i.BalanceUsed))
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
                new Dictionary<string, string[]> { ["amountPaid"] = new[] { $"Amount paid Rs.{req.AmountPaid:F2} > price Rs.{pkg.Price:F2}." } });

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
            issued.Status, issued.PaymentStatus, issued.AmountPaid, issued.AmountDue, issued.BalanceUsed);
    }

    public async Task<bool> FreezeAsync(Guid id, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return false;
        var s = await _db.IssuedSubscriptions.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == _user.TenantId, ct);
        if (s is null) return false;
        if (s.Status == IssuedSubscriptionStatus.Frozen)
            throw AppException.BusinessRule("Subscription is already frozen.");
        if (s.Status == IssuedSubscriptionStatus.Expired || s.Status == IssuedSubscriptionStatus.Cancelled)
            throw AppException.BusinessRule($"Cannot freeze a subscription that's already {s.Status.Humanize().ToLower()}.");
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

    public async Task<IssuedSubscriptionDetail?> GetIssuedAsync(Guid id, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return null;
        var s = await _db.IssuedSubscriptions.AsNoTracking()
            .Include(x => x.Package).Include(x => x.PetParent)
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == _user.TenantId, ct);
        if (s is null) return null;
        var payments = await _db.Payments.AsNoTracking()
            .Where(p => p.IssuedSubscriptionId == id && p.TenantId == _user.TenantId)
            .OrderByDescending(p => p.PaymentTime)
            .Select(p => new PaymentDto(p.Id, p.PaymentTime, p.Amount, p.Mode, p.Source, p.TransactionId, p.Type, p.Status, p.Notes))
            .ToListAsync(ct);
        return new IssuedSubscriptionDetail(s.Id, s.Package!.Name, s.PetParentId, s.PetParent!.Name, s.PetParent.Phone,
            s.IssuedOn, s.ValidUntil, s.RemainingSessions, s.TotalSessions, s.Status, s.PaymentStatus, s.AmountPaid, s.AmountDue, payments, s.BalanceUsed);
    }

    public async Task<PaymentDto?> RecordPaymentAsync(Guid issuedId, RecordSubscriptionPaymentRequest req, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return null;
        var s = await _db.IssuedSubscriptions.FirstOrDefaultAsync(x => x.Id == issuedId && x.TenantId == _user.TenantId, ct);
        if (s is null) return null;
        if (s.Status == IssuedSubscriptionStatus.Cancelled)
            throw AppException.BusinessRule("Cannot record a payment on a cancelled subscription.");
        if (req.Amount <= 0)
            throw AppException.Validation("Invalid amount",
                new Dictionary<string, string[]> { ["amount"] = new[] { "Payment amount must be greater than zero." } });
        if (req.Status == PaymentRecordStatus.Success && req.Amount > s.AmountDue + 0.01m)
            throw AppException.Validation("Payment exceeds amount due",
                new Dictionary<string, string[]> { ["amount"] = new[] { $"Payment Rs.{req.Amount:F2} exceeds amount due Rs.{s.AmountDue:F2}." } });

        var payment = new Payment
        {
            IssuedSubscriptionId = s.Id,
            PaymentTime = req.PaymentTime ?? DateTimeOffset.UtcNow,
            Amount = req.Amount, Mode = req.Mode, Source = req.Source,
            Type = req.Type, Status = req.Status, TransactionId = req.TransactionId, Notes = req.Notes,
        };
        _db.Payments.Add(payment);
        if (payment.Status == PaymentRecordStatus.Success)
        {
            s.AmountPaid += req.Amount;
            s.AmountDue = Math.Max(0, s.AmountDue - req.Amount);
            s.PaymentStatus = s.AmountDue == 0 ? IssuedPaymentStatus.Paid
                : s.AmountPaid > 0 ? IssuedPaymentStatus.PartiallyPaid : IssuedPaymentStatus.Pending;
        }
        await _db.SaveChangesAsync(ct);
        return new PaymentDto(payment.Id, payment.PaymentTime, payment.Amount, payment.Mode, payment.Source, payment.TransactionId, payment.Type, payment.Status, payment.Notes);
    }

    public async Task<bool> DeletePaymentAsync(Guid issuedId, Guid paymentId, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return false;
        var p = await _db.Payments.FirstOrDefaultAsync(x => x.Id == paymentId && x.IssuedSubscriptionId == issuedId && x.TenantId == _user.TenantId, ct);
        if (p is null) return false;
        var s = await _db.IssuedSubscriptions.FirstOrDefaultAsync(x => x.Id == issuedId && x.TenantId == _user.TenantId, ct);
        if (s is not null && p.Status == PaymentRecordStatus.Success)
        {
            s.AmountPaid = Math.Max(0, s.AmountPaid - p.Amount);
            s.AmountDue += p.Amount;
            s.PaymentStatus = s.AmountPaid <= 0 ? IssuedPaymentStatus.Pending
                : s.AmountDue == 0 ? IssuedPaymentStatus.Paid : IssuedPaymentStatus.PartiallyPaid;
        }
        _db.Payments.Remove(p);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<ActiveSubscriptionSummary?> GetActiveByClientAsync(Guid petParentId, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return null;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var sub = await _db.IssuedSubscriptions.AsNoTracking()
            .Include(s => s.Package)
            .Where(s => s.TenantId == _user.TenantId && s.PetParentId == petParentId
                && s.Status == IssuedSubscriptionStatus.Active && s.ValidUntil >= today
                && s.RemainingSessions > 0)
            .OrderByDescending(s => s.IssuedOn)
            .FirstOrDefaultAsync(ct);
        if (sub is null) return null;
        var remaining = sub.AmountPaid - sub.BalanceUsed;
        return new ActiveSubscriptionSummary(
            sub.Id, sub.Package!.Name, sub.AmountPaid, sub.BalanceUsed, Math.Max(0, remaining),
            sub.RemainingSessions, sub.TotalSessions, sub.ValidUntil, sub.Status.ToString());
    }
}
