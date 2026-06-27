using Shouldly;
using Unchained.Xlsx.Core.Xml;
using Unchained.Xlsx.Models.DataValidation;
using Unchained.Xlsx.Models.Styles;
using Xunit;

namespace Unchained.Xlsx.Tests.UnitTests;

/// <summary>Exhaustive round-trip coverage for the SpreadsheetML enum literal mapping table.</summary>
public class SmlEnumsTests
{
    // ── Horizontal alignment ─────────────────────────────────────────────────

    [
        Theory,
        InlineData(HorizontalAlignment.Left, "left"),
        InlineData(HorizontalAlignment.Center, "center"),
        InlineData(HorizontalAlignment.Right, "right"),
        InlineData(HorizontalAlignment.Fill, "fill"),
        InlineData(HorizontalAlignment.Justify, "justify"),
        InlineData(HorizontalAlignment.CenterAcrossSelection, "centerContinuous"),
        InlineData(HorizontalAlignment.Distributed, "distributed")
    ]
    public void Horizontal_RoundTrips(HorizontalAlignment value, string literal)
    {
        SmlEnums.ToLiteral(value).ShouldBe(literal);
        SmlEnums.ParseHorizontal(literal).ShouldBe(value);
    }

    [Fact]
    public void Horizontal_General_IsNull()
    {
        SmlEnums.ToLiteral(HorizontalAlignment.General).ShouldBeNull();
        SmlEnums.ParseHorizontal(null).ShouldBe(HorizontalAlignment.General);
        SmlEnums.ParseHorizontal("bogus").ShouldBe(HorizontalAlignment.General);
    }

    // ── Vertical alignment ───────────────────────────────────────────────────

    [
        Theory,
        InlineData(VerticalAlignment.Top, "top"),
        InlineData(VerticalAlignment.Center, "center"),
        InlineData(VerticalAlignment.Justify, "justify"),
        InlineData(VerticalAlignment.Distributed, "distributed")
    ]
    public void Vertical_RoundTrips(VerticalAlignment value, string literal)
    {
        SmlEnums.ToLiteral(value).ShouldBe(literal);
        SmlEnums.ParseVertical(literal).ShouldBe(value);
    }

    [Fact]
    public void Vertical_Bottom_IsDefault()
    {
        SmlEnums.ToLiteral(VerticalAlignment.Bottom).ShouldBeNull();
        SmlEnums.ParseVertical(null).ShouldBe(VerticalAlignment.Bottom);
        SmlEnums.ParseVertical("bogus").ShouldBe(VerticalAlignment.Bottom);
    }

    // ── Reading order ────────────────────────────────────────────────────────

    [
        Theory,
        InlineData(1, ReadingOrder.LeftToRight),
        InlineData(2, ReadingOrder.RightToLeft),
        InlineData(0, ReadingOrder.ContextDependent),
        InlineData(null, ReadingOrder.ContextDependent)
    ]
    public void ReadingOrder_Parses(int? value, ReadingOrder expected) =>
        SmlEnums.ParseReadingOrder(value).ShouldBe(expected);

    [Fact]
    public void ReadingOrder_ToLiteral_IsNumeric() =>
        SmlEnums.ToLiteral(ReadingOrder.RightToLeft).ShouldBe((int)ReadingOrder.RightToLeft);

    // ── Font underline ───────────────────────────────────────────────────────

    [
        Theory,
        InlineData(FontUnderline.Single, "single"),
        InlineData(FontUnderline.Double, "double"),
        InlineData(FontUnderline.SingleAccounting, "singleAccounting"),
        InlineData(FontUnderline.DoubleAccounting, "doubleAccounting")
    ]
    public void Underline_RoundTrips(FontUnderline value, string literal)
    {
        SmlEnums.ToLiteral(value).ShouldBe(literal);
        SmlEnums.ParseUnderline(literal).ShouldBe(value);
    }

    [Fact]
    public void Underline_None_IsNull() => SmlEnums.ToLiteral(FontUnderline.None).ShouldBeNull();

