using Shouldly;
using Unchained.Xlsx.Formatting;
using Xunit;

namespace Unchained.Xlsx.Tests.UnitTests;

public class NumberFormatterTests
{
    [
        Theory,
        InlineData(3.14159, "0.00", "3.14"),
        InlineData(3.0, "0", "3"),
        InlineData(1234.5, "#,##0.00", "1,234.50"),
        InlineData(1234.0, "#,##0", "1,234"),
        InlineData(0.75, "0%", "75%"),
        InlineData(0.7525, "0.00%", "75.25%")
    ]
    public void Format_Numeric(double value, string code, string expected) =>
        NumberFormatter.Format(value, code, date1904: false).ShouldBe(expected);

    [Fact]
    public void Format_General_UsesInvariant() =>
        NumberFormatter.Format(3.14, "General", date1904: false).ShouldBe("3.14");

    [
        Theory,
        InlineData("0.00", false),
        InlineData("#,##0", false),
        InlineData("dd/MM/yyyy", true),
        InlineData("h:mm:ss", true),
        InlineData("0.00 \"days\"", false),
        InlineData("[Red]0.00", false),
        InlineData("mmm-yy", true)
    ]
    public void IsDateTimeFormatCode_Detects(string code, bool expected) =>
        NumberFormatter.IsDateTimeFormatCode(code).ShouldBe(expected);

    [Fact]
    public void Format_Date()
    {
        // 44927 = 1 Jan 2023 in the 1900 system.
        NumberFormatter.Format(44927, "yyyy-MM-dd", date1904: false).ShouldBe("2023-01-01");
    }

    [Fact]
    public void Format_NegativeUsesSecondSection() =>
        NumberFormatter.Format(-5, "0.00;(0.00)", date1904: false).ShouldBe("(5.00)");
}
