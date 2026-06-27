using Shouldly;
using Unchained.Ooxml.Drawing;
using Unchained.Xlsx.Core.Xml;
using Unchained.Xlsx.Models;
using Unchained.Xlsx.Models.Cell;
using Unchained.Xlsx.Styles;
using Xunit;

namespace Unchained.Xlsx.Tests.UnitTests;

public class StyleModelTests
{
    // ── CellFill ─────────────────────────────────────────────────────────────

    [Fact]
    public void CellFill_Solid_SetsPatternAndForeground()
    {
        var color = ColorSpec.FromRgb(0x44, 0x72, 0xC4);
        var fill = CellFill.Solid(color);

        fill.PatternType.ShouldBe(FillPattern.Solid);
        fill.ForegroundColor.ShouldBe(color);
    }

    [Fact]
    public void CellFill_None_HasNoPattern() =>
        CellFill.None.PatternType.ShouldBe(FillPattern.None);

    [Fact]
    public void CellFill_Clone_IsEqualButDistinct()
    {
        var fill = CellFill.Solid(ColorSpec.FromRgb(1, 2, 3));
        var clone = fill.Clone();

        clone.ShouldBe(fill);
        clone.ShouldNotBeSameAs(fill);
    }

    [Fact]
    public void CellFill_Equality_AndHashCode()
    {
        var a = CellFill.Solid(ColorSpec.FromRgb(1, 2, 3));
        var b = CellFill.Solid(ColorSpec.FromRgb(1, 2, 3));
        var c = CellFill.Solid(ColorSpec.FromRgb(4, 5, 6));

        a.Equals(b).ShouldBeTrue();
        a.Equals((object)b).ShouldBeTrue();
        a.Equals(c).ShouldBeFalse();
        a.Equals(null).ShouldBeFalse();
        // ReSharper disable once SuspiciousTypeConversion.Global
        a.Equals("not a fill").ShouldBeFalse();
        a.GetHashCode().ShouldBe(b.GetHashCode());
    }

    // ── BorderLine ─────────────────────────────────────────────────────────────

    [Fact]
    public void BorderLine_None_HasNoStyle() =>
        BorderLine.None.Style.ShouldBe(BorderStyle.None);

    [Fact]
    public void BorderLine_Clone_IsEqual()
    {
        var line = new BorderLine { Style = BorderStyle.Thick, Color = ColorSpec.FromRgb(9, 9, 9) };
        var clone = line.Clone();

        clone.ShouldBe(line);
        clone.ShouldNotBeSameAs(line);
    }

    [Fact]
    public void BorderLine_Equality_AndHashCode()
    {
        var a = new BorderLine { Style = BorderStyle.Medium, Color = ColorSpec.FromRgb(1, 1, 1) };
        var b = new BorderLine { Style = BorderStyle.Medium, Color = ColorSpec.FromRgb(1, 1, 1) };
        var c = new BorderLine { Style = BorderStyle.Thin };

        a.Equals(b).ShouldBeTrue();
        a.Equals((object)b).ShouldBeTrue();
        a.Equals(c).ShouldBeFalse();
        a.Equals(null).ShouldBeFalse();
        a.GetHashCode().ShouldBe(b.GetHashCode());
    }

    // ── CellBorder ─────────────────────────────────────────────────────────────

    [Fact]
    public void CellBorder_SetAllEdges_AppliesToFourSides()
    {
        var border = new CellBorder().SetAllEdges(BorderStyle.Thin, ColorSpec.FromRgb(0, 0, 0));

        border.Left.Style.ShouldBe(BorderStyle.Thin);
        border.Right.Style.ShouldBe(BorderStyle.Thin);
        border.Top.Style.ShouldBe(BorderStyle.Thin);
        border.Bottom.Style.ShouldBe(BorderStyle.Thin);
        border.Diagonal.Style.ShouldBe(BorderStyle.None);
    }

    [Fact]
    public void CellBorder_Clone_IsDeepCopy()
    {
        var border = new CellBorder { DiagonalUp = true, DiagonalDown = true }
            .SetAllEdges(BorderStyle.Double);
        var clone = border.Clone();

        clone.ShouldBe(border);
        clone.Left.ShouldNotBeSameAs(border.Left);
        clone.DiagonalUp.ShouldBeTrue();
        clone.DiagonalDown.ShouldBeTrue();
    }

