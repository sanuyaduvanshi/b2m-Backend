using Pettle.Domain.Clients;

namespace Pettle.Application.Clients;

public interface IClientService
{
    Task<PagedResult<PetParentListItem>> ListAsync(ClientListQuery query, CancellationToken ct = default);
    /// <summary>Every client matching the same query ListAsync was given, unpaginated — so a
    /// download is exactly the table the user is looking at, in the same order, rather than a
    /// differently-filtered set that happens to share a name.</summary>
    Task<IReadOnlyList<PetParentListItem>> ExportAsync(ClientListQuery query, CancellationToken ct = default);
    /// <summary>The same rows as an Excel workbook — a Summary sheet of totals and a formatted
    /// Clients sheet. A CSV can hold neither a second sheet nor any formatting, so the totals had
    /// to sit on top of the data where Excel's sort and filter treat them as rows.</summary>
    Task<byte[]> ExportWorkbookAsync(ClientListQuery query, CancellationToken ct = default);
    Task<PetParentDetail?> GetAsync(Guid id, CancellationToken ct = default);
    Task<PetParentDetail> CreateAsync(CreatePetParentRequest req, CancellationToken ct = default);
    Task<PetParentDetail?> UpdateAsync(Guid id, UpdatePetParentRequest req, CancellationToken ct = default);
    Task<bool> ArchiveAsync(Guid id, string reason, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);

    Task<PetSummary?> AddPetAsync(Guid parentId, CreatePetRequest req, CancellationToken ct = default);
    Task<PetSummary?> UpdatePetAsync(Guid parentId, Guid petId, UpdatePetRequest req, CancellationToken ct = default);
    Task<bool> DeletePetAsync(Guid parentId, Guid petId, CancellationToken ct = default);
}
