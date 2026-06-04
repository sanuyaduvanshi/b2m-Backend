namespace Pettle.Application.Inventory;

using Pettle.Application.Clients;

public interface IInventoryService
{
    Task<PagedResult<SkuListItem>> ListSkusAsync(string? search, bool? lowStock, bool? inAppStore, int page, int pageSize, CancellationToken ct = default);
    Task<SkuListItem?> GetSkuAsync(Guid id, CancellationToken ct = default);
    Task<SkuListItem> CreateSkuAsync(CreateOrUpdateSkuRequest req, CancellationToken ct = default);
    Task<SkuListItem?> UpdateSkuAsync(Guid id, CreateOrUpdateSkuRequest req, CancellationToken ct = default);
    Task<SkuListItem?> UpdateSkuListingAsync(Guid id, UpdateSkuListingRequest req, CancellationToken ct = default);

    Task<PagedResult<VendorListItem>> ListVendorsAsync(string? search, int page, int pageSize, CancellationToken ct = default);
    Task<VendorListItem> CreateVendorAsync(CreateOrUpdateVendorRequest req, CancellationToken ct = default);
    Task<VendorListItem?> UpdateVendorAsync(Guid id, CreateOrUpdateVendorRequest req, CancellationToken ct = default);

    Task<PagedResult<PoListItem>> ListPosAsync(string? search, int page, int pageSize, CancellationToken ct = default);
    Task<PoDetail?> GetPoAsync(Guid id, CancellationToken ct = default);
    Task<PoDetail> CreatePoAsync(CreatePoRequest req, CancellationToken ct = default);
    Task<bool> ReceivePoAsync(Guid id, ReceivePoRequest req, CancellationToken ct = default);
}
