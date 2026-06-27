using Shouldly;
using Unchained.Xlsx.Models.PageSetup;
using Unchained.Xlsx.Tests.Helpers;
using Xunit;

namespace Unchained.Xlsx.Tests.IntegrationTests;

public class PageSetupTests
{
    [Fact]
    public async Task PageSetup_RoundTrips()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        var setup = document.Sheets[0].PageSetup;
        setup.Orientation = PageOrientation.Landscape;
        setup.PaperSize = 9;
        setup.FitToWidth = 1;

        using var reloaded = await XlsxFixtures.RoundTripAsync(document);
        var result = reloaded.Sheets[0].PageSetup;
        result.Orientation.ShouldBe(PageOrientation.Landscape);
        result.PaperSize.ShouldBe(9);
        result.FitToWidth.ShouldBe(1);
    }

    [Fact]
    public async Task PageMargins_RoundTrip()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        var margins = document.Sheets[0].PageMargins;
        margins.Left = 1.25;
        margins.Top = 0.5;

        using var reloaded = await XlsxFixtures.RoundTripAsync(document);
        var result = reloaded.Sheets[0].PageMargins;
        result.Left.ShouldBe(1.25);
        result.Top.ShouldBe(0.5);
    }

    [Fact]
    public async Task FreezePanes_RoundTrips()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        document.Sheets[0].FreezePanes(1, 2);

        using var reloaded = await XlsxFixtures.RoundTripAsync(document);
        var frozen = reloaded.Sheets[0].View.FrozenPanes;
        frozen.ShouldNotBeNull();
        frozen.FrozenRows.ShouldBe(1);
        frozen.FrozenColumns.ShouldBe(2);
    }

    [Fact]
    public async Task SheetView_RoundTrips()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        document.Sheets[0].View.ShowGridLines = false;
        document.Sheets[0].View.ZoomScale = 150;

        using var reloaded = await XlsxFixtures.RoundTripAsync(document);
        reloaded.Sheets[0].View.ShowGridLines.ShouldBeFalse();
        reloaded.Sheets[0].View.ZoomScale.ShouldBe(150);
    }

    [Fact]
    public async Task HeaderFooter_RoundTrips()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        document.Sheets[0].HeaderFooter.OddHeader = "&CMy Report";
        document.Sheets[0].HeaderFooter.OddFooter = "&CPage &P of &N";

        using var reloaded = await XlsxFixtures.RoundTripAsync(document);
        reloaded.Sheets[0].HeaderFooter.OddHeader.ShouldBe("&CMy Report");
        reloaded.Sheets[0].HeaderFooter.OddFooter.ShouldBe("&CPage &P of &N");
    }
}
