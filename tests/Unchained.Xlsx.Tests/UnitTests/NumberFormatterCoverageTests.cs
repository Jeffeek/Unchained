using Shouldly;
using Unchained.Xlsx.Formatting;
using Xunit;

namespace Unchained.Xlsx.Tests.UnitTests;

/// <summary>Additional branch coverage for <c>NumberFormatter</c>.</summary>
public class NumberFormatterCoverageTests
{
    [Fact]
    public void EmptyCode_UsesGeneral() =>
        NumberFormatter.Format(3.5, "", false).ShouldBe("3.5");

    [Fact]
    public void TextPlaceholder_RendersNumber() =>
        NumberFormatter.Format(42, "@", false).ShouldBe("42");

    [Fact]
    public void ZeroSection_IsUsedForZero() =>
        NumberFormatter.Format(0, "0.00;(0.00);\"zero\"", false).ShouldStartWith("zero");

    [Fact]
    public void Scientific_Notation()
    {
        var result = NumberFormatter.Format(12345, "0.00E+00", false);
        result.ShouldContain("E");
    }

    [
        Theory,
        InlineData(1234567, "#,##0", "1,234,567"),
        InlineData(0.5, "0.00%", "50.00%"),
        InlineData(1234.5, "#,##0.00", "1,234.50")
    ]
    public void GroupingAndPercent(double value, string code, string expected) =>
        NumberFormatter.Format(value, code, false).ShouldBe(expected);

    [Fact]
    public void LiteralAffixes_ArePreserved() =>
        // Parentheses around the placeholder body are literal affixes.
        NumberFormatter.Format(5, "(0.00)", false).ShouldBe("(5.00)");

    [Fact]
    public void NegativeWithoutSection_KeepsSign()
    {
        var result = NumberFormatter.Format(-3.5, "0.00", false);
        result.ShouldBe("-3.50");
    }

    // ── Date/time translation ────────────────────────────────────────────────

    [Fact]
    public void DateTime_WithAmPm()
    {
        // 44927.5 = 2023-01-01 12:00 in the 1900 system.
        var result = NumberFormatter.Format(44927.5, "h:mm AM/PM", false);
        result.ShouldContain("PM");
    }

    [Fact]
    public void DateTime_MinutesVsMonths()
    {
        // h:mm → minutes; the m following h must be rendered as minutes (mm), not months.
        var result = NumberFormatter.Format(44927.5, "hh:mm", false);
        result.ShouldStartWith("12:");
        result.Length.ShouldBe(5);
    }

    [Fact]
    public void DateTime_MonthDayYear() => NumberFormatter.Format(44927, "mm/dd/yyyy", false).ShouldBe("01/01/2023");

    [Fact]
    public void DateTime_WithSeconds()
    {
        var result = NumberFormatter.Format(44927.5, "hh:mm:ss", false);
        result.ShouldStartWith("12:");
        result.Length.ShouldBe(8);
    }

    [Fact]
    public void DateTime_QuotedLiteralInFormat()
    {
        var result = NumberFormatter.Format(44927, "yyyy\"年\"", false);
        result.ShouldContain("2023");
        result.ShouldContain("年");
    }

    [Fact]
    public void DateTime_InvalidSerial_FallsBack()
    {
        // A serial well out of range cannot map to a DateTime → invariant fallback.
        var result = NumberFormatter.Format(double.MaxValue, "yyyy-MM-dd", false);
        result.ShouldNotBeNullOrEmpty();
        result.ShouldNotBe("12:00:00 PM"); // ensure it's not a bogus date
    }

    [Fact]
    public void Date1904_System()
    {
        // The same display serial differs between 1900 and 1904 systems; just ensure it formats.
        var result = NumberFormatter.Format(1000, "yyyy-MM-dd", true);
        result.ShouldNotBeNullOrEmpty();
        result.ShouldNotBe("12:00:00 PM");
    }
}