    [Fact]
    public void CellBorder_Equality_AndHashCode()
    {
        var a = new CellBorder().SetAllEdges(BorderStyle.Thin);
        var b = new CellBorder().SetAllEdges(BorderStyle.Thin);
        var c = new CellBorder().SetAllEdges(BorderStyle.Thick);

        a.Equals(b).ShouldBeTrue();
        a.Equals((object)b).ShouldBeTrue();
        a.Equals(c).ShouldBeFalse();
        a.Equals(null).ShouldBeFalse();
        a.GetHashCode().ShouldBe(b.GetHashCode());
    }

    // ── NumberFormat ─────────────────────────────────────────────────────────────

    [Fact]
    public void NumberFormat_BuiltIn_FlagsCorrectly()
    {
        new NumberFormat(0, "General").IsBuiltIn.ShouldBeTrue();
        new NumberFormat(NumberFormat.FirstCustomId, "0.00").IsBuiltIn.ShouldBeFalse();
    }

    [Fact]
    public void NumberFormat_StoresIdAndCode()
    {
        var format = new NumberFormat(164, "0.000");
        format.FormatId.ShouldBe(164);
        format.FormatCode.ShouldBe("0.000");
    }

    [Fact]
    public void NumberFormat_Equality_AndHashCode()
    {
        var a = new NumberFormat(164, "0.00");
        var b = new NumberFormat(164, "0.00");
        var c = new NumberFormat(165, "0.00");

        a.Equals(b).ShouldBeTrue();
        a.Equals((object)b).ShouldBeTrue();
        a.Equals(c).ShouldBeFalse();
        a.Equals(null).ShouldBeFalse();
        a.GetHashCode().ShouldBe(b.GetHashCode());
    }

    // ── CellError ─────────────────────────────────────────────────────────────

    [
        Theory,
        InlineData(CellError.Null, "#NULL!"),
        InlineData(CellError.DivisionByZero, "#DIV/0!"),
        InlineData(CellError.Value, "#VALUE!"),
        InlineData(CellError.Reference, "#REF!"),
        InlineData(CellError.Name, "#NAME?"),
        InlineData(CellError.Number, "#NUM!"),
        InlineData(CellError.NotAvailable, "#N/A")
    ]
    public void CellError_ToLiteral_RoundTripsFromLiteral(CellError error, string literal)
    {
        error.ToLiteral().ShouldBe(literal);
        CellErrorExtensions.FromLiteral(literal).ShouldBe(error);
    }

    [
        Theory,
        InlineData(null),
        InlineData(""),
        InlineData("#WAT?"),
        InlineData("not an error")
    ]
    public void CellError_FromLiteral_ReturnsNullForUnknown(string? literal) =>
        CellErrorExtensions.FromLiteral(literal).ShouldBeNull();

    // ── SmlColor ─────────────────────────────────────────────────────────────

    [Fact]
    public void SmlColor_FromHexArgb_ParsesEightDigits()
    {
        var color = SmlColor.FromHexArgb("FF4472C4");
        color.ShouldNotBeNull();
        color.Value.Type.ShouldBe(ColorSpecType.Rgb);
    }

    [Fact]
    public void SmlColor_FromHexArgb_PadsSixDigitRgb()
    {
        var color = SmlColor.FromHexArgb("4472C4");
        color.ShouldNotBeNull();
    }

    [
        Theory,
        InlineData(null),
        InlineData(""),
        InlineData("ZZZZ"),
        InlineData("12345"),
        InlineData("NOTHEXAA")
    ]
    public void SmlColor_FromHexArgb_ReturnsNullForInvalid(string? hex) =>
        SmlColor.FromHexArgb(hex).ShouldBeNull();

    [Fact]
    public void SmlColor_ToHexArgb_RendersEightDigits()
    {
        var hex = SmlColor.ToHexArgb(ColorSpec.FromArgb(0xFF, 0x44, 0x72, 0xC4));
        hex.Length.ShouldBe(8);
        hex.ShouldBe("FF4472C4");
    }

    [Fact]
    public void SmlColor_HexRoundTrips()
    {
        var original = ColorSpec.FromArgb(0xFF, 0x12, 0x34, 0x56);
        var hex = SmlColor.ToHexArgb(original);
        SmlColor.FromHexArgb(hex)!.Value.Rgb.ShouldBe(original.Rgb);
    }
}
