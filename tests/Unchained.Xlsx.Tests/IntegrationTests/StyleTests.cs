using Shouldly;
using Unchained.Ooxml.Drawing;
using Unchained.Xlsx.Models;
using Unchained.Xlsx.Tests.Helpers;
using Xunit;

namespace Unchained.Xlsx.Tests.IntegrationTests;

public class StyleTests
{
    [Fact]
    public async Task ApplyFont_BoldRoundTrips()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        var cell = document.Sheets[0][1, 1];
        cell.SetValue("Header");
        cell.ApplyFont(static f =>
            {
                f.Bold = true;
                f.SizePoints = 14;
            }
        );

        using var reloaded = await XlsxFixtures.RoundTripAsync(document);
        var resolved = reloaded.Styles.GetFont(reloaded.Sheets[0].GetCell(1, 1)!.StyleIndex);
        resolved.Bold.ShouldBeTrue();
        resolved.SizePoints.ShouldBe(14);
    }

    [Fact]
    public async Task ApplyFill_SolidColorRoundTrips()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        var cell = document.Sheets[0][1, 1];
        cell.SetValue(1.0);
        cell.ApplyFill(static f =>
            {
                f.PatternType = FillPattern.Solid;
                f.ForegroundColor = ColorSpec.FromRgb(0x44, 0x72, 0xC4);
            }
        );

        using var reloaded = await XlsxFixtures.RoundTripAsync(document);
        var result = reloaded.Sheets[0].GetCell(1, 1)!;
        var fill = reloaded.Styles.GetFill(result.StyleIndex);
        fill.PatternType.ShouldBe(FillPattern.Solid);
        fill.ForegroundColor.ShouldNotBeNull();
    }

    [Fact]
    public async Task ApplyBorder_RoundTrips()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        var cell = document.Sheets[0][1, 1];
        cell.SetValue(1.0);
        cell.ApplyBorder(static b => b.SetAllEdges(BorderStyle.Thin));

        using var reloaded = await XlsxFixtures.RoundTripAsync(document);
        var result = reloaded.Sheets[0].GetCell(1, 1)!;
        var border = reloaded.Styles.GetBorder(result.StyleIndex);
        border.Left.Style.ShouldBe(BorderStyle.Thin);
        border.Bottom.Style.ShouldBe(BorderStyle.Thin);
    }

    [Fact]
    public async Task ApplyAlignment_RoundTrips()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        var cell = document.Sheets[0][1, 1];
        cell.SetValue("x");
        cell.ApplyAlignment(static a =>
            {
                a.Horizontal = HorizontalAlignment.Center;
                a.Vertical = VerticalAlignment.Center;
                a.WrapText = true;
            }
        );

        using var reloaded = await XlsxFixtures.RoundTripAsync(document);
        var alignment = reloaded.Sheets[0].GetCell(1, 1)!.GetEffectiveStyle().Alignment;
        alignment.Horizontal.ShouldBe(HorizontalAlignment.Center);
        alignment.Vertical.ShouldBe(VerticalAlignment.Center);
        alignment.WrapText.ShouldBeTrue();
    }

    [Fact]
    public async Task SetNumberFormat_RoundTrips()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        var cell = document.Sheets[0][1, 1];
        cell.SetValue(1234.5);
        cell.SetNumberFormat("#,##0.00");

        using var reloaded = await XlsxFixtures.RoundTripAsync(document);
        reloaded.Sheets[0].GetCell(1, 1)!.NumberFormatCode.ShouldBe("#,##0.00");
    }

    [Fact]
    public void StyleBook_GetOrAdd_DeduplicatesIdenticalStyles()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        var sheet = document.Sheets[0];

        for (var r = 1; r <= 100; r++)
        {
            var cell = sheet[r, 1];
            cell.SetValue(r);
            cell.ApplyFont(static f => f.Bold = true);
        }

        // One font added beyond the default, one xf beyond the default.
        document.Styles.Fonts.Count.ShouldBe(2);
        document.Styles.CellXfs.Count.ShouldBe(2);
    }

    [Fact]
    public async Task DefaultFills_AreNoneAndGray125()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        using var reloaded = await XlsxFixtures.RoundTripAsync(document);

        reloaded.Styles.Fills.Count.ShouldBeGreaterThanOrEqualTo(2);
        reloaded.Styles.Fills[0].PatternType.ShouldBe(FillPattern.None);
        reloaded.Styles.Fills[1].PatternType.ShouldBe(FillPattern.Gray125);
    }

    [Fact]
    public async Task CopyStyleFrom_SharesStyleIndex()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        var sheet = document.Sheets[0];
        sheet[1, 1].SetValue("a");
        sheet[1, 1].ApplyFont(static f => f.Italic = true);
        sheet[2, 1].SetValue("b");
        sheet[2, 1].CopyStyleFrom(sheet[1, 1]);

        using var reloaded = await XlsxFixtures.RoundTripAsync(document);
        var s = reloaded.Sheets[0];
        s.GetCell(2, 1)!.StyleIndex.ShouldBe(s.GetCell(1, 1)!.StyleIndex);
    }
}
