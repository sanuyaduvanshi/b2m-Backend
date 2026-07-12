using Xunit;

namespace Pettle.Tests.Inventory;

/// <summary>
/// Pure-logic tests for FIFO (First In First Out) batch deduction.
/// Mirrors the algorithm in FifoBatchDeductor.DeductAsync.
/// </summary>
public class FifoTests
{
    // Represents one in-memory batch
    private record Batch(decimal Qty, DateTime ReceivedAt)
    {
        public decimal QtyRemaining { get; set; } = Qty;
    }

    /// <summary>Deduct qty from batches sorted FIFO (oldest received first).</summary>
    private static decimal Deduct(List<Batch> batches, decimal qty)
    {
        var sorted = batches
            .Where(b => b.QtyRemaining > 0)
            .OrderBy(b => b.ReceivedAt)
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
        var batches = new List<Batch> { new(100, DateTime.UtcNow) };
        var unmet = Deduct(batches, 30);
        Assert.Equal(0, unmet);
        Assert.Equal(70, batches[0].QtyRemaining);
    }

    [Fact]
    public void OldestReceived_DeductedFirst()
    {
        var older = new Batch(10, DateTime.UtcNow.AddDays(-5));
        var newer = new Batch(50, DateTime.UtcNow);
        var batches = new List<Batch> { newer, older }; // deliberately reversed order

        Deduct(batches, 10);

        Assert.Equal(0, older.QtyRemaining);   // older fully consumed
        Assert.Equal(50, newer.QtyRemaining);  // newer untouched
    }

    [Fact]
    public void SpansMultipleBatches_FIFO()
    {
        var b1 = new Batch(5, DateTime.UtcNow.AddDays(-10));
        var b2 = new Batch(5, DateTime.UtcNow.AddDays(-5));
        var b3 = new Batch(20, DateTime.UtcNow);
        var batches = new List<Batch> { b3, b1, b2 };

        var unmet = Deduct(batches, 12);

        Assert.Equal(0, unmet);
        Assert.Equal(0, b1.QtyRemaining);  // 5 taken (oldest)
        Assert.Equal(0, b2.QtyRemaining);  // 5 taken (next oldest)
        Assert.Equal(18, b3.QtyRemaining); // 2 taken (newest)
    }

    [Fact]
    public void InsufficientStock_ReturnsUnmet()
    {
        var batches = new List<Batch> { new(3, DateTime.UtcNow) };
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
        var empty = new Batch(0, DateTime.UtcNow.AddDays(-10));
        var full = new Batch(20, DateTime.UtcNow);
        var batches = new List<Batch> { empty, full };

        var unmet = Deduct(batches, 10);

        Assert.Equal(0, unmet);
        Assert.Equal(0, empty.QtyRemaining); // was 0, stays 0
        Assert.Equal(10, full.QtyRemaining);
    }

    [Fact]
    public void ExactMatch_BatchDepleted()
    {
        var batches = new List<Batch> { new(25, DateTime.UtcNow) };
        Deduct(batches, 25);
        Assert.Equal(0, batches[0].QtyRemaining);
    }

    [Fact]
    public void PartialDeduction_QtyRemainingCorrect()
    {
        var batches = new List<Batch> { new(100, DateTime.UtcNow) };
        Deduct(batches, 1);
        Assert.Equal(99, batches[0].QtyRemaining);
    }
}
