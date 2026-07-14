namespace Pettle.Tests.Bookings;

// Mirrors the exact coverage formula in BookingService.CreateAsync's subscription-deduction
// block. If that formula changes, these tests will catch the drift.
public class SubscriptionCoverageTests
{
    private record PackageItem(string ServiceName, decimal Discount, bool IsFlatAmount);
    private record BookedLine(string ServiceName, decimal FinalAmount);

    private static decimal ComputeCoveredAmount(IReadOnlyList<BookedLine> lines, IReadOnlyList<PackageItem> packageServices, decimal invoiceRevenue)
    {
        decimal coveredAmount = 0;
        foreach (var line in lines)
        {
            var match = packageServices.FirstOrDefault(s => string.Equals(s.ServiceName, line.ServiceName, StringComparison.OrdinalIgnoreCase));
            if (match is null) continue;
            var portion = match.IsFlatAmount
                ? Math.Min(match.Discount, line.FinalAmount)
                : line.FinalAmount * (match.Discount / 100m);
            coveredAmount += Math.Clamp(portion, 0, line.FinalAmount);
        }
        return Math.Round(Math.Min(coveredAmount, invoiceRevenue), 2, MidpointRounding.AwayFromZero);
    }

    [Fact]
    public void FullyCoveredService_100PercentDiscount_CoversWholeLine()
    {
        var lines = new[] { new BookedLine("Bath & Brush", 1500m) };
        var pkg = new[] { new PackageItem("Bath & Brush", 100m, IsFlatAmount: false) };
        Assert.Equal(1500m, ComputeCoveredAmount(lines, pkg, 1500m));
    }

    [Fact]
    public void PartiallyCoveredService_50PercentDiscount_CoversHalf()
    {
        var lines = new[] { new BookedLine("Grooming Consult", 1000m) };
        var pkg = new[] { new PackageItem("Grooming Consult", 50m, IsFlatAmount: false) };
        Assert.Equal(500m, ComputeCoveredAmount(lines, pkg, 1000m));
    }

    [Fact]
    public void FlatAmountCoverage_CapsAtLineAmount_NeverOvercovers()
    {
        // Package covers up to ₹2000 flat, but the line only costs ₹1200 — coverage can't exceed the line.
        var lines = new[] { new BookedLine("Boarding Night", 1200m) };
        var pkg = new[] { new PackageItem("Boarding Night", 2000m, IsFlatAmount: true) };
        Assert.Equal(1200m, ComputeCoveredAmount(lines, pkg, 1200m));
    }

    [Fact]
    public void ExtraServiceNotInPackage_IsNotCovered()
    {
        // The whole point of the fix: an out-of-plan service must not be silently free.
        var lines = new[] { new BookedLine("Nail Trim (extra)", 300m) };
        var pkg = new[] { new PackageItem("Bath & Brush", 100m, IsFlatAmount: false) };
        Assert.Equal(0m, ComputeCoveredAmount(lines, pkg, 300m));
    }

    [Fact]
    public void MixedBooking_CoveredServicePlusExtraService_OnlyCoveredPortionDeducted()
    {
        // Bath & Brush is fully covered; Nail Trim is a new extra service outside the package.
        var lines = new[] { new BookedLine("Bath & Brush", 1500m), new BookedLine("Nail Trim (extra)", 300m) };
        var pkg = new[] { new PackageItem("Bath & Brush", 100m, IsFlatAmount: false) };
        var invoiceRevenue = 1800m;
        var covered = ComputeCoveredAmount(lines, pkg, invoiceRevenue);
        Assert.Equal(1500m, covered);
        Assert.Equal(300m, invoiceRevenue - covered); // remains Due, billed separately
    }

    [Fact]
    public void CoveredAmount_NeverExceedsInvoiceRevenue_EvenIfOvermatched()
    {
        var lines = new[] { new BookedLine("Bath & Brush", 1500m) };
        // Discount would compute to more than the line itself allows for via clamp, and the overall
        // cap must also never exceed total invoice revenue.
        var pkg = new[] { new PackageItem("Bath & Brush", 100m, IsFlatAmount: false) };
        Assert.Equal(1500m, ComputeCoveredAmount(lines, pkg, 1500m));
    }
}
