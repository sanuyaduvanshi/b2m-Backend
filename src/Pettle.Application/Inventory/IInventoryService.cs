namespace Pettle.Application.Inventory;

using Pettle.Application.Clients;

public interface IInventoryService
{
    Task<PagedResult<SkuListItem>> ListSkusAsync(string? search, bool? lowStock, bool? inAppStore, Guid? categoryId, int page, int pageSize, bool withVariants = false, CancellationToken ct = default);
    /// <summary>Same filters as ListSkusAsync but every match, unpaginated — backs the SKU table's
    /// "Download Report" so the export matches exactly what's on screen, not a separate dataset.</summary>
    Task<IReadOnlyList<SkuListItem>> ExportSkusAsync(string? search, bool? lowStock, Guid? categoryId, CancellationToken ct = default);
    Task<SkuListItem?> GetSkuAsync(Guid id, CancellationToken ct = default);
    Task<SkuListItem> CreateSkuAsync(CreateOrUpdateSkuRequest req, CancellationToken ct = default);
    Task<SkuListItem?> UpdateSkuAsync(Guid id, CreateOrUpdateSkuRequest req, CancellationToken ct = default);
    Task<SkuListItem?> UpdateSkuListingAsync(Guid id, UpdateSkuListingRequest req, CancellationToken ct = default);
    Task<bool> DeleteSkuAsync(Guid id, CancellationToken ct = default);

    Task<PagedResult<ProductListItem>> ListProductsAsync(string? search, int page, int pageSize, CancellationToken ct = default);
    Task<ProductListItem> CreateProductAsync(CreateOrUpdateProductRequest req, CancellationToken ct = default);
    Task<ProductListItem?> UpdateProductAsync(Guid id, CreateOrUpdateProductRequest req, CancellationToken ct = default);
    Task<bool> DeleteProductAsync(Guid id, CancellationToken ct = default);

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
    /// <summary>Same filter as ListPosAsync but every match, unpaginated — backs the Purchase
    /// Order table's "Download Report".</summary>
    Task<IReadOnlyList<PoListItem>> ExportPosAsync(string? search, CancellationToken ct = default);
    Task<PoDetail?> GetPoAsync(Guid id, CancellationToken ct = default);
    Task<PoDetail> CreatePoAsync(CreatePoRequest req, CancellationToken ct = default);
    Task<PoDetail?> UpdatePoAsync(Guid id, CreatePoRequest req, CancellationToken ct = default);
    Task<bool> ReceivePoAsync(Guid id, ReceivePoRequest req, CancellationToken ct = default);
    Task<bool> RecordPoPaymentAsync(Guid id, RecordPoPaymentRequest req, CancellationToken ct = default);
    Task<bool> DeletePoAsync(Guid id, bool force = false, CancellationToken ct = default);

    // --- Debit notes (purchase returns) ---
    Task<PagedResult<PoListItem>> ListDebitNotesAsync(string? search, int page, int pageSize, CancellationToken ct = default);
    Task<PoDetail?> GetDebitNoteAsync(Guid id, CancellationToken ct = default);
    /// <summary>Raises the return. Takes the stock back out — from the batch it came in on where
    /// one is named — and never lets more go back than actually came in and is still on hand.</summary>
    Task<PoDetail> CreateDebitNoteAsync(CreateDebitNoteRequest req, CancellationToken ct = default);
    /// <summary>Bills for this supplier that still have something returnable, with per-line
    /// quantities already net of earlier returns — backs the "Select Purchase" picker.</summary>
    Task<IReadOnlyList<ReturnablePurchase>> ListReturnablePurchasesAsync(Guid vendorId, CancellationToken ct = default);
    /// <summary>What each supplier is owed once debit notes are taken off.</summary>
    Task<IReadOnlyList<VendorBalance>> VendorBalancesAsync(CancellationToken ct = default);

    Task<bool> CreateStockAdjustmentAsync(CreateStockAdjustmentRequest req, CancellationToken ct = default);
    Task<PagedResult<StockMovementDto>> ListMovementsAsync(Guid skuId, string? reason, int page, int pageSize, CancellationToken ct = default);
    /// <summary>Every stock movement across all SKUs within the date range, oldest first (the
    /// same FIFO order stock is actually deducted in) — backs the SKU tab's report download.</summary>
    Task<IReadOnlyList<StockMovementDto>> ExportMovementsAsync(DateOnly from, DateOnly to, CancellationToken ct = default);
    Task<IReadOnlyList<SkuBatchDto>> ListBatchesAsync(Guid skuId, CancellationToken ct = default);
}
