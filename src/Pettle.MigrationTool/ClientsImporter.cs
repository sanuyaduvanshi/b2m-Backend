using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Pettle.Domain.Clients;
using Pettle.Infrastructure.Persistence;
using static Pettle.MigrationTool.ImportHelpers;

namespace Pettle.MigrationTool;

public class ClientsImporter
{
    // Map of CSV column name → canonical vaccine code we persist.
    // PetVaccination row is created only if the cell holds a "Yes"/date-like value.
    private static readonly (string Header, string Code, string DisplayName)[] VaccineColumns =
    {
        ("Tri Cat Vaccination", "tri_cat",       "Tri Cat"),
        ("Bordetella",          "bordetella",    "Bordetella"),
        ("FeLV",                "felv",          "FeLV"),
        ("FVRCP",               "fvrcp",         "FVRCP"),
        ("Lepto",               "lepto",         "Leptospirosis"),
        ("Anti Rabies",         "anti_rabies",   "Anti Rabies"),
        ("Corona",              "corona",        "Corona"),
        ("DHPPiL (9-in-1)",     "dhppil_9in1",   "DHPPiL (9-in-1)"),
        ("Kennel Cough",        "kennel_cough",  "Kennel Cough"),
    };

    private readonly PettleDbContext _db;
    private readonly ILogger<ClientsImporter> _log;

    public ClientsImporter(PettleDbContext db, ILogger<ClientsImporter> log)
    {
        _db = db;
        _log = log;
    }

