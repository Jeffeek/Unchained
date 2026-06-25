using Shouldly;
using Unchained.Xlsx.Tests.Helpers;
using Xunit;

namespace Unchained.Xlsx.Tests.IntegrationTests;

public class FormulaFunctionLibraryExtraTests
{
    private static object? Eval(string formula, Action<Unchained.Xlsx.Worksheets.Worksheet>? setup = null)
    {
        using var document = XlsxFixtures.WithSheets("S");
        var sheet = document.Sheets[0];
        setup?.Invoke(sheet);
        return Unchained.Xlsx.Engine.SpreadsheetDocument.EvaluateFormula(sheet, formula);
    }

    private static double? Num(string f, Action<Unchained.Xlsx.Worksheets.Worksheet>? setup = null)
        => Eval(f, setup) is double d ? d : null;

    [
        Theory,
        InlineData("=SUMSQ(2,3,4)", 29),
        InlineData("=COMBIN(5,2)", 10),
        InlineData("=PERMUT(5,2)", 20),
        InlineData("=FACTDOUBLE(7)", 105),
        InlineData("=CEILING.MATH(6.3)", 7),
        InlineData("=FLOOR.MATH(6.7)", 6),
        InlineData("=DECIMAL(\"FF\",16)", 255),
        InlineData("=COT(1)", 0.6420926159343306)
    ]
    public void Math(string f, double expected) => Num(f)!.Value.ShouldBe(expected, 1e-9);

    [
        Theory,
        InlineData("=ROMAN(2024)", "MMXXIV"),
        InlineData("=BASE(255,16)", "FF"),
        InlineData("=TEXTBEFORE(\"a-b-c\",\"-\")", "a"),
        InlineData("=TEXTAFTER(\"a-b-c\",\"-\")", "b-c")
    ]
    public void Text(string f, string expected) => Eval(f).ShouldBe(expected);

    [
        Theory,
        InlineData("=GEOMEAN(2,8)", 4),
        InlineData("=HARMEAN(1,2,4)", 1.7142857142857142),
        InlineData("=DEVSQ(2,4,6)", 8),
        InlineData("=AVEDEV(2,4,6)", 1.3333333333333333)
    ]
    public void Statistics(string f, double expected) => Num(f)!.Value.ShouldBe(expected, 1e-9);

    [Fact]
    public void Quartile()
    {
        Num("=QUARTILE(A1:A5,2)", Fill).ShouldBe(3); // median
        return;
        static void Fill(Unchained.Xlsx.Worksheets.Worksheet s)
        {
            for (var i = 1; i <= 5; i++) s.SetValue(i, 1, (double)i);
        }
    }

    [Fact]
    public void MaxIfsMinIfs()
    {
        void Fill(Unchained.Xlsx.Worksheets.Worksheet s)
        {
            for (var i = 1; i <= 4; i++) { s.SetValue(i, 1, (double)(i * 10)); s.SetValue(i, 2, i <= 2 ? "A" : "B"); }
        }

        Num("=MAXIFS(A1:A4,B1:B4,\"A\")", Fill).ShouldBe(20);
        Num("=MINIFS(A1:A4,B1:B4,\"B\")", Fill).ShouldBe(30);
    }

    [Fact]
    public void DateTime()
    {
        Num("=EDATE(DATE(2023,1,31),1)").ShouldBe(Num("=DATE(2023,2,28)"));
        Num("=DAYS(DATE(2023,1,10),DATE(2023,1,1))").ShouldBe(9);
        Num("=ISOWEEKNUM(DATE(2023,1,2))").ShouldBe(1);
        Num("=DATEDIF(DATE(2020,1,1),DATE(2023,1,1),\"Y\")").ShouldBe(3);
    }

    [Fact]
    public void Financial()
    {
        // PMT for a $10,000 loan, 5%/yr over 10 periods is ~ -1295.05.
        Num("=PMT(0.05,10,10000)")!.Value.ShouldBe(-1295.0457, 1e-3);
        // FV of 10 payments of -100 at 5% is ~ 1257.79.
        Num("=FV(0.05,10,-100)")!.Value.ShouldBe(1257.789, 1e-2);
        Num("=NPV(0.1,100,100,100)")!.Value.ShouldBe(248.685, 1e-2);
    }

    [Fact]
    public void Information()
    {
        Num("=TYPE(42)").ShouldBe(1);
        Num("=TYPE(\"x\")").ShouldBe(2);
        Num("=ERROR.TYPE(1/0)").ShouldBe(2);
    }
}
