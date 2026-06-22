using Pettle.Domain.Clients;

namespace Pettle.Application.Clients;

public interface IClientService
{
    Task<PagedResult<PetParentListItem>> ListAsync(ClientListQuery query, CancellationToken ct = default);
    Task<PetParentDetail?> GetAsync(Guid id, CancellationToken ct = default);
    Task<PetParentDetail> CreateAsync(CreatePetParentRequest req, CancellationToken ct = default);
    Task<PetParentDetail?> UpdateAsync(Guid id, UpdatePetParentRequest req, CancellationToken ct = default);
    Task<bool> ArchiveAsync(Guid id, string reason, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);

    Task<PetSummary?> AddPetAsync(Guid parentId, CreatePetRequest req, CancellationToken ct = default);
    Task<PetSummary?> UpdatePetAsync(Guid parentId, Guid petId, UpdatePetRequest req, CancellationToken ct = default);
    Task<bool> DeletePetAsync(Guid parentId, Guid petId, CancellationToken ct = default);
}
