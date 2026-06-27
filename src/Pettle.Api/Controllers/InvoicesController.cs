using Microsoft.AspNetCore.Mvc;
using Pettle.Api.Authorization;
using Pettle.Application.Invoices;
using Pettle.Domain.Identity;

namespace Pettle.Api.Controllers;

[ApiController]
[Route("api/invoices")]
public class InvoicesController : ControllerBase
{
    private readonly IInvoiceService _svc;
    public InvoicesController(IInvoiceService svc) => _svc = svc;

    [HttpGet]
    [HasPermission(Modules.Invoices, Actions.View)]
    public async Task<IActionResult> List([FromQuery] InvoiceListQuery query, CancellationToken ct)
        => Ok(await _svc.ListAsync(query, ct));

    [HttpGet("{id:guid}")]
    [HasPermission(Modules.Invoices, Actions.View)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var r = await _svc.GetAsync(id, ct);
        return r is null ? NotFound() : Ok(r);
    }

    [HttpPost("sale")]
    [HasPermission(Modules.Invoices, Actions.Create)]
    public async Task<IActionResult> CreateSale([FromBody] CreateSaleRequest req, CancellationToken ct)
    {
        var r = await _svc.CreateSaleAsync(req, ct);
        return CreatedAtAction(nameof(Get), new { id = r.Id }, r);
    }

    [HttpPost("{id:guid}/payments")]
    [HasPermission(Modules.Invoices, Actions.Edit)]
    public async Task<IActionResult> RecordPayment(Guid id, [FromBody] RecordPaymentRequest req, CancellationToken ct)
    {
        var p = await _svc.RecordPaymentAsync(id, req, ct);
        return p is null ? NotFound() : Ok(p);
    }

    [HttpPut("{id:guid}/payments/{paymentId:guid}")]
    [HasPermission(Modules.Invoices, Actions.Edit)]
    public async Task<IActionResult> UpdatePayment(Guid id, Guid paymentId, [FromBody] RecordPaymentRequest req, CancellationToken ct)
    {
        var p = await _svc.UpdatePaymentAsync(id, paymentId, req, ct);
        return p is null ? NotFound() : Ok(p);
    }

    [HttpDelete("{id:guid}/payments/{paymentId:guid}")]
    [HasPermission(Modules.Invoices, Actions.Edit)]
    public async Task<IActionResult> DeletePayment(Guid id, Guid paymentId, CancellationToken ct)
        => await _svc.DeletePaymentAsync(id, paymentId, ct) ? NoContent() : NotFound();

    [HttpPost("{id:guid}/refund")]
    [HasPermission(Modules.Invoices, Actions.Approve)]
    public async Task<IActionResult> Refund(Guid id, [FromBody] RefundRequest req, CancellationToken ct)
        => await _svc.RefundAsync(id, req, ct) ? NoContent() : NotFound();

    [HttpDelete("{id:guid}")]
    [HasPermission(Modules.Invoices, Actions.Delete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => await _svc.DeleteAsync(id, ct) ? NoContent() : NotFound();
}
