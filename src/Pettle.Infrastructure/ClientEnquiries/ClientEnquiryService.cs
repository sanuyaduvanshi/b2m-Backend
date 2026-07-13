using Microsoft.EntityFrameworkCore;
using Pettle.Application.ClientEnquiries;
using Pettle.Application.Clients;
using Pettle.Application.Common;
using Pettle.Application.Common.Errors;
using Pettle.Domain.ClientEnquiries;
using Pettle.Domain.Clients;
using Pettle.Infrastructure.Persistence;

namespace Pettle.Infrastructure.ClientEnquiries;

public class ClientEnquiryService : IClientEnquiryService
{
    private readonly PettleDbContext _db;
    private readonly ICurrentUser _user;

    public ClientEnquiryService(PettleDbContext db, ICurrentUser user) { _db = db; _user = user; }

    public async Task<ClientEnquiryBoard> ListAsync(string? tab, string? search, string? source, int page, int pageSize, CancellationToken ct = default)
    {
        if (_user.TenantId is null)
            return new ClientEnquiryBoard(new ClientEnquiryCounts(0, 0, 0, 0),
                new PagedResult<ClientEnquiryRow>(Array.Empty<ClientEnquiryRow>(), 0, page, pageSize));

        var tid = _user.TenantId.Value;
        var q = _db.ClientEnquiries.AsNoTracking().Where(e => e.TenantId == tid);

        var counts = await q.GroupBy(e => e.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var pending = counts.FirstOrDefault(c => c.Status == EnquiryStatus.Pending)?.Count ?? 0;
        var completed = counts.FirstOrDefault(c => c.Status == EnquiryStatus.Completed)?.Count ?? 0;
        var rejected = counts.FirstOrDefault(c => c.Status == EnquiryStatus.Rejected)?.Count ?? 0;
        var board = new ClientEnquiryCounts(pending + completed + rejected, pending, completed, rejected);

        switch (tab?.ToLowerInvariant())
        {
            case "pending": q = q.Where(e => e.Status == EnquiryStatus.Pending); break;
            case "completed": q = q.Where(e => e.Status == EnquiryStatus.Completed); break;
            case "rejected": q = q.Where(e => e.Status == EnquiryStatus.Rejected); break;
            case "all":
            case null:
            case "":
                break;
        }

        if (!string.IsNullOrWhiteSpace(source) && Enum.TryParse<EnquirySource>(source, true, out var src))
            q = q.Where(e => e.Source == src);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            q = q.Where(e =>
                e.ParentName.ToLower().Contains(s) ||
                e.Phone.Contains(s) ||
                (e.PetName != null && e.PetName.ToLower().Contains(s)) ||
                (e.Email != null && e.Email.ToLower().Contains(s)));
        }

        var total = await q.CountAsync(ct);
        var p = Math.Max(page, 1);
        var sz = Math.Clamp(pageSize, 1, 200);

        var rows = await q.OrderByDescending(e => e.CreatedAt)
            .Skip((p - 1) * sz).Take(sz)
            .Select(e => new ClientEnquiryRow(
                e.Id, e.LegacyEnquiryId, e.Source, e.Status,
                e.ParentName, e.Phone, e.Email, e.PetName, e.Message,
                e.AssignedToUserId, e.AssignedToName,
                e.ResolvedAt, e.ResolvedByName,
                e.RejectionReason, e.ConvertedClientId, e.CreatedAt))
            .ToListAsync(ct);

        return new ClientEnquiryBoard(board, new PagedResult<ClientEnquiryRow>(rows, total, p, sz));
    }

