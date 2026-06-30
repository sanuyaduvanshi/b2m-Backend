using Xunit;

namespace Pettle.Tests.Inventory;

/// <summary>
/// Pure-logic tests for FEFO (First Expiry First Out) batch deduction.
/// Mirrors the algorithm in InvoiceService.DeductFefoAsync and InventoryService.
/// </summary>
public class FifoTests
{
    // Represents one in-memory batch
    private record Batch(DateOnly? Expiry, decimal Qty, DateTime ReceivedAt)
    {
        public decimal QtyRemaining { get; set; } = Qty;
    }

    /// <summary>Deduct qty from batches sorted FEFO (null expiry last, then oldest received).</summary>
    private static decimal Deduct(List<Batch> batches, decimal qty)
    {
        var sorted = batches
            .Where(b => b.QtyRemaining > 0)
            .OrderBy(b => b.Expiry == null)
            .ThenBy(b => b.Expiry)
            .ThenBy(b => b.ReceivedAt)
            .ToList();

        decimal remaining = qty;
        foreach (var b in sorted)
        {
            if (remaining <= 0) break;
            var take = Math.Min(b.QtyRemaining, remaining);
            b.QtyRemaining -= take;
            remaining -= take;
        }
        return remaining; // 0 means fully satisfied
    }

    [Fact]
    public void SingleBatch_FullyCovers_Qty()
    {
        var batches = new List<Batch> { new(new DateOnly(2025, 12, 31), 100, DateTime.UtcNow) };
        var unmet = Deduct(batches, 30);
        Assert.Equal(0, unmet);
        Assert.Equal(70, batches[0].QtyRemaining);
    }

    [Fact]
    public void EarlierExpiry_DeductedFirst()
    {
        var early = new Batch(new DateOnly(2025, 6, 30), 10, DateTime.UtcNow.AddDays(-5));
        var late = new Batch(new DateOnly(2026, 6, 30), 50, DateTime.UtcNow);
        var batches = new List<Batch> { late, early }; // deliberately reversed order

        Deduct(batches, 10);

        Assert.Equal(0, early.QtyRemaining);   // early fully consumed
        Assert.Equal(50, late.QtyRemaining);   // late untouched
    }

    [Fact]
    public void SpansMultipleBatches_FEFO()
    {
        var b1 = new Batch(new DateOnly(2025, 6, 1), 5, DateTime.UtcNow.AddDays(-10));
        var b2 = new Batch(new DateOnly(2025, 9, 1), 5, DateTime.UtcNow.AddDays(-5));
        var b3 = new Batch(new DateOnly(2026, 1, 1), 20, DateTime.UtcNow);
        var batches = new List<Batch> { b3, b1, b2 };

        var unmet = Deduct(batches, 12);

        Assert.Equal(0, unmet);
        Assert.Equal(0, b1.QtyRemaining);  // 5 taken
        Assert.Equal(0, b2.QtyRemaining);  // 5 taken
        Assert.Equal(18, b3.QtyRemaining); // 2 taken
    }

    [Fact]
    public void NullExpiry_UsedLast_AfterDatedBatches()
    {
        var dated = new Batch(new DateOnly(2026, 3, 1), 10, DateTime.UtcNow.AddDays(-2));
        var noExpiry = new Batch(null, 50, DateTime.UtcNow.AddDays(-100)); // older but no expiry
        var batches = new List<Batch> { noExpiry, dated };

        Deduct(batches, 10);

        Assert.Equal(0, dated.QtyRemaining);     // dated consumed first
        Assert.Equal(50, noExpiry.QtyRemaining); // no-expiry untouched
    }

    [Fact]
    public void NullExpiry_FIFO_WhenMultipleUndated()
    {
        var older = new Batch(null, 5, new DateTime(2025, 1, 1));
        var newer = new Batch(null, 5, new DateTime(2025, 6, 1));
        var batches = new List<Batch> { newer, older };

        Deduct(batches, 5);

        Assert.Equal(0, older.QtyRemaining);  // FIFO: oldest received first
        Assert.Equal(5, newer.QtyRemaining);
    }

    [Fact]
    public void InsufficientStock_ReturnsUnmet()
    {
        var batches = new List<Batch> { new(null, 3, DateTime.UtcNow) };
        var unmet = Deduct(batches, 10);
        Assert.Equal(7, unmet); // only 3 available, 7 unmet
    }

    [Fact]
    public void EmptyBatchList_FullQtyUnmet()
    {
        var batches = new List<Batch>();
        var unmet = Deduct(batches, 5);
        Assert.Equal(5, unmet);
    }

    [Fact]
    public void ZeroQtyBatches_Skipped()
    {
        var empty = new Batch(new DateOnly(2025, 1, 1), 0, DateTime.UtcNow.AddDays(-10));
        var full = new Batch(new DateOnly(2025, 12, 1), 20, DateTime.UtcNow);
        var batches = new List<Batch> { empty, full };

        var unmet = Deduct(batches, 10);

        Assert.Equal(0, unmet);
        Assert.Equal(0, empty.QtyRemaining); // was 0, stays 0
        Assert.Equal(10, full.QtyRemaining);
    }

    [Fact]
    public void ExactMatch_BatchDepleted()
    {
        var batches = new List<Batch> { new(new DateOnly(2026, 12, 31), 25, DateTime.UtcNow) };
        Deduct(batches, 25);
        Assert.Equal(0, batches[0].QtyRemaining);
    }

    [Fact]
    public void PartialDeduction_QtyRemainingCorrect()
    {
        var batches = new List<Batch> { new(new DateOnly(2026, 6, 30), 100, DateTime.UtcNow) };
        Deduct(batches, 1);
        Assert.Equal(99, batches[0].QtyRemaining);
    }
}
