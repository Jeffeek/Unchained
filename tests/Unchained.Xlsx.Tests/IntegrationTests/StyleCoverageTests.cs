using Shouldly;
using Unchained.Ooxml.Drawing;
using Unchained.Xlsx.Models;
using Unchained.Xlsx.Styles;
using Unchained.Xlsx.Tests.Helpers;
using Xunit;

namespace Unchained.Xlsx.Tests.IntegrationTests;

/// <summary>Extended style round-trips to exercise more StylesWriter / StylesParser branches.</summary>
public class StyleCoverageTests
{
    [Fact]
    public async Task Font_AllAttributes_RoundTrip()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        var cell = document.Sheets[0][1, 1];
        cell.SetValue("styled");
        cell.ApplyFont(static f =>
            {
                f.Name = "Arial";
                f.SizePoints = 16;
                f.Bold = true;
                f.Italic = true;
                f.Underline = FontUnderline.Double;
                f.Strikethrough = true;
                f.Color = ColorSpec.FromRgb(0xC0, 0x00, 0x00);
                f.VerticalAlignment = FontVerticalAlignment.Superscript;
            }
        );

        using var reloaded = await XlsxFixtures.RoundTripAsync(document);
        var font = reloaded.Styles.GetFont(reloaded.Sheets[0].GetCell(1, 1)!.StyleIndex);
        font.Name.ShouldBe("Arial");
        font.SizePoints.ShouldBe(16);
        font.Bold.ShouldBeTrue();
        font.Italic.ShouldBeTrue();
        font.Underline.ShouldBe(FontUnderline.Double);
        font.Strikethrough.ShouldBeTrue();
        font.Color.ShouldNotBeNull();
        font.VerticalAlignment.ShouldBe(FontVerticalAlignment.Superscript);
    }

    [Fact]
    public async Task Fill_PatternWithBackground_RoundTrips()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        var cell = document.Sheets[0][1, 1];
        cell.SetValue(1.0);
        cell.ApplyFill(static f =>
            {
                f.PatternType = FillPattern.LightGrid;
                f.ForegroundColor = ColorSpec.FromRgb(0x10, 0x20, 0x30);
                f.BackgroundColor = ColorSpec.FromRgb(0xFF, 0xFF, 0x00);
            }
        );

        using var reloaded = await XlsxFixtures.RoundTripAsync(document);
        var fill = reloaded.Styles.GetFill(reloaded.Sheets[0].GetCell(1, 1)!.StyleIndex);
        fill.PatternType.ShouldBe(FillPattern.LightGrid);
        fill.ForegroundColor.ShouldNotBeNull();
        fill.BackgroundColor.ShouldNotBeNull();
    }

    [Fact]
    public async Task Border_DiagonalAndEdges_RoundTrip()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        var cell = document.Sheets[0][1, 1];
        cell.SetValue(1.0);
        cell.ApplyBorder(static b =>
            {
                b.Left = new BorderLine { Style = BorderStyle.Thin };
                b.Right = new BorderLine { Style = BorderStyle.Medium };
                b.Top = new BorderLine { Style = BorderStyle.Dashed };
                b.Bottom = new BorderLine { Style = BorderStyle.Double };
                b.Diagonal = new BorderLine { Style = BorderStyle.Thin };
                b.DiagonalUp = true;
            }
        );

        using var reloaded = await XlsxFixtures.RoundTripAsync(document);
        var border = reloaded.Styles.GetBorder(reloaded.Sheets[0].GetCell(1, 1)!.StyleIndex);
        border.Left.Style.ShouldBe(BorderStyle.Thin);
        border.Right.Style.ShouldBe(BorderStyle.Medium);
        border.Top.Style.ShouldBe(BorderStyle.Dashed);
        border.Bottom.Style.ShouldBe(BorderStyle.Double);
        border.Diagonal.Style.ShouldBe(BorderStyle.Thin);
        border.DiagonalUp.ShouldBeTrue();
    }

    [Fact]
    public async Task MultipleCustomNumberFormats_RoundTrip()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        var sheet = document.Sheets[0];
        sheet[1, 1].SetValue(1.0);
        sheet[1, 1].SetNumberFormat("0.000");
        sheet[2, 1].SetValue(2.0);
        sheet[2, 1].SetNumberFormat("#,##0.00;[Red](#,##0.00)");
        sheet[3, 1].SetValue(0.5);
        sheet[3, 1].SetNumberFormat("0.00%");

        using var reloaded = await XlsxFixtures.RoundTripAsync(document);
        var s = reloaded.Sheets[0];
        s.GetCell(1, 1)!.NumberFormatCode.ShouldBe("0.000");
        s.GetCell(2, 1)!.NumberFormatCode.ShouldBe("#,##0.00;[Red](#,##0.00)");
        s.GetCell(3, 1)!.NumberFormatCode.ShouldBe("0.00%");
    }

    [Fact]
    public async Task AlignmentWithIndentAndRotation_RoundTrips()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        var cell = document.Sheets[0][1, 1];
        cell.SetValue("x");
        cell.ApplyAlignment(static a =>
            {
                a.Horizontal = HorizontalAlignment.Right;
                a.Indent = 2;
                a.TextRotation = 45;
            }
        );

        using var reloaded = await XlsxFixtures.RoundTripAsync(document);
        var alignment = reloaded.Sheets[0].GetCell(1, 1)!.GetEffectiveStyle().Alignment;
        alignment.Horizontal.ShouldBe(HorizontalAlignment.Right);
        alignment.Indent.ShouldBe(2);
        alignment.TextRotation.ShouldBe(45);
    }
}
