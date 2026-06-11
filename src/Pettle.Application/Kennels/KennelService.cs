using Pettle.Application.Clients;
using Pettle.Domain.Kennels;

namespace Pettle.Application.Kennels;

public record KennelListItem(Guid Id, string Name, string? KennelType, string? SizeClass, int Capacity, decimal? PricePerNight, string? AllowedSpecies, bool IsActive);
public record CreateOrUpdateKennelRequest(string Name, string? KennelType, string? SizeClass, int Capacity, decimal? PricePerNight, string? AllowedSpecies, bool IsActive);

public record KennelLiveCell(
    Guid KennelId, string KennelName, string? KennelType,
    KennelLiveStatus Status,
    string? OccupantPetName, string? OccupantParentName, DateOnly? OccupiedFrom, DateOnly? OccupiedUntil,
    Guid? BookingId, Guid? BookingServiceId,
    string? BlockReason
);

public record KennelLiveGroup(
    Guid? GroupId, string GroupName, string? Color, int SortOrder,
    IReadOnlyList<KennelLiveCell> Cells
);

public record KennelTimelineSlot(
    Guid BookingServiceId, Guid BookingId, Guid KennelId,
    string PetName, string ParentName,
    DateOnly CheckIn, DateOnly CheckOut, string Status
);

public record KennelTimelineKennel(
    Guid KennelId, string KennelName,
    IReadOnlyList<KennelTimelineSlot> Slots
);

public record KennelTimelineGroup(
    Guid? GroupId, string GroupName, string? Color, int SortOrder,
    IReadOnlyList<KennelTimelineKennel> Kennels
);

public enum KennelLiveStatus { Free = 0, Occupied = 1, Blocked = 2, Reserved = 3 }

public record KennelBlockRequest(DateOnly FromDate, DateOnly ToDate, KennelBlockReason Reason, string? Notes);

public interface IKennelService
{
    Task<IReadOnlyList<KennelListItem>> ListAsync(CancellationToken ct = default);
    Task<KennelListItem> CreateAsync(CreateOrUpdateKennelRequest req, CancellationToken ct = default);
    Task<KennelListItem?> UpdateAsync(Guid id, CreateOrUpdateKennelRequest req, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<KennelLiveGroup>> LiveGridAsync(DateOnly date, CancellationToken ct = default);
    Task<IReadOnlyList<KennelTimelineGroup>> TimelineAsync(DateOnly from, DateOnly to, CancellationToken ct = default);
    Task<bool> BlockAsync(Guid kennelId, KennelBlockRequest req, CancellationToken ct = default);
    Task<bool> UnblockAsync(Guid blockingId, CancellationToken ct = default);
}
