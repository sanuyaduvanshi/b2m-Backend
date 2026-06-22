using Microsoft.AspNetCore.Mvc;
using Pettle.Api.Authorization;
using Pettle.Application.Inventory;
using Pettle.Domain.Identity;

namespace Pettle.Api.Controllers;

[ApiController]
[Route("api/inventory")]
public class InventoryController : ControllerBase
{
    private readonly IInventoryService _svc;
    public InventoryController(IInventoryService svc) => _svc = svc;

    [HttpGet("categories")]
    [HasPermission(Modules.Inventory, Actions.View)]
    public async Task<IActionResult> Categories(CancellationToken ct)
        => Ok(await _svc.ListCategoriesAsync(ct));

    [HttpPost("categories")]
    [HasPermission(Modules.Inventory, Actions.Create)]
    public async Task<IActionResult> CreateCategory([FromBody] CreateOrUpdateCategoryRequest req, CancellationToken ct)
        => Ok(await _svc.CreateCategoryAsync(req, ct));

    [HttpPut("categories/{id:guid}")]
    [HasPermission(Modules.Inventory, Actions.Edit)]
    public async Task<IActionResult> UpdateCategory(Guid id, [FromBody] CreateOrUpdateCategoryRequest req, CancellationToken ct)
    {
        var r = await _svc.UpdateCategoryAsync(id, req, ct);
        return r is null ? NotFound() : Ok(r);
    }

    [HttpDelete("categories/{id:guid}")]
    [HasPermission(Modules.Inventory, Actions.Delete)]
    public async Task<IActionResult> DeleteCategory(Guid id, CancellationToken ct)
        => await _svc.DeleteCategoryAsync(id, ct) ? NoContent() : NotFound();

    [HttpGet("skus")]
    [HasPermission(Modules.Inventory, Actions.View)]
    public async Task<IActionResult> Skus([FromQuery] string? search, [FromQuery] bool? lowStock, [FromQuery] bool? inAppStore, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
        => Ok(await _svc.ListSkusAsync(search, lowStock, inAppStore, page, pageSize, ct));

    [HttpPatch("skus/{id:guid}/listing")]
    [HasPermission(Modules.Inventory, Actions.Edit)]
    public async Task<IActionResult> UpdateSkuListing(Guid id, [FromBody] UpdateSkuListingRequest req, CancellationToken ct)
    {
        var r = await _svc.UpdateSkuListingAsync(id, req, ct);
        return r is null ? NotFound() : Ok(r);
    }

    [HttpPost("skus")]
    [HasPermission(Modules.Inventory, Actions.Create)]
    public async Task<IActionResult> CreateSku([FromBody] CreateOrUpdateSkuRequest req, CancellationToken ct)
        => Ok(await _svc.CreateSkuAsync(req, ct));

    [HttpPut("skus/{id:guid}")]
    [HasPermission(Modules.Inventory, Actions.Edit)]
    public async Task<IActionResult> UpdateSku(Guid id, [FromBody] CreateOrUpdateSkuRequest req, CancellationToken ct)
    {
        var r = await _svc.UpdateSkuAsync(id, req, ct);
        return r is null ? NotFound() : Ok(r);
    }

    [HttpDelete("skus/{id:guid}")]
    [HasPermission(Modules.Inventory, Actions.Delete)]
    public async Task<IActionResult> DeleteSku(Guid id, CancellationToken ct)
        => await _svc.DeleteSkuAsync(id, ct) ? NoContent() : NotFound();

    [HttpGet("vendors")]
    [HasPermission(Modules.Inventory, Actions.View)]
    public async Task<IActionResult> Vendors([FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
        => Ok(await _svc.ListVendorsAsync(search, page, pageSize, ct));

    [HttpPost("vendors")]
    [HasPermission(Modules.Inventory, Actions.Create)]
    public async Task<IActionResult> CreateVendor([FromBody] CreateOrUpdateVendorRequest req, CancellationToken ct)
        => Ok(await _svc.CreateVendorAsync(req, ct));

    [HttpPut("vendors/{id:guid}")]
    [HasPermission(Modules.Inventory, Actions.Edit)]
    public async Task<IActionResult> UpdateVendor(Guid id, [FromBody] CreateOrUpdateVendorRequest req, CancellationToken ct)
    {
        var r = await _svc.UpdateVendorAsync(id, req, ct);
        return r is null ? NotFound() : Ok(r);
    }

    [HttpDelete("vendors/{id:guid}")]
    [HasPermission(Modules.Inventory, Actions.Delete)]
    public async Task<IActionResult> DeleteVendor(Guid id, CancellationToken ct)
        => await _svc.DeleteVendorAsync(id, ct) ? NoContent() : NotFound();

    [HttpGet("purchase-orders")]
    [HasPermission(Modules.Inventory, Actions.View)]
    public async Task<IActionResult> Pos([FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
        => Ok(await _svc.ListPosAsync(search, page, pageSize, ct));

    [HttpGet("purchase-orders/{id:guid}")]
    [HasPermission(Modules.Inventory, Actions.View)]
    public async Task<IActionResult> PoDetail(Guid id, CancellationToken ct)
    {
        var r = await _svc.GetPoAsync(id, ct);
        return r is null ? NotFound() : Ok(r);
    }

    [HttpPost("purchase-orders")]
    [HasPermission(Modules.Inventory, Actions.Create)]
    public async Task<IActionResult> CreatePo([FromBody] CreatePoRequest req, CancellationToken ct)
        => Ok(await _svc.CreatePoAsync(req, ct));

    [HttpPost("purchase-orders/{id:guid}/receive")]
    [HasPermission(Modules.Inventory, Actions.Approve)]
    public async Task<IActionResult> ReceivePo(Guid id, [FromBody] ReceivePoRequest req, CancellationToken ct)
        => await _svc.ReceivePoAsync(id, req, ct) ? NoContent() : NotFound();

    [HttpPost("purchase-orders/{id:guid}/pay")]
    [HasPermission(Modules.Inventory, Actions.Edit)]
    public async Task<IActionResult> PayPo(Guid id, [FromBody] RecordPoPaymentRequest req, CancellationToken ct)
        => await _svc.RecordPoPaymentAsync(id, req, ct) ? NoContent() : NotFound();

    [HttpDelete("purchase-orders/{id:guid}")]
    [HasPermission(Modules.Inventory, Actions.Delete)]
    public async Task<IActionResult> DeletePo(Guid id, CancellationToken ct)
        => await _svc.DeletePoAsync(id, ct) ? NoContent() : NotFound();
}
