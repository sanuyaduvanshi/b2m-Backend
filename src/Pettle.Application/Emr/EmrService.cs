using Pettle.Application.Clients;

namespace Pettle.Application.Emr;

public record EmrMedicineDto(string MedicineName, string? Morning, string? Afternoon, string? Night, string? Comments);

public record EmrListItem(
    Guid Id, DateOnly VisitDate, Guid PetParentId, string ParentName, string ParentPhone,
    Guid PetId, string PetName, string? Diagnosis, int MedicineCount, string? DoctorName);

public record EmrDetail(
    Guid Id, DateOnly VisitDate, Guid PetParentId, string ParentName, string ParentPhone,
    Guid PetId, string PetName, string? PetSpecies, string? PetBreed, decimal? PetWeightKg,
    string? Complaint, string? Diagnosis, string? Advice, DateOnly? NextVisitDate, string? DoctorName,
    IReadOnlyList<EmrMedicineDto> Medicines);

public record CreateOrUpdateEmrRequest(
    Guid PetId, DateOnly VisitDate, decimal? PetWeightKg, string? Complaint, string? Diagnosis, string? Advice,
    DateOnly? NextVisitDate, string? DoctorName, List<EmrMedicineDto>? Medicines);

public interface IEmrService
{
    Task<PagedResult<EmrListItem>> ListAsync(string? search, Guid? petId, int page, int pageSize, CancellationToken ct = default);
    Task<EmrDetail?> GetAsync(Guid id, CancellationToken ct = default);
    Task<EmrDetail> CreateAsync(CreateOrUpdateEmrRequest req, CancellationToken ct = default);
    Task<EmrDetail?> UpdateAsync(Guid id, CreateOrUpdateEmrRequest req, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
    Task<byte[]?> GeneratePdfAsync(Guid id, CancellationToken ct = default);
}
