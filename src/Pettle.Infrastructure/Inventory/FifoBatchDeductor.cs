using Microsoft.EntityFrameworkCore;
using Pettle.Infrastructure.Persistence;

namespace Pettle.Infrastructure.Inventory;

/// <summary>
/// Single source of truth for FIFO (first received, first out) batch stock deduction, shared by
/// every code path that consumes SKU stock (POS sale, booking service SKU usage, etc.) so batch
/// order is always consistent with what the SKU "Batches" screen displays.
/// </summary>
public static class FifoBatchDeductor
{
    /// <summary>Deducts qty across a SKU's batches oldest-received-first. Returns the batch number of the first (oldest) batch consumed, for line-level traceability.</summary>
    public static async Task<string?> DeductAsync(PettleDbContext db, Guid skuId, Guid tenantId, decimal qty, CancellationToken ct)
    {
        var batches = await db.SkuBatches
            .Where(b => b.SkuId == skuId && b.TenantId == tenantId && b.QtyRemaining > 0)
            .OrderBy(b => b.ReceivedAt)
            .ToListAsync(ct);

        string? firstBatch = null;
        var remaining = qty;
        foreach (var batch in batches)
        {
            if (remaining <= 0) break;
            var take = Math.Min(batch.QtyRemaining, remaining);
            batch.QtyRemaining -= take;
            remaining -= take;
            firstBatch ??= batch.BatchNumber;
        }
        return firstBatch;
    }
}
