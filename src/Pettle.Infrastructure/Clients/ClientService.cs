using Microsoft.EntityFrameworkCore;
using Pettle.Application.Clients;
using Pettle.Application.Common;
using Pettle.Application.Common.Errors;
using Pettle.Domain.Bookings;
using Pettle.Domain.Clients;
using Pettle.Infrastructure.Persistence;

namespace Pettle.Infrastructure.Clients;

public class ClientService : IClientService
{
    private readonly PettleDbContext _db;
    private readonly ICurrentUser _user;

    public ClientService(PettleDbContext db, ICurrentUser user)
    {
        _db = db;
        _user = user;
    }

    public async Task<PagedResult<PetParentListItem>> ListAsync(ClientListQuery query, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return new PagedResult<PetParentListItem>(Array.Empty<PetParentListItem>(), 0, 1, query.PageSize);

        var q = _db.PetParents.AsNoTracking().Where(p => p.TenantId == _user.TenantId);

        if (query.Status.HasValue)
            q = q.Where(p => p.Status == query.Status.Value);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim().ToLower();
            q = q.Where(p =>
                p.Name.ToLower().Contains(s) ||
                p.Phone.Contains(s) ||
                (p.Email != null && p.Email.ToLower().Contains(s)) ||
                p.Pets.Any(pet => pet.Name.ToLower().Contains(s)));
        }

        q = (query.Sort?.ToLowerInvariant(), query.Desc) switch
        {
            ("phone", true) => q.OrderByDescending(p => p.Phone),
            ("phone", false) => q.OrderBy(p => p.Phone),
            ("onboarding", true) => q.OrderByDescending(p => p.OnboardingDate),
            ("onboarding", false) => q.OrderBy(p => p.OnboardingDate),
            ("outstanding", true) => q.OrderByDescending(p => p.OutstandingBalance),
            ("outstanding", false) => q.OrderBy(p => p.OutstandingBalance),
            (_, true) => q.OrderByDescending(p => p.Name),
            _ => q.OrderBy(p => p.Name)
        };

        var total = await q.CountAsync(ct);
        var page = Math.Max(query.Page, 1);
        var size = Math.Clamp(query.PageSize, 1, 200);

        var items = await q.Skip((page - 1) * size).Take(size)
            .Select(p => new PetParentListItem(
                p.Id,
                p.LegacyClientId,
                p.Name,
                p.Phone,
                p.Email,
                p.City,
                p.Pets.Count,
                p.OutstandingBalance,
                p.WalletBalance,
                p.Status,
                p.OnboardingDate,
                null,
                p.Tags.Select(t => t.ClientTag!.Name).ToList(),
                p.Pets.Where(pet => pet.Breed != null).Select(pet => pet.Breed!).Distinct().ToList()
            )).ToListAsync(ct);

