namespace Pettle.Application.Invoices;

using Pettle.Application.Clients;

public interface IInvoiceService
{
    Task<PagedResult<InvoiceListItem>> ListAsync(InvoiceListQuery query, CancellationToken ct = default);
    Task<InvoiceDetail?> GetAsync(Guid id, CancellationToken ct = default);
    Task<InvoiceDetail> CreateSaleAsync(CreateSaleRequest req, CancellationToken ct = default);
    Task<PaymentDto?> RecordPaymentAsync(Guid invoiceId, RecordPaymentRequest req, CancellationToken ct = default);
    Task<bool> RefundAsync(Guid invoiceId, RefundRequest req, CancellationToken ct = default);
}
