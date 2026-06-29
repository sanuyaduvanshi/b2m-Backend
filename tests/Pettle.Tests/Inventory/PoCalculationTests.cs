namespace Pettle.Tests.Inventory;

// Mirrors InventoryService.CreatePoAsync / UpdatePoAsync line + bill calculation.
// Both create and update use identical arithmetic — one test set covers both.
public class PoCalculationTests
{
    private static decimal R(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);

    private record LineResult(
        decimal Gross, decimal Disc, decimal Net,
        decimal Taxable, decimal TaxAmt, decimal LineTotal, decimal LandingCost);

    private static LineResult CalcPoLine(
        decimal qty, decimal unitCost,
        decimal disc1Pct, decimal disc2Pct,
        decimal taxPct, bool inclusiveTax,
        decimal freeQty = 0)
    {
        var gross = qty * unitCost;
        var disc1 = gross * (disc1Pct / 100m);
        var disc2 = (gross - disc1) * (disc2Pct / 100m);
        var net   = gross - disc1 - disc2;

        decimal taxable, taxAmt;
        if (inclusiveTax)
        {
            taxable = net / (1 + taxPct / 100m);
            taxAmt  = net - taxable;
        }
        else
        {
            taxable = net;
            taxAmt  = net * (taxPct / 100m);
        }

        var lineTotal   = taxable + taxAmt;
        var units       = qty + freeQty;
        var landingCost = units > 0 ? lineTotal / units : 0m;

        return new(gross, disc1 + disc2, net, taxable, taxAmt, lineTotal, landingCost);
    }

    // ── Exclusive tax ─────────────────────────────────────────────────────────

    [Fact]
    public void ExclusiveTax_LineTotal_IsNetPlusTax()
    {
        // qty=10, unitCost=100, disc1=10%, disc2=5%, tax=12% exclusive
        var r = CalcPoLine(10, 100, 10, 5, 12, inclusiveTax: false);

        Assert.Equal(1000m, r.Gross);
        Assert.Equal(145m,  r.Disc);          // disc1=100, disc2=45
        Assert.Equal(855m,  r.Net);
        Assert.Equal(855m,  R(r.Taxable));
        Assert.Equal(102.6m, R(r.TaxAmt));
        Assert.Equal(957.6m, R(r.LineTotal));
        Assert.Equal(95.76m, R(r.LandingCost));
    }

    [Fact]
    public void ExclusiveTax_NoDiscount_LineTotal_IsQtyTimesUnitPlusTax()
    {
        var r = CalcPoLine(5, 100, 0, 0, 18, inclusiveTax: false);
        Assert.Equal(500m, r.Gross);
        Assert.Equal(500m, R(r.Taxable));
        Assert.Equal(90m,  R(r.TaxAmt));      // 500 * 0.18
        Assert.Equal(590m, R(r.LineTotal));
        Assert.Equal(118m, R(r.LandingCost)); // 590 / 5
    }

    // ── Inclusive tax ─────────────────────────────────────────────────────────

    [Fact]
    public void InclusiveTax_LineTotal_SameAsGross()
    {
        // Price already contains 18% GST: unit=118 for 10 units → gross=1180
        var r = CalcPoLine(10, 118, 0, 0, 18, inclusiveTax: true);

        Assert.Equal(1180m, r.Gross);
        Assert.Equal(1000m, R(r.Taxable));    // 1180 / 1.18
        Assert.Equal(180m,  R(r.TaxAmt));
        Assert.Equal(1180m, R(r.LineTotal));  // taxable + taxAmt = gross
        Assert.Equal(118m,  R(r.LandingCost));
    }

    [Fact]
    public void InclusiveTax_WithDiscount_ReducedTaxAndTotal()
    {
        // unit=118, 10% discount, 18% inclusive
        // gross=118, disc=11.8, net=106.2
        // taxable=106.2/1.18=90, taxAmt=16.2 → lineTotal=106.2
        var r = CalcPoLine(1, 118, 10, 0, 18, inclusiveTax: true);

        Assert.Equal(118m,  r.Gross);
        Assert.Equal(11.8m, R(r.Disc));
        Assert.Equal(106.2m, R(r.Net));
        Assert.Equal(90m,   R(r.Taxable));
        Assert.Equal(16.2m, R(r.TaxAmt));
        Assert.Equal(106.2m, R(r.LineTotal));
    }

    // ── Landing cost with free quantity ───────────────────────────────────────

    [Fact]
    public void LandingCost_FreeQtySpread_AcrossAllUnits()
    {
        // qty=10, freeQty=2, no tax → lineTotal=1000, units=12 → landingCost≈83.33
        var r = CalcPoLine(10, 100, 0, 0, 0, inclusiveTax: false, freeQty: 2);

        Assert.Equal(1000m, R(r.LineTotal));
        Assert.Equal(83.33m, R(r.LandingCost)); // 1000/12 = 83.3333...
    }

    [Fact]
    public void LandingCost_ZeroQtyAndFreeQty_IsZero()
    {
        var r = CalcPoLine(0, 100, 0, 0, 0, inclusiveTax: false, freeQty: 0);
        Assert.Equal(0m, R(r.LandingCost));
    }

    // ── Bill-level (grand total) ───────────────────────────────────────────────

    private static decimal PoBillTotal(
        IEnumerable<(decimal qty, decimal unitCost, decimal disc1, decimal disc2, decimal taxPct, bool inclusive)> lines,
        decimal flatDiscPct, decimal additionalCharges, decimal adjustment)
    {
        decimal sumTaxable = 0, sumTax = 0;
        foreach (var (qty, unitCost, d1, d2, taxPct, inclusive) in lines)
        {
            var r = CalcPoLine(qty, unitCost, d1, d2, taxPct, inclusive);
            sumTaxable += r.Taxable;
            sumTax     += r.TaxAmt;
        }
        var flatDiscAmt  = sumTaxable * (flatDiscPct / 100m);
        var finalTaxable = sumTaxable - flatDiscAmt;
        var finalTax     = sumTaxable > 0 ? sumTax * (finalTaxable / sumTaxable) : 0m;
        var rawTotal     = finalTaxable + finalTax + additionalCharges + adjustment;
        return Math.Round(rawTotal, MidpointRounding.AwayFromZero);
    }

    [Fact]
    public void BillTotal_FlatDiscount_ReducesTotal()
    {
        // Single line: qty=10, unit=100, tax=0 → taxable=1000
        // flatDisc=10% → finalTaxable=900, total=900
        var total = PoBillTotal(
            new[] { (10m, 100m, 0m, 0m, 0m, false) },
            flatDiscPct: 10, additionalCharges: 0, adjustment: 0);
        Assert.Equal(900m, total);
    }

    [Fact]
    public void BillTotal_AdditionalChargesAndAdjustment_Added()
    {
        // taxable=1000, no flat disc, addCharges=100, adj=50 → total=1150
        var total = PoBillTotal(
            new[] { (10m, 100m, 0m, 0m, 0m, false) },
            flatDiscPct: 0, additionalCharges: 100, adjustment: 50);
        Assert.Equal(1150m, total);
    }

    [Fact]
    public void BillTotal_MultipleLines_Summed()
    {
        // line1: 5 × 100 → 500;  line2: 3 × 200 → 600;  total=1100
        var total = PoBillTotal(
            new[] { (5m, 100m, 0m, 0m, 0m, false), (3m, 200m, 0m, 0m, 0m, false) },
            flatDiscPct: 0, additionalCharges: 0, adjustment: 0);
        Assert.Equal(1100m, total);
    }
}