    [Fact]
    public void Underline_ParseSpecialCases()
    {
        SmlEnums.ParseUnderline(null).ShouldBe(FontUnderline.Single);   // <u/> with no val
        SmlEnums.ParseUnderline("none").ShouldBe(FontUnderline.None);
        SmlEnums.ParseUnderline("bogus").ShouldBe(FontUnderline.Single);
    }

    // ── Font vertical alignment ──────────────────────────────────────────────

    [
        Theory,
        InlineData(FontVerticalAlignment.Superscript, "superscript"),
        InlineData(FontVerticalAlignment.Subscript, "subscript")
    ]
    public void FontVertical_RoundTrips(FontVerticalAlignment value, string literal)
    {
        SmlEnums.ToLiteral(value).ShouldBe(literal);
        SmlEnums.ParseFontVerticalAlignment(literal).ShouldBe(value);
    }

    [Fact]
    public void FontVertical_None_IsDefault()
    {
        SmlEnums.ToLiteral(FontVerticalAlignment.None).ShouldBeNull();
        SmlEnums.ParseFontVerticalAlignment(null).ShouldBe(FontVerticalAlignment.None);
    }

    // ── Fill pattern (exhaustive) ────────────────────────────────────────────

    [Fact]
    public void FillPattern_AllValues_RoundTrip()
    {
        foreach (var value in Enum.GetValues<FillPattern>())
        {
            var literal = SmlEnums.ToLiteral(value);
            literal.ShouldNotBeNull();
            SmlEnums.ParseFillPattern(literal).ShouldBe(value);
        }
    }

    [Fact]
    public void FillPattern_UnknownLiteral_IsNone() =>
        SmlEnums.ParseFillPattern("bogus").ShouldBe(FillPattern.None);

    // ── Border style (exhaustive) ────────────────────────────────────────────

    [Fact]
    public void BorderStyle_AllValues_RoundTrip()
    {
        foreach (var value in Enum.GetValues<BorderStyle>())
        {
            var literal = SmlEnums.ToLiteral(value);
            if (value == BorderStyle.None)
            {
                literal.ShouldBeNull();
                continue;
            }

            literal.ShouldNotBeNull();
            SmlEnums.ParseBorderStyle(literal).ShouldBe(value);
        }
    }

    [Fact]
    public void BorderStyle_UnknownLiteral_IsNone() =>
        SmlEnums.ParseBorderStyle("bogus").ShouldBe(BorderStyle.None);

    // ── Data validation type ─────────────────────────────────────────────────

    [Fact]
    public void ValidationType_AllValues_RoundTrip()
    {
        foreach (var value in Enum.GetValues<DataValidationType>())
        {
            var literal = SmlEnums.ToLiteral(value);
            if (value == DataValidationType.None)
            {
                literal.ShouldBeNull();
                continue;
            }

            SmlEnums.ParseValidationType(literal).ShouldBe(value);
        }
    }

    [Fact]
    public void ValidationOperator_AllValues_RoundTrip()
    {
        foreach (var value in Enum.GetValues<DataValidationOperator>())
        {
            var literal = SmlEnums.ToLiteral(value);
            if (value == DataValidationOperator.Between)
            {
                literal.ShouldBeNull(); // between is the default
                continue;
            }

            SmlEnums.ParseValidationOperator(literal).ShouldBe(value);
        }
    }

    [Fact]
    public void ValidationErrorStyle_RoundTrips()
    {
        SmlEnums.ToLiteral(DataValidationErrorStyle.Stop).ShouldBeNull();
        SmlEnums.ParseErrorStyle(null).ShouldBe(DataValidationErrorStyle.Stop);
        SmlEnums.ToLiteral(DataValidationErrorStyle.Warning).ShouldBe("warning");
        SmlEnums.ParseErrorStyle("warning").ShouldBe(DataValidationErrorStyle.Warning);
        SmlEnums.ToLiteral(DataValidationErrorStyle.Information).ShouldBe("information");
        SmlEnums.ParseErrorStyle("information").ShouldBe(DataValidationErrorStyle.Information);
    }
}
