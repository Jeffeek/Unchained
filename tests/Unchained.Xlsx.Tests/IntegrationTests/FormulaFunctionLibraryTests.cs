using Shouldly;
using Unchained.Xlsx.Models.Cell;
using Unchained.Xlsx.Tests.Helpers;
using Xunit;

namespace Unchained.Xlsx.Tests.IntegrationTests;

public class FormulaFunctionLibraryTests
{
    private static object? Eval(string formula, Action<Unchained.Xlsx.Worksheets.Worksheet>? setup = null)
    {
        using var document = XlsxFixtures.WithSheets("S");
        var sheet = document.Sheets[0];
        setup?.Invoke(sheet);
        return Unchained.Xlsx.Engine.SpreadsheetDocument.EvaluateFormula(sheet, formula);
    }

    private static double? Num(string formula, Action<Unchained.Xlsx.Worksheets.Worksheet>? setup = null)
        => Eval(formula, setup) is double d ? d : null;

    [
        Theory,
        InlineData("=CEILING(4.3,1)", 5),
        InlineData("=FLOOR(4.7,1)", 4),
        InlineData("=TRUNC(4.78,1)", 4.7),
        InlineData("=EXP(0)", 1),
        InlineData("=LN(2.718281828459045)", 1),
        InlineData("=LOG10(1000)", 3),
        InlineData("=LOG(8,2)", 3),
        InlineData("=GCD(12,18)", 6),
        InlineData("=LCM(4,6)", 12),
        InlineData("=FACT(5)", 120),
        InlineData("=QUOTIENT(17,5)", 3),
        InlineData("=EVEN(3)", 4),
        InlineData("=ODD(2)", 3),
        InlineData("=MROUND(10,3)", 9)
    ]
    public void Math(string formula, double expected) => Num(formula)!.Value.ShouldBe(expected, 1e-9);

    [
        Theory,
        InlineData("=DEGREES(3.141592653589793)", 180),
        InlineData("=RADIANS(180)", 3.141592653589793),
        InlineData("=SIN(0)", 0),
        InlineData("=COS(0)", 1)
    ]
    public void Trig(string formula, double expected) => Num(formula)!.Value.ShouldBe(expected, 1e-9);

    [
        Theory,
        InlineData("=MEDIAN(1,2,3,4,5)", 3),
        InlineData("=MEDIAN(1,2,3,4)", 2.5),
        InlineData("=MODE(1,2,2,3,3,3)", 3),
        InlineData("=STDEV(2,4,4,4,5,5,7,9)", 2.138089935299395),
        InlineData("=STDEVP(2,4,4,4,5,5,7,9)", 2),
        InlineData("=VARP(2,4,4,4,5,5,7,9)", 4)
    ]
    public void Statistics(string formula, double expected) => Num(formula)!.Value.ShouldBe(expected, 1e-9);

    [Fact]
    public void LargeSmall()
    {
        Num("=LARGE(A1:A5,1)", Fill).ShouldBe(50);
        Num("=LARGE(A1:A5,2)", Fill).ShouldBe(40);
        Num("=SMALL(A1:A5,1)", Fill).ShouldBe(10);
        return;
        static void Fill(Unchained.Xlsx.Worksheets.Worksheet s)
        {
            s.SetValue(1, 1, 10.0); s.SetValue(2, 1, 50.0); s.SetValue(3, 1, 30.0);
            s.SetValue(4, 1, 40.0); s.SetValue(5, 1, 20.0);
        }
    }

    [
        Theory,
        InlineData("=PROPER(\"hello world\")", "Hello World"),
        InlineData("=TRIM(\"  a   b  \")", "a b"),
        InlineData("=REPT(\"ab\",3)", "ababab"),
        InlineData("=SUBSTITUTE(\"a-b-c\",\"-\",\"+\")", "a+b+c"),
        InlineData("=REPLACE(\"abcdef\",2,3,\"XY\")", "aXYef"),
        InlineData("=TEXTJOIN(\"-\",TRUE,\"a\",\"b\",\"c\")", "a-b-c"),
        InlineData("=CHAR(65)", "A")
    ]
    public void Text(string formula, string expected) => Eval(formula).ShouldBe(expected);

