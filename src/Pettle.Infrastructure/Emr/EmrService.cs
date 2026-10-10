using Microsoft.EntityFrameworkCore;
using Pettle.Application.Clients;
using Pettle.Application.Common;
using Pettle.Application.Common.Errors;
using Pettle.Application.Emr;
using Pettle.Domain.Emr;
using Pettle.Infrastructure.Persistence;

namespace Pettle.Infrastructure.Emr;

public class EmrService : IEmrService
{
    private readonly PettleDbContext _db;
    private readonly ICurrentUser _user;

    public EmrService(PettleDbContext db, ICurrentUser user) { _db = db; _user = user; }

    /// <summary>Tenant scope, plus - for Receptionist-type roles - only the records they created themselves,
    /// the same rule Bookings and Invoices apply.</summary>
    private IQueryable<EmrRecord> Scoped()
    {
        var q = _db.EmrRecords.Where(r => r.TenantId == _user.TenantId);
        if (_user.RestrictToOwnRecords) q = q.Where(r => r.CreatedById == _user.UserId);
        return q;
    }

    public async Task<PagedResult<EmrListItem>> ListAsync(string? search, Guid? petId, int page, int pageSize, CancellationToken ct = default)
    {
        var p = Math.Max(page, 1); var sz = Math.Clamp(pageSize, 1, 200);
        if (_user.TenantId is null) return new PagedResult<EmrListItem>(Array.Empty<EmrListItem>(), 0, p, sz);

        var q = Scoped().AsNoTracking();
        if (petId.HasValue) q = q.Where(r => r.PetId == petId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            q = q.Where(r => r.PetParent!.Name.ToLower().Contains(s) || r.PetParent.Phone.Contains(s)
                          || r.Pet!.Name.ToLower().Contains(s) || (r.Diagnosis != null && r.Diagnosis.ToLower().Contains(s)));
        }

        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(r => r.VisitDate).ThenByDescending(r => r.CreatedAt)
            .Skip((p - 1) * sz).Take(sz)
            .Select(r => new EmrListItem(r.Id, r.VisitDate, r.PetParentId, r.PetParent!.Name, r.PetParent.Phone,
                r.PetId, r.Pet!.Name, r.Diagnosis, r.Medicines.Count, r.DoctorName))
            .ToListAsync(ct);
        return new PagedResult<EmrListItem>(items, total, p, sz);
    }

    public async Task<EmrDetail?> GetAsync(Guid id, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return null;
        var r = await Scoped().AsNoTracking()
            .Include(x => x.PetParent).Include(x => x.Pet).Include(x => x.Medicines)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (r is null) return null;
        return new EmrDetail(r.Id, r.VisitDate, r.PetParentId, r.PetParent?.Name ?? "", r.PetParent?.Phone ?? "",
            r.PetId, r.Pet?.Name ?? "", r.Pet?.Species.ToString(), r.Pet?.Breed, r.PetWeightKg,
            r.Complaint, r.Diagnosis, r.Advice, r.NextVisitDate, r.DoctorName,
            r.Medicines.OrderBy(m => m.SortOrder)
                .Select(m => new EmrMedicineDto(m.MedicineName, m.Morning, m.Afternoon, m.Night, m.Comments)).ToList());
    }

    private async Task<Domain.Clients.Pet> RequirePetAsync(Guid petId, CancellationToken ct)
    {
        var pet = await _db.Pets.FirstOrDefaultAsync(p => p.Id == petId && p.TenantId == _user.TenantId, ct);
        return pet ?? throw AppException.Validation("Invalid pet",
            new Dictionary<string, string[]> { ["petId"] = new[] { "Pet not found in this business." } });
    }

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static void ApplyFields(EmrRecord r, CreateOrUpdateEmrRequest req, Domain.Clients.Pet pet)
    {
        r.PetId = pet.Id; r.PetParentId = pet.PetParentId;
        r.VisitDate = req.VisitDate; r.PetWeightKg = req.PetWeightKg;
        r.Complaint = Clean(req.Complaint); r.Diagnosis = Clean(req.Diagnosis); r.Advice = Clean(req.Advice);
        r.NextVisitDate = req.NextVisitDate; r.DoctorName = Clean(req.DoctorName);
        // Keep the pet's own record current, as the booking form does.
        if (req.PetWeightKg is > 0) pet.WeightKg = req.PetWeightKg;
    }

    private List<EmrMedicine> BuildMedicines(Guid recordId, IEnumerable<EmrMedicineDto>? rows) =>
        (rows ?? Array.Empty<EmrMedicineDto>()).Select((m, i) => new EmrMedicine
        {
            EmrRecordId = recordId, TenantId = _user.TenantId!.Value, SortOrder = i,
            MedicineName = m.MedicineName.Trim(), Morning = Clean(m.Morning), Afternoon = Clean(m.Afternoon),
            Night = Clean(m.Night), Comments = Clean(m.Comments),
        }).ToList();

    public async Task<EmrDetail> CreateAsync(CreateOrUpdateEmrRequest req, CancellationToken ct = default)
    {
        if (_user.TenantId is null) throw AppException.Forbidden();
        var pet = await RequirePetAsync(req.PetId, ct);
        var r = new EmrRecord();
        ApplyFields(r, req, pet);
        _db.EmrRecords.Add(r);
        _db.EmrMedicines.AddRange(BuildMedicines(r.Id, req.Medicines));
        await _db.SaveChangesAsync(ct);
        return (await GetAsync(r.Id, ct))!;
    }

    public async Task<EmrDetail?> UpdateAsync(Guid id, CreateOrUpdateEmrRequest req, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return null;
        var r = await Scoped().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (r is null) return null;
        var pet = await RequirePetAsync(req.PetId, ct);
        ApplyFields(r, req, pet);

        // Replace the prescription rows by bulk delete + standalone insert - re-adding through the tracked
        // collection trips a concurrency error under the soft-delete interceptor.
        await _db.EmrMedicines.Where(m => m.EmrRecordId == id && m.TenantId == _user.TenantId).ExecuteDeleteAsync(ct);
        _db.EmrMedicines.AddRange(BuildMedicines(id, req.Medicines));
        await _db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return false;
        var r = await Scoped().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (r is null) return false;
        _db.EmrRecords.Remove(r);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<byte[]?> GeneratePdfAsync(Guid id, CancellationToken ct = default)
    {
        var detail = await GetAsync(id, ct);
        if (detail is null) return null;
        var tenant = await _db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == _user.TenantId, ct);
        return EmrPdfRenderer.Render(detail, tenant?.Name ?? "Prescription");
    }
}
