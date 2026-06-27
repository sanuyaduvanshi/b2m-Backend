namespace Pettle.Application.Inventory;

using Pettle.Application.Clients;

public interface IInventoryService
{
    Task<PagedResult<SkuListItem>> ListSkusAsync(string? search, bool? lowStock, bool? inAppStore, int page, int pageSize, CancellationToken ct = default);
    Task<SkuListItem?> GetSkuAsync(Guid id, CancellationToken ct = default);
    Task<SkuListItem> CreateSkuAsync(CreateOrUpdateSkuRequest req, CancellationToken ct = default);
    Task<SkuListItem?> UpdateSkuAsync(Guid id, CreateOrUpdateSkuRequest req, CancellationToken ct = default);
    Task<SkuListItem?> UpdateSkuListingAsync(Guid id, UpdateSkuListingRequest req, CancellationToken ct = default);
    Task<bool> DeleteSkuAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<SkuCategoryDto>> ListCategoriesAsync(CancellationToken ct = default);
    Task<SkuCategoryDto> CreateCategoryAsync(CreateOrUpdateCategoryRequest req, CancellationToken ct = default);
    Task<SkuCategoryDto?> UpdateCategoryAsync(Guid id, CreateOrUpdateCategoryRequest req, CancellationToken ct = default);
    Task<bool> DeleteCategoryAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<SkuBrandDto>> ListBrandsAsync(CancellationToken ct = default);
    Task<SkuBrandDto> CreateBrandAsync(CreateOrUpdateBrandRequest req, CancellationToken ct = default);
    Task<SkuBrandDto?> UpdateBrandAsync(Guid id, CreateOrUpdateBrandRequest req, CancellationToken ct = default);
    Task<bool> DeleteBrandAsync(Guid id, CancellationToken ct = default);

    Task<PagedResult<VendorListItem>> ListVendorsAsync(string? search, int page, int pageSize, CancellationToken ct = default);
    Task<VendorListItem> CreateVendorAsync(CreateOrUpdateVendorRequest req, CancellationToken ct = default);
    Task<VendorListItem?> UpdateVendorAsync(Guid id, CreateOrUpdateVendorRequest req, CancellationToken ct = default);
    Task<bool> DeleteVendorAsync(Guid id, CancellationToken ct = default);

    Task<PagedResult<PoListItem>> ListPosAsync(string? search, int page, int pageSize, CancellationToken ct = default);
    Task<PoDetail?> GetPoAsync(Guid id, CancellationToken ct = default);
    Task<PoDetail> CreatePoAsync(CreatePoRequest req, CancellationToken ct = default);
    Task<PoDetail?> UpdatePoAsync(Guid id, CreatePoRequest req, CancellationToken ct = default);
    Task<bool> ReceivePoAsync(Guid id, ReceivePoRequest req, CancellationToken ct = default);
    Task<bool> RecordPoPaymentAsync(Guid id, RecordPoPaymentRequest req, CancellationToken ct = default);
    Task<bool> DeletePoAsync(Guid id, CancellationToken ct = default);

    Task<bool> CreateStockAdjustmentAsync(CreateStockAdjustmentRequest req, CancellationToken ct = default);
    Task<PagedResult<StockMovementDto>> ListMovementsAsync(Guid skuId, int page, int pageSize, CancellationToken ct = default);
}