    [
        Theory,
        InlineData("=FIND(\"b\",\"abc\")", 2),
        InlineData("=SEARCH(\"B\",\"abc\")", 2),
        InlineData("=CODE(\"A\")", 65)
    ]
    public void TextNumeric(string formula, double expected) => Num(formula)!.Value.ShouldBe(expected);

    [Fact]
    public void Logical()
    {
        Eval("=XOR(TRUE,FALSE)").ShouldBe(true);
        Eval("=XOR(TRUE,TRUE)").ShouldBe(false);
        Eval("=IFS(FALSE,1,TRUE,2)").ShouldBe(2.0);
        Eval("=SWITCH(2,1,\"a\",2,\"b\",\"z\")").ShouldBe("b");
        Eval("=SWITCH(9,1,\"a\",\"default\")").ShouldBe("default");
        Eval("=NOT(FALSE)").ShouldBe(true);
    }

    [Fact]
    public void Lookup_Vlookup()
    {
        Eval("=VLOOKUP(\"B\",A1:B3,2,FALSE)", s =>
        {
            s.SetValue(1, 1, "A"); s.SetValue(1, 2, 10.0);
            s.SetValue(2, 1, "B"); s.SetValue(2, 2, 20.0);
            s.SetValue(3, 1, "C"); s.SetValue(3, 2, 30.0);
        }).ShouldBe(20.0);
    }

    [Fact]
    public void Lookup_IndexMatch()
    {
        void Fill(Unchained.Xlsx.Worksheets.Worksheet s)
        {
            s.SetValue(1, 1, "X"); s.SetValue(2, 1, "Y"); s.SetValue(3, 1, "Z");
            s.SetValue(1, 2, 100.0); s.SetValue(2, 2, 200.0); s.SetValue(3, 2, 300.0);
        }

        Num("=MATCH(\"Y\",A1:A3,0)", Fill).ShouldBe(2);
        Num("=INDEX(B1:B3,2)", Fill).ShouldBe(200.0);
        Num("=INDEX(B1:B3,MATCH(\"Z\",A1:A3,0))", Fill).ShouldBe(300.0);
    }

    [Fact]
    public void Lookup_Choose() => Eval("=CHOOSE(2,\"a\",\"b\",\"c\")").ShouldBe("b");

    [Fact]
    public void ConditionalAggregates()
    {
        void Fill(Unchained.Xlsx.Worksheets.Worksheet s)
        {
            for (var i = 1; i <= 4; i++) { s.SetValue(i, 1, (double)(i * 10)); s.SetValue(i, 2, i <= 2 ? "A" : "B"); }
        }

        Num("=SUMIFS(A1:A4,B1:B4,\"A\")", Fill).ShouldBe(30);   // 10+20
        Num("=COUNTIFS(B1:B4,\"B\")", Fill).ShouldBe(2);
        Num("=AVERAGEIF(A1:A4,\">15\")", Fill).ShouldBe(30);    // (20+30+40)/3
        Num("=SUMPRODUCT(A1:A4,A1:A4)", Fill).ShouldBe(100 + 400 + 900 + 1600);
    }

    [Fact]
    public void DateFunctions()
    {
        // DATE(2023,6,15) → YEAR/MONTH/DAY round-trip.
        Num("=YEAR(DATE(2023,6,15))").ShouldBe(2023);
        Num("=MONTH(DATE(2023,6,15))").ShouldBe(6);
        Num("=DAY(DATE(2023,6,15))").ShouldBe(15);
    }

    [Fact]
    public void Information()
    {
        Eval("=ISEVEN(4)").ShouldBe(true);
        Eval("=ISODD(4)").ShouldBe(false);
        Eval("=ISLOGICAL(TRUE)").ShouldBe(true);
        Eval("=ISNA(NA())").ShouldBe(true);
        Eval("=IFNA(NA(),\"fallback\")").ShouldBe("fallback");
    }
}