        return new PagedResult<PetParentListItem>(items, total, page, size);
    }

    public async Task<PetParentDetail?> GetAsync(Guid id, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return null;
        var p = await _db.PetParents.AsNoTracking()
            .Include(x => x.Pets)
            .Include(x => x.Tags).ThenInclude(t => t.ClientTag)
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == _user.TenantId, ct);
        return p is null ? null : Map(p);
    }

    public async Task<PetParentDetail> CreateAsync(CreatePetParentRequest req, CancellationToken ct = default)
    {
        if (_user.TenantId is null) throw AppException.Forbidden();
        var phoneNorm = NormalizePhone(req.Phone);
        var dup = await _db.PetParents.IgnoreQueryFilters()
            .AnyAsync(p => p.TenantId == _user.TenantId && p.Phone == phoneNorm, ct);
        if (dup) throw AppException.Conflict("A client with this phone already exists.");

        var parent = new PetParent
        {
            Name = req.Name.Trim(),
            Phone = phoneNorm,
            Email = req.Email,
            AlternatePhone = req.AlternatePhone,
            AddressLine1 = req.AddressLine1,
            City = req.City,
            State = req.State,
            PostalCode = req.PostalCode,
            OnboardingDate = req.OnboardingDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
            TermsAccepted = req.TermsAccepted
        };
        _db.PetParents.Add(parent);
        await _db.SaveChangesAsync(ct);
        return (await GetAsync(parent.Id, ct))!;
    }

    public async Task<PetParentDetail?> UpdateAsync(Guid id, UpdatePetParentRequest req, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return null;
        var p = await _db.PetParents.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == _user.TenantId, ct);
        if (p is null) return null;

        var phoneNorm = NormalizePhone(req.Phone);
        if (phoneNorm != p.Phone)
        {
            var dup = await _db.PetParents.IgnoreQueryFilters()
                .AnyAsync(x => x.TenantId == _user.TenantId && x.Id != id && x.Phone == phoneNorm, ct);
            if (dup) throw AppException.Conflict("Another client already uses this phone number.");
        }

        p.Name = req.Name.Trim(); p.Phone = phoneNorm; p.Email = req.Email;
        p.AlternatePhone = req.AlternatePhone;
        p.AddressLine1 = req.AddressLine1; p.AddressLine2 = req.AddressLine2;
        p.City = req.City; p.State = req.State; p.Country = req.Country; p.PostalCode = req.PostalCode;
        p.Status = req.Status; p.ArchiveReason = req.ArchiveReason;

        await _db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<bool> ArchiveAsync(Guid id, string reason, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return false;
        var p = await _db.PetParents.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == _user.TenantId, ct);
        if (p is null) return false;
        p.Status = ClientStatus.Archived;
        p.ArchiveReason = reason;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return false;
        var p = await _db.PetParents.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == _user.TenantId, ct);
        if (p is null) return false;
        _db.PetParents.Remove(p);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>Normalize a phone string: strip spaces/dashes/parens, keep leading +.</summary>
    public async Task<PetSummary?> AddPetAsync(Guid parentId, CreatePetRequest req, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return null;
        var parentExists = await _db.PetParents.AnyAsync(p => p.Id == parentId && p.TenantId == _user.TenantId, ct);
        if (!parentExists) return null;
        if (string.IsNullOrWhiteSpace(req.Name))
            throw AppException.Validation("Name required", new Dictionary<string, string[]> { ["name"] = new[] { "Pet name is required." } });
        var pet = new Pet
        {
            PetParentId = parentId,
            Name = req.Name.Trim(),
            Species = req.Species,
            Breed = req.Breed?.Trim(),
            Gender = req.Gender,
            Birthday = req.Birthday,
            BreedSize = req.BreedSize,
            WeightKg = req.WeightKg,
            BirthdayReminderEnabled = req.BirthdayReminderEnabled,
        };
        _db.Pets.Add(pet);
        await _db.SaveChangesAsync(ct);
        return new PetSummary(pet.Id, null, pet.Name, pet.Species, pet.Breed, pet.Gender, pet.Birthday, pet.BreedSize, pet.WeightKg, pet.PhotoUrl, pet.BirthdayReminderEnabled);
    }

    public async Task<PetSummary?> UpdatePetAsync(Guid parentId, Guid petId, UpdatePetRequest req, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return null;
        var pet = await _db.Pets.FirstOrDefaultAsync(p => p.Id == petId && p.PetParentId == parentId && p.TenantId == _user.TenantId, ct);
        if (pet is null) return null;
        if (string.IsNullOrWhiteSpace(req.Name))
            throw AppException.Validation("Name required", new Dictionary<string, string[]> { ["name"] = new[] { "Pet name is required." } });
        pet.Name = req.Name.Trim();
        pet.Species = req.Species;
        pet.Breed = req.Breed?.Trim();
        pet.Gender = req.Gender;
        pet.Birthday = req.Birthday;
        pet.BreedSize = req.BreedSize;
        pet.WeightKg = req.WeightKg;
        pet.BirthdayReminderEnabled = req.BirthdayReminderEnabled;
        await _db.SaveChangesAsync(ct);
        return new PetSummary(pet.Id, pet.LegacyPetId, pet.Name, pet.Species, pet.Breed, pet.Gender, pet.Birthday, pet.BreedSize, pet.WeightKg, pet.PhotoUrl, pet.BirthdayReminderEnabled);
    }

    public async Task<bool> DeletePetAsync(Guid parentId, Guid petId, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return false;
        var pet = await _db.Pets.FirstOrDefaultAsync(p => p.Id == petId && p.PetParentId == parentId && p.TenantId == _user.TenantId, ct);
        if (pet is null) return false;
        var usedInBooking = await _db.BookingServices.AnyAsync(bs => bs.PetId == petId && bs.TenantId == _user.TenantId, ct);
        if (usedInBooking)
            throw AppException.Conflict("Cannot delete pet — it has booking history.");
        _db.Pets.Remove(pet);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private static string NormalizePhone(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return raw;
        var trimmed = raw.Trim();
        var leadingPlus = trimmed.StartsWith("+");
        var digits = new string(trimmed.Where(char.IsDigit).ToArray());
        return leadingPlus ? "+" + digits : digits;
    }

    private static PetParentDetail Map(PetParent p) => new(
        p.Id, p.LegacyClientId, p.Name, p.Phone, p.Email, p.AlternatePhone,
        p.AddressLine1, p.AddressLine2, p.City, p.State, p.Country, p.PostalCode,
        p.OnboardingDate, p.WalletBalance, p.OutstandingBalance, p.TermsAccepted,
        p.Status, p.ArchiveReason,
        p.Pets.Select(x => new PetSummary(x.Id, x.LegacyPetId, x.Name, x.Species, x.Breed, x.Gender, x.Birthday, x.BreedSize, x.WeightKg, x.PhotoUrl, x.BirthdayReminderEnabled)).ToList(),
        p.Tags.Select(t => t.ClientTag?.Name ?? "").Where(s => !string.IsNullOrEmpty(s)).ToList()
    );
}
