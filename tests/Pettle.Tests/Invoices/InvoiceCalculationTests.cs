using Pettle.Domain.Invoices;

namespace Pettle.Tests.Invoices;

// Mirrors the exact formula in InvoiceService.UpdateAsync / CreateSaleAsync.
// If either formula changes, these tests will catch the drift.
public class InvoiceCalculationTests
{
    private static decimal R(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);

    // ── Line-level ────────────────────────────────────────────────────────────

    private static (decimal net, decimal taxable, decimal taxAmt)
        CalcLine(decimal qty, decimal unit, decimal discPct, decimal taxPct)
    {
        var gross = qty * unit;
        var disc  = gross * (discPct / 100m);
        var net   = gross - disc;
        var taxable = taxPct > 0 ? net / (1 + taxPct / 100m) : net;
        var taxAmt  = net - taxable;
        return (net, taxable, taxAmt);
    }

    [Theory]
    [InlineData(1,   100,  0,  100)]   // no discount
    [InlineData(2,   100,  0,  200)]   // qty 2
    [InlineData(1,   100, 10,   90)]   // 10% discount
    [InlineData(1,   100, 50,   50)]   // 50% discount
    [InlineData(1,   100,100,    0)]   // 100% discount → zero
    [InlineData(3,    50, 20,  120)]   // qty 3, 20% discount
    public void LineNet_DiscountApplied(decimal qty, decimal unit, decimal discPct, decimal expectedNet)
    {
        var (net, _, _) = CalcLine(qty, unit, discPct, 0);
        Assert.Equal(expectedNet, net);
    }

    [Theory]
    // (qty, unit, discPct, taxPct) → (expectedTaxable, expectedTax)
    [InlineData(1,   100,  0,   0,  100.00,  0.00)]   // no tax
    [InlineData(1,   100,  0,  18,   84.75, 15.25)]   // 18% GST inclusive, price = 100
    [InlineData(1,   118,  0,  18,  100.00, 18.00)]   // 18% GST inclusive, price already = base+tax
    [InlineData(1,   100, 10,  18,   76.27, 13.73)]   // 10% disc + 18% GST
    [InlineData(3,    50,  0,  12,  133.93, 16.07)]   // qty 3, 12% GST
    [InlineData(1,   100,100,  18,    0.00,  0.00)]   // 100% discount → zero taxable & tax
    public void LineTaxSplit_GstInclusiveFormula(
        decimal qty, decimal unit, decimal discPct, decimal taxPct,
        decimal expectedTaxable, decimal expectedTax)
    {
        var (_, taxable, taxAmt) = CalcLine(qty, unit, discPct, taxPct);
        Assert.Equal(expectedTaxable, R(taxable));
        Assert.Equal(expectedTax,     R(taxAmt));
    }

    // ── Bill-level ────────────────────────────────────────────────────────────

    private static decimal BillTotal(
        IEnumerable<(decimal qty, decimal unit, decimal discPct, decimal taxPct)> lines,
        decimal flatDiscPct, decimal additionalCharges)
    {
        decimal sumTaxable = 0, sumTax = 0;
        foreach (var (qty, unit, discPct, taxPct) in lines)
        {
            var (_, taxable, taxAmt) = CalcLine(qty, unit, discPct, taxPct);
            sumTaxable += taxable;
            sumTax     += taxAmt;
        }
        var flatDiscAmt  = sumTaxable * (flatDiscPct / 100m);
        var finalTaxable = sumTaxable - flatDiscAmt;
        var finalTax     = sumTaxable > 0 ? sumTax * (finalTaxable / sumTaxable) : 0m;
        var rawTotal     = finalTaxable + finalTax + additionalCharges;
        return Math.Round(rawTotal, MidpointRounding.AwayFromZero);
    }

    [Fact]
    public void BillTotal_NoDiscNoTax_EqualsSumOfLines()
    {
        var total = BillTotal(
            new[] { (1m, 100m, 0m, 0m), (2m, 50m, 0m, 0m) },
            flatDiscPct: 0, additionalCharges: 0);
        Assert.Equal(200m, total);
    }

