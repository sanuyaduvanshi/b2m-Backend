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
}