    public async Task<ImportSummary> ImportAsync(Guid tenantId, string csvPath, bool dryRun, CancellationToken ct)
    {
        var summary = new ImportSummary();

        using var reader = new StreamReader(csvPath);
        var rows = CsvReader.Read(reader).GetEnumerator();
        if (!rows.MoveNext()) { _log.LogWarning("CSV is empty."); return summary; }

        var headers = rows.Current;
        var idx = BuildIndex(headers);

        // Preload existing LegacyPetIds + parent phone-keyed cache for idempotency.
        var existingPetIdList = await _db.Pets.IgnoreQueryFilters()
            .Where(p => p.TenantId == tenantId && p.LegacyPetId != null)
            .Select(p => p.LegacyPetId!)
            .ToListAsync(ct);
        var existingPetIds = new HashSet<string>(existingPetIdList, StringComparer.Ordinal);
        _log.LogInformation("Found {Count} pets already imported for tenant.", existingPetIds.Count);

        var existingParents = await _db.PetParents.IgnoreQueryFilters()
            .Where(p => p.TenantId == tenantId)
            .Select(p => new { p.Id, p.Phone })
            .ToListAsync(ct);
        var parentsByPhone = new Dictionary<string, Guid>(StringComparer.Ordinal);
        foreach (var p in existingParents)
            if (!string.IsNullOrEmpty(p.Phone)) parentsByPhone.TryAdd(p.Phone, p.Id);

        int lineNo = 1;
        while (rows.MoveNext())
        {
            ct.ThrowIfCancellationRequested();
            lineNo++;
            var row = rows.Current;
            if (row.All(string.IsNullOrWhiteSpace)) continue;

            string Get(string col) => idx.TryGetValue(col, out var i) && i < row.Length ? row[i] : "";

            var legacyId = Clean(Get("Pet ID"));
            if (legacyId is null)
            {
                _log.LogWarning("Line {Line}: missing Pet ID, skipped.", lineNo);
                summary.Skipped++;
                continue;
            }

            if (existingPetIds.Contains(legacyId))
            {
                summary.SkippedExisting++;
                continue;
            }

            try
            {
                var phone = NormalisePhone(Get("Phone Number"));
                var parentName = Clean(Get("Name")) ?? "(unknown)";

                Guid parentId;
                if (!string.IsNullOrEmpty(phone) && parentsByPhone.TryGetValue(phone, out var existingId))
                {
                    parentId = existingId;
                    summary.ParentsReused++;
                }
                else
                {
                    var parent = new PetParent
                    {
                        TenantId = tenantId,
                        Name = parentName,
                        Phone = phone,
                        Email = Clean(Get("Email")),
                        AddressLine1 = Clean(Get("Address")),
                        OnboardingDate = ParseDate(Get("Onboarding Date")),
                        TermsAccepted = ParseYesNo(Get("T&C Accepted")),
                        WalletBalance = ParseDecimal(Get("Wallet Balance")),
                        OutstandingBalance = ParseDecimal(Get("Outstanding Balance")),
                        Status = ParseClientStatus(Get("Status")),
                        ArchiveReason = Clean(Get("Archive Reason")),
                    };
                    _db.PetParents.Add(parent);
                    parentId = parent.Id;
                    if (!string.IsNullOrEmpty(phone)) parentsByPhone[phone] = parentId;
                    summary.ParentsCreated++;
                }

                var pet = new Pet
                {
                    TenantId = tenantId,
                    LegacyPetId = legacyId,
                    PetParentId = parentId,
                    Name = Clean(Get("Pet Name")) ?? "(unnamed)",
                    Species = ParseSpecies(Get("Pet Type")),
                    Breed = Clean(Get("Breed")),
                    Gender = ParseGender(Get("Gender")),
                    Birthday = ParseDate(Get("Pet Birthday")),
                    Coat = Clean(Get("Coat")),
                    BreedSize = ParseBreedSize(Get("Breed Size")),
                    WeightKg = TryDecimal(Get("Weight (kg)")),
                    ConsentToUsePetPhotos = ParseYesNo(Get("Consent To Use Pet Photos")),
                };
                _db.Pets.Add(pet);
                existingPetIds.Add(legacyId);
                summary.PetsCreated++;

                // Medical profile
                _db.PetMedicalProfiles.Add(new PetMedicalProfile
                {
                    TenantId = tenantId,
                    PetId = pet.Id,
                    VaccinationStatus = Clean(Get("Vaccination Status")),
                    LastTickPreventionDate = ParseDate(Get("Last Tick Prevention Date")),
                    TickPreventionMethod = Clean(Get("Tick Prevention Method")),
                    OngoingMedication = ParseYesNo(Get("Ongoing Medication")),
                    MedicationDetail = Clean(Get("Medication Detail")),
                    MajorIllnessHistory = Clean(Get("Major Illness History")),
                    DewormingDate = ParseDate(Get("Deworming Date")),
                });
                summary.MedicalProfilesCreated++;

                foreach (var (header, code, displayName) in VaccineColumns)
                {
                    var cell = Clean(Get(header));
                    if (cell is null) continue;
                    if (cell.Equals("No", StringComparison.OrdinalIgnoreCase)) continue;

                    var givenOn = ParseDate(cell);
                    var positive = givenOn != null || ParseYesNo(cell);
                    if (!positive) continue;

                    _db.PetVaccinations.Add(new PetVaccination
                    {
                        TenantId = tenantId,
                        PetId = pet.Id,
                        VaccineCode = code,
                        VaccineName = displayName,
                        GivenOn = givenOn,
                    });
                    summary.VaccinationsCreated++;
                }

                // Commit in batches so a mid-file failure preserves progress.
                if (!dryRun && summary.PetsCreated % 100 == 0)
                {
                    await _db.SaveChangesAsync(ct);
                    _db.ChangeTracker.Clear();
                    // re-prime the parent cache: SaveChanges has assigned PKs (Guid.NewGuid already set client-side, so cache stays valid).
                    _log.LogInformation("Progress: {Pets} pets imported so far.", summary.PetsCreated);
                }
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Line {Line} (Pet ID {LegacyId}): import failed.", lineNo, legacyId);
                summary.Errors++;
            }
        }

        if (!dryRun) await _db.SaveChangesAsync(ct);
        return summary;
    }

    private static decimal? TryDecimal(string? raw)
    {
        var s = Clean(raw);
        if (s is null) return null;
        s = s.Replace(",", "");
        return decimal.TryParse(s, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : null;
    }

    private static Dictionary<string, int> BuildIndex(string[] headers)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < headers.Length; i++)
        {
            var h = headers[i].Trim();
            if (h.Length > 0) map[h] = i;
        }
        return map;
    }
}

public class ImportSummary
{
    public int PetsCreated { get; set; }
    public int ParentsCreated { get; set; }
    public int ParentsReused { get; set; }
    public int MedicalProfilesCreated { get; set; }
    public int VaccinationsCreated { get; set; }
    public int SkippedExisting { get; set; }
    public int Skipped { get; set; }
    public int Errors { get; set; }

    public override string ToString()
        => $"pets={PetsCreated} parents(new={ParentsCreated},reused={ParentsReused}) medical={MedicalProfilesCreated} vaccinations={VaccinationsCreated} skippedExisting={SkippedExisting} skippedInvalid={Skipped} errors={Errors}";
}
