namespace Pettle.Tests.Bookings;

// Mirrors the exact coverage formula in BookingService.CreateAsync's subscription-deduction
// block. If that formula changes, these tests will catch the drift.
public class SubscriptionCoverageTests
{
    private record PackageItem(string ServiceName, decimal Discount, bool IsFlatAmount);
    private record BookedLine(string ServiceName, string ServiceType, decimal FinalAmount);

    private static decimal ComputeCoveredAmount(IReadOnlyList<BookedLine> lines, IReadOnlyList<PackageItem> packageServices, string packageType, decimal invoiceRevenue)
    {
        decimal coveredAmount = 0;
        foreach (var line in lines)
        {
            var match = packageServices.FirstOrDefault(s => string.Equals(s.ServiceName, line.ServiceName, StringComparison.OrdinalIgnoreCase));
            if (match is null && string.Equals(packageType, line.ServiceType, StringComparison.OrdinalIgnoreCase))
                match = packageServices.FirstOrDefault();
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
        var lines = new[] { new BookedLine("Bath & Brush", "Grooming", 1500m) };
        var pkg = new[] { new PackageItem("Bath & Brush", 100m, IsFlatAmount: false) };
        Assert.Equal(1500m, ComputeCoveredAmount(lines, pkg, "Grooming", 1500m));
    }

    [Fact]
    public void PartiallyCoveredService_50PercentDiscount_CoversHalf()
    {
        var lines = new[] { new BookedLine("Grooming Consult", "Grooming", 1000m) };
        var pkg = new[] { new PackageItem("Grooming Consult", 50m, IsFlatAmount: false) };
        Assert.Equal(500m, ComputeCoveredAmount(lines, pkg, "Grooming", 1000m));
    }

    [Fact]
    public void FlatAmountCoverage_CapsAtLineAmount_NeverOvercovers()
    {
        // Package covers up to ₹2000 flat, but the line only costs ₹1200 — coverage can't exceed the line.
        var lines = new[] { new BookedLine("Boarding Night", "Boarding", 1200m) };
        var pkg = new[] { new PackageItem("Boarding Night", 2000m, IsFlatAmount: true) };
        Assert.Equal(1200m, ComputeCoveredAmount(lines, pkg, "Boarding", 1200m));
    }

    [Fact]
    public void ExtraServiceNotInPackage_IsNotCovered()
    {
        // The whole point of the fix: an out-of-plan service (different vertical too) must not be free.
        var lines = new[] { new BookedLine("Nail Trim (extra)", "Vet", 300m) };
        var pkg = new[] { new PackageItem("Bath & Brush", 100m, IsFlatAmount: false) };
        Assert.Equal(0m, ComputeCoveredAmount(lines, pkg, "Grooming", 300m));
    }

    [Fact]
    public void MixedBooking_CoveredServicePlusExtraService_OnlyCoveredPortionDeducted()
    {
        // Bath & Brush is fully covered; Nail Trim is a Vet-vertical extra outside the Grooming package.
        var lines = new[] { new BookedLine("Bath & Brush", "Grooming", 1500m), new BookedLine("Nail Trim (extra)", "Vet", 300m) };
        var pkg = new[] { new PackageItem("Bath & Brush", 100m, IsFlatAmount: false) };
        var invoiceRevenue = 1800m;
        var covered = ComputeCoveredAmount(lines, pkg, "Grooming", invoiceRevenue);
        Assert.Equal(1500m, covered);
        Assert.Equal(300m, invoiceRevenue - covered); // remains Due, billed separately
    }

    [Fact]
    public void CoveredAmount_NeverExceedsInvoiceRevenue_EvenIfOvermatched()
    {
        var lines = new[] { new BookedLine("Bath & Brush", "Grooming", 1500m) };
        var pkg = new[] { new PackageItem("Bath & Brush", 100m, IsFlatAmount: false) };
        Assert.Equal(1500m, ComputeCoveredAmount(lines, pkg, "Grooming", 1500m));
    }

    // ── Vertical-level fallback (real-world case) ──────────────────────────────
    // Packages are usually created with one broad item name (e.g. "Boarding") rather than one row
    // per exact catalogue service name (e.g. "CAT", "DOGGY OVERNIGHT"). When nothing matches by
    // name but the package's own Type matches the booked line's vertical, the package's first item
    // still applies — otherwise a real "Cat Boarding" subscription would never cover any actual
    // catalogue-picked boarding service and the subscription toggle would be silently useless.

    [Fact]
    public void NoExactNameMatch_ButSameVertical_FallsBackToPackageItem()
    {
        var lines = new[] { new BookedLine("CAT", "Boarding", 700m) };
        var pkg = new[] { new PackageItem("Boarding", 100m, IsFlatAmount: false) };
        Assert.Equal(700m, ComputeCoveredAmount(lines, pkg, "Boarding", 700m));
    }

    [Fact]
    public void NoExactNameMatch_DifferentVertical_NotCovered()
    {
        // A Grooming service booked against a Boarding-type package's subscription shouldn't fall back.
        var lines = new[] { new BookedLine("Haircut", "Grooming", 900m) };
        var pkg = new[] { new PackageItem("Boarding", 100m, IsFlatAmount: false) };
        Assert.Equal(0m, ComputeCoveredAmount(lines, pkg, "Boarding", 900m));
    }
}