    public async Task<ClientEnquiryRow?> GetAsync(Guid id, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return null;
        var e = await _db.ClientEnquiries.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == _user.TenantId, ct);
        return e is null ? null : Map(e);
    }

    public async Task<ClientEnquiryRow> CreateAsync(CreateClientEnquiryRequest req, CancellationToken ct = default)
    {
        if (_user.TenantId is null) throw AppException.Forbidden();
        var e = new ClientEnquiry
        {
            ParentName = req.ParentName.Trim(),
            Phone = NormalizePhone(req.Phone),
            Email = req.Email?.Trim(),
            PetName = req.PetName?.Trim(),
            Message = req.Message?.Trim(),
            Source = req.Source,
            Status = EnquiryStatus.Pending,
        };
        _db.ClientEnquiries.Add(e);
        await _db.SaveChangesAsync(ct);
        return Map(e);
    }

    public async Task<ClientEnquiryRow?> UpdateAsync(Guid id, UpdateClientEnquiryRequest req, CancellationToken ct = default)
    {
        if (_user.TenantId is null) throw AppException.Forbidden();
        var e = await _db.ClientEnquiries.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == _user.TenantId, ct);
        if (e is null) return null;
        if (e.Status != EnquiryStatus.Pending)
            throw AppException.BusinessRule($"This enquiry has already been {e.Status.Humanize().ToLower()} and can no longer be edited.");

        e.ParentName = req.ParentName.Trim();
        e.Phone = NormalizePhone(req.Phone);
        e.Email = req.Email?.Trim();
        e.PetName = req.PetName?.Trim();
        e.Message = req.Message?.Trim();
        e.AssignedToUserId = req.AssignedToUserId;
        e.AssignedToName = req.AssignedToName?.Trim();

        await _db.SaveChangesAsync(ct);
        return Map(e);
    }

    public async Task<bool> RejectAsync(Guid id, RejectClientEnquiryRequest req, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return false;
        var e = await _db.ClientEnquiries.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == _user.TenantId, ct);
        if (e is null) return false;
        if (e.Status == EnquiryStatus.Completed)
            throw AppException.BusinessRule("Cannot reject a completed (converted) enquiry.");

        e.Status = EnquiryStatus.Rejected;
        e.RejectionReason = req.Reason.Trim();
        e.ResolvedAt = DateTimeOffset.UtcNow;
        e.ResolvedByUserId = _user.UserId;
        e.ResolvedByName = _user.DisplayName ?? _user.Email;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<ConvertEnquiryResult?> ConvertToClientAsync(Guid id, ConvertEnquiryRequest req, CancellationToken ct = default)
    {
        if (_user.TenantId is null) throw AppException.Forbidden();
        var e = await _db.ClientEnquiries.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == _user.TenantId, ct);
        if (e is null) return null;
        if (e.Status == EnquiryStatus.Completed)
            throw AppException.BusinessRule("This enquiry has already been converted.");
        if (e.Status == EnquiryStatus.Rejected)
            throw AppException.BusinessRule("Cannot convert a rejected enquiry — reopen it first.");

        var phone = NormalizePhone(req.Phone);

        // Reuse existing parent on phone match (within tenant); avoids duplicates from web-form double-submits.
        var existing = await _db.PetParents
            .FirstOrDefaultAsync(p => p.TenantId == _user.TenantId && p.Phone == phone, ct);

        PetParent parent;
        if (existing is not null)
        {
            parent = existing;
        }
        else
        {
            parent = new PetParent
            {
                Name = req.Name.Trim(),
                Phone = phone,
                Email = req.Email?.Trim(),
                AlternatePhone = string.IsNullOrWhiteSpace(req.AlternatePhone) ? null : NormalizePhone(req.AlternatePhone!),
                AddressLine1 = req.AddressLine1?.Trim(),
                City = req.City?.Trim(),
                State = req.State?.Trim(),
                PostalCode = req.PostalCode?.Trim(),
                OnboardingDate = req.OnboardingDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
                TermsAccepted = req.TermsAccepted,
                Status = ClientStatus.Active,
            };
            _db.PetParents.Add(parent);
        }

        e.Status = EnquiryStatus.Completed;
        e.ConvertedClientId = parent.Id;
        e.ResolvedAt = DateTimeOffset.UtcNow;
        e.ResolvedByUserId = _user.UserId;
        e.ResolvedByName = _user.DisplayName ?? _user.Email;

        await _db.SaveChangesAsync(ct);
        return new ConvertEnquiryResult(e.Id, parent.Id);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return false;
        var e = await _db.ClientEnquiries.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == _user.TenantId, ct);
        if (e is null) return false;
        if (e.Status == EnquiryStatus.Completed)
            throw AppException.BusinessRule("Converted enquiries cannot be deleted (audit trail).");
        _db.ClientEnquiries.Remove(e);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private static ClientEnquiryRow Map(ClientEnquiry e) => new(
        e.Id, e.LegacyEnquiryId, e.Source, e.Status,
        e.ParentName, e.Phone, e.Email, e.PetName, e.Message,
        e.AssignedToUserId, e.AssignedToName,
        e.ResolvedAt, e.ResolvedByName,
        e.RejectionReason, e.ConvertedClientId, e.CreatedAt);

    private static string NormalizePhone(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return raw;
        var trimmed = raw.Trim();
        var leadingPlus = trimmed.StartsWith("+");
        var digits = new string(trimmed.Where(char.IsDigit).ToArray());
        return leadingPlus ? "+" + digits : digits;
    }
}
