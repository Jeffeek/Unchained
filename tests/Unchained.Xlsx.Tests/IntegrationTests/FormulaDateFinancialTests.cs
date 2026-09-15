using Shouldly;
using Unchained.Xlsx.Engine;
using Unchained.Xlsx.Tests.Helpers;
using Xunit;

namespace Unchained.Xlsx.Tests.IntegrationTests;

/// <summary>
///     Exercises the date and financial worksheet functions: DATEDIF units, YEARFRAC bases,
///     ROMAN/ARABIC, WEEKDAY types, EOMONTH/EDATE, and PMT/FV/PV/NPER.
/// </summary>
public sealed class FormulaDateFinancialTests
{
    private static object? Eval(string formula)
    {
        using var document = XlsxFixtures.WithSheets("Sheet1");
        return SpreadsheetDocument.EvaluateFormula(document.Sheets[0], formula);
    }

    private static double Num(string formula) => (double)Eval(formula)!;

    [
        Theory,
        InlineData("=DATEDIF(DATE(2020,1,1),DATE(2023,1,1),\"Y\")", 3),
        InlineData("=DATEDIF(DATE(2023,1,1),DATE(2023,3,1),\"M\")", 2),
        InlineData("=DATEDIF(DATE(2023,1,1),DATE(2023,1,11),\"D\")", 10),
        InlineData("=DATEDIF(DATE(2023,1,10),DATE(2023,4,20),\"YM\")", 3)
    ]
    public void DateDif_ExactUnits(string formula, double expected) => Num(formula).ShouldBe(expected);

    [
        Theory,
        InlineData("=DATEDIF(DATE(2023,1,10),DATE(2023,3,15),\"MD\")"),
        InlineData("=DATEDIF(DATE(2023,1,10),DATE(2024,3,15),\"YD\")")
    ]
    public void DateDif_QuirkyUnits_ReturnNumber(string formula) => Eval(formula).ShouldBeOfType<double>();

    [
        Theory,
        InlineData("=YEARFRAC(DATE(2023,1,1),DATE(2024,1,1),0)", 1.0),
        InlineData("=YEARFRAC(DATE(2023,1,1),DATE(2024,1,1),1)", 1.0),
        InlineData("=YEARFRAC(DATE(2023,1,1),DATE(2024,1,1),2)", 1.0),
        InlineData("=YEARFRAC(DATE(2023,1,1),DATE(2024,1,1),3)", 1.0),
        InlineData("=YEARFRAC(DATE(2023,1,1),DATE(2024,1,1),4)", 1.0)
    ]
    public void YearFrac_FullYearBases(string formula, double expected) => Num(formula).ShouldBe(expected, 1e-6);

    [Fact]
    public void Roman_FormatsSubtractivePairs() => Eval("=ROMAN(1994)").ShouldBe("MCMXCIV");

    [Fact]
    public void Arabic_ParsesRomanNumeral() => Num("=ARABIC(\"MCMXCIV\")").ShouldBe(1994);

    [
        Theory,
        InlineData("=WEEKDAY(DATE(2023,1,1))", 1),   // Sunday, type 1
        InlineData("=WEEKDAY(DATE(2023,1,1),2)", 7), // Monday-based
        InlineData("=WEEKDAY(DATE(2023,1,1),3)", 6)  // Monday=0-based
    ]
    public void Weekday_Types(string formula, double expected) => Num(formula).ShouldBe(expected);

    [Fact]
    public void EoMonth_ReturnsLastDayOfShiftedMonth() =>
        Num("=EOMONTH(DATE(2023,1,15),1)").ShouldBe(Num("=DATE(2023,2,28)"));

    [Fact]
    public void EDate_ClampsToShorterMonth() =>
        Num("=EDATE(DATE(2023,1,31),1)").ShouldBe(Num("=DATE(2023,2,28)"));

    [
        Theory,
        InlineData("=PMT(0,10,1000)", -100),
        InlineData("=FV(0,10,-100)", 1000),
        InlineData("=PV(0,10,-100)", 1000),
        InlineData("=NPER(0,-100,1000)", 10)
    ]
    public void ZeroRateFinancials(string formula, double expected) => Num(formula).ShouldBe(expected, 1e-6);

    [Fact]
    public void Pmt_WithRateAndTypeArg_IsNegative() => Num("=PMT(0.1,10,1000,0,1)").ShouldBeLessThan(0);
}