    [Fact]
    public void BillTotal_FlatDiscount_ReducesTotalAndProRatesTax()
    {
        // line: qty=1, unit=118, tax=18% → taxable=100, tax=18, net=118
        // flatDisc=50% → finalTaxable=50, finalTax=18*(50/100)=9 → rawTotal=59
        var total = BillTotal(
            new[] { (1m, 118m, 0m, 18m) },
            flatDiscPct: 50, additionalCharges: 0);
        Assert.Equal(59m, total);
    }

    [Fact]
    public void BillTotal_AdditionalCharges_Added()
    {
        var total = BillTotal(
            new[] { (1m, 118m, 0m, 18m) },
            flatDiscPct: 0, additionalCharges: 50);
        Assert.Equal(168m, total);   // 118 + 50
    }

    [Fact]
    public void BillTotal_MultipleLines_AggregatesCorrectly()
    {
        // line1: qty=1, unit=118, tax=18% → net=118, taxable=100, tax=18
        // line2: qty=1, unit=59,  tax=18% → net=59,  taxable=50,  tax=9
        // sumTaxable=150, sumTax=27 → total=177
        var total = BillTotal(
            new[] { (1m, 118m, 0m, 18m), (1m, 59m, 0m, 18m) },
            flatDiscPct: 0, additionalCharges: 0);
        Assert.Equal(177m, total);
    }

    [Fact]
    public void BillTotal_RoundHalfUp_AwayFromZero()
    {
        // Construct a total that produces .5 → should round UP (AwayFromZero)
        // line: qty=1, unit=100.5, tax=0% → raw=100.5 → rounded=101
        var total = BillTotal(
            new[] { (1m, 100.5m, 0m, 0m) },
            flatDiscPct: 0, additionalCharges: 0);
        Assert.Equal(101m, total);
    }

    // ── Payment-status derivation ─────────────────────────────────────────────
    // Mirrors the three-way expression at the end of InvoiceService.UpdateAsync

    private static InvoicePaymentStatus DeriveStatus(decimal revenue, decimal paid)
    {
        var due = Math.Max(0, R(revenue) - paid);
        return due == 0 && paid > 0
            ? InvoicePaymentStatus.Paid
            : paid > 0
                ? InvoicePaymentStatus.PartiallyPaid
                : InvoicePaymentStatus.Pending;
    }

    [Theory]
    [InlineData(100, 100, InvoicePaymentStatus.Paid)]
    [InlineData(100,  50, InvoicePaymentStatus.PartiallyPaid)]
    [InlineData(100,   0, InvoicePaymentStatus.Pending)]
    public void UpdateAsync_PaymentStatus_SetCorrectly(
        decimal revenue, decimal paid, InvoicePaymentStatus expected)
    {
        Assert.Equal(expected, DeriveStatus(revenue, paid));
    }

    // ── Payment tolerance ──────────────────────────────────────────────────────
    // Mirrors the "paid > invoice.Revenue + 1.01m" guard in CreateSaleAsync/UpdateAsync.
    // The POS screen recomputes this same whole-rupee total independently in JS; a raw total a
    // fraction of a paisa from a .50 boundary can round to the adjacent rupee on one side and not
    // the other, so a genuine ±1 rupee gap between what the screen offered and what the server
    // computed must be accepted - only a real overpayment should still be rejected.

    private static bool PaymentExceedsTotal(decimal paid, decimal revenue) => paid > revenue + 1.01m;

    [Theory]
    [InlineData(864, 865, false)]     // customer paid the ₹1-over amount the screen offered - must succeed
    [InlineData(865, 864, false)]     // divergence can go either direction
    [InlineData(100, 100, false)]     // exact match always fine
    [InlineData(100, 100.5, false)]   // sub-rupee rounding noise
    [InlineData(100, 101.01, false)]  // right at the edge of tolerance
    [InlineData(100, 101.02, true)]   // just past tolerance - still rejected
    [InlineData(100, 150, true)]      // genuine overpayment - must still be rejected
    public void PaymentExceedsTotal_ToleratesOneRupeeRoundingGap(decimal revenue, decimal paid, bool expectRejected)
    {
        Assert.Equal(expectRejected, PaymentExceedsTotal(paid, revenue));
    }
}
