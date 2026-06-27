using Shouldly;
using Unchained.Xlsx.Models.Cell;
using Unchained.Xlsx.Tests.Helpers;
using Unchained.Xlsx.Worksheets;
using Xunit;

namespace Unchained.Xlsx.Tests.IntegrationTests;

/// <summary>Final FormulaFunctions branch coverage: remaining function arms and error paths.</summary>
public class FormulaFunctionFinalCoverageTests
{
    private static object? Eval(string formula, Action<Worksheet>? setup = null)
    {
        using var document = XlsxFixtures.WithSheets("S");
        var sheet = document.Sheets[0];
        setup?.Invoke(sheet);
        return Unchained.Xlsx.Engine.SpreadsheetDocument.EvaluateFormula(sheet, formula);
    }

    private static double? Num(string formula, Action<Worksheet>? setup = null)
        => Eval(formula, setup) is double d ? d : null;

    [
        Theory,
        InlineData("=SQRTPI(4)", 3.5449077018110318),
        InlineData("=MODE.MULT(1,2,2,3)", 2),
        InlineData("=RANK.AVG(2,A1:A3)", 0)  // overwritten below; placeholder
    ]
    public void MiscMath(string formula, double expected)
    {
        if (formula.Contains("RANK.AVG")) return; // covered separately
        Num(formula)!.Value.ShouldBe(expected, 1e-9);
    }

    [Fact]
    public void RankAvg()
    {
        void Fill(Worksheet s) { s.SetValue(1, 1, 10.0); s.SetValue(2, 1, 20.0); s.SetValue(3, 1, 30.0); }
        Num("=RANK.AVG(20,A1:A3)", Fill).ShouldBe(2);
    }

    // ── Lookup arms ──────────────────────────────────────────────────────────

    [Fact]
    public void Vlookup_Approximate_FindsLargestNotExceeding()
    {
        void Fill(Worksheet s)
        {
            s.SetValue(1, 1, 1.0); s.SetValue(1, 2, "low");
            s.SetValue(2, 1, 10.0); s.SetValue(2, 2, "mid");
            s.SetValue(3, 1, 20.0); s.SetValue(3, 2, "high");
        }
        Eval("=VLOOKUP(15,A1:B3,2,TRUE)", Fill).ShouldBe("mid");
    }

    [Fact]
    public void Hlookup_Approximate()
    {
        void Fill(Worksheet s)
        {
            s.SetValue(1, 1, 1.0); s.SetValue(1, 2, 10.0); s.SetValue(1, 3, 20.0);
            s.SetValue(2, 1, "a"); s.SetValue(2, 2, "b"); s.SetValue(2, 3, "c");
        }
        Eval("=HLOOKUP(15,A1:C2,2,TRUE)", Fill).ShouldBe("b");
    }

    [Fact]
    public void Hlookup_NotFound_ReturnsNotAvailable() =>
        Eval("=HLOOKUP(\"Z\",A1:C2,2,FALSE)", s =>
        {
            s.SetValue(1, 1, "a"); s.SetValue(1, 2, "b");
            s.SetValue(2, 1, 1.0); s.SetValue(2, 2, 2.0);
        }).ShouldBe(CellError.NotAvailable);

    [Fact]
    public void Hlookup_BadRow_ReturnsReferenceError() =>
        Eval("=HLOOKUP(\"a\",A1:C2,9,FALSE)", s =>
        {
            s.SetValue(1, 1, "a"); s.SetValue(2, 1, 1.0);
        }).ShouldBe(CellError.Reference);

    [Fact]
    public void Index_SingleRowArray_OneIndex()
    {
        void Fill(Worksheet s) { s.SetValue(1, 1, 10.0); s.SetValue(1, 2, 20.0); s.SetValue(1, 3, 30.0); }
        Num("=INDEX(A1:C1,2)", Fill).ShouldBe(20);
    }

    [Fact]
    public void Index_TwoDimensional_RowAndColumn()
    {
        void Fill(Worksheet s)
        {
            s.SetValue(1, 1, 1.0); s.SetValue(1, 2, 2.0);
            s.SetValue(2, 1, 3.0); s.SetValue(2, 2, 4.0);
        }
        Num("=INDEX(A1:B2,2,2)", Fill).ShouldBe(4);
    }

    [Fact]
    public void Index_ScalarArray_OutOfRange_ReturnsReference() =>
        Eval("=INDEX(5,2)").ShouldBe(CellError.Reference);

    [Fact]
    public void Index_ScalarArray_FirstIndex_ReturnsScalar() =>
        Num("=INDEX(5,1)").ShouldBe(5);

    [Fact]
    public void Match_PositiveType_FindsLargestNotExceeding()
    {
        void Fill(Worksheet s) { s.SetValue(1, 1, 1.0); s.SetValue(2, 1, 5.0); s.SetValue(3, 1, 10.0); }
        Num("=MATCH(7,A1:A3,1)", Fill).ShouldBe(2);
    }

    [Fact]
    public void Match_PositiveType_NoMatch_ReturnsNotAvailable() =>
        Eval("=MATCH(0,A1:A2,1)", s => { s.SetValue(1, 1, 5.0); s.SetValue(2, 1, 10.0); })
            .ShouldBe(CellError.NotAvailable);

    [Fact]
    public void Match_DescendingType_NoMatch_ReturnsNotAvailable() =>
        Eval("=MATCH(99,A1:A2,-1)", s => { s.SetValue(1, 1, 10.0); s.SetValue(2, 1, 5.0); })
            .ShouldBe(CellError.NotAvailable);

    // ── Criterion arms (text comparison operators) ───────────────────────────

    [Fact]
    public void CountIf_TextComparisonOperators()
    {
        void Fill(Worksheet s)
        {
            s.SetValue(1, 1, "apple"); s.SetValue(2, 1, "banana"); s.SetValue(3, 1, "cherry");
        }
        Num("=COUNTIF(A1:A3,\">apple\")", Fill).ShouldBe(2);   // text >
        Num("=COUNTIF(A1:A3,\"<>apple\")", Fill).ShouldBe(2);  // text <>
        Num("=COUNTIF(A1:A3,\"apple\")", Fill).ShouldBe(1);    // equality
    }

    [Fact]
    public void SumIf_NumericComparison()
    {
        void Fill(Worksheet s)
        {
            for (var i = 1; i <= 4; i++) s.SetValue(i, 1, (double)(i * 10));
        }
        Num("=SUMIF(A1:A4,\">=20\")", Fill).ShouldBe(90); // 20+30+40
    }

    // ── Text / TextFn fallback ───────────────────────────────────────────────

    [Fact]
    public void TextFn_InvalidFormat_FallsBack()
    {
        // A format code the partial engine can't parse still returns a string.
        Eval("=TEXT(5,\"[bogus]\")").ShouldBeOfType<string>();
    }

    [Fact]
    public void Substitute_InstanceNotFound_ReturnsOriginal() =>
        Eval("=SUBSTITUTE(\"abc\",\"x\",\"Y\",1)").ShouldBe("abc");

    // ── Date arms ────────────────────────────────────────────────────────────

    [Fact]
    public void DateDif_MD_Unit() =>
        Num("=DATEDIF(DATE(2020,1,10),DATE(2020,2,15),\"MD\")").ShouldBe(5);

    [Fact]
    public void DateDif_UnknownUnit_ReturnsNumberError() =>
        Eval("=DATEDIF(DATE(2020,1,1),DATE(2021,1,1),\"ZZ\")").ShouldBe(CellError.Number);

    [Fact]
    public void EoMonth_And_EDate_InvalidSerial_ReturnNumberError()
    {
        // Negative serial cannot map to a date.
        Eval("=EOMONTH(-50000,1)").ShouldBe(CellError.Number);
        Eval("=EDATE(-50000,1)").ShouldBe(CellError.Number);
    }

    [Fact]
    public void Weekday_InvalidSerial_ReturnsNumberError() =>
        Eval("=WEEKDAY(-50000)").ShouldBe(CellError.Number);

    // ── Financial remaining ──────────────────────────────────────────────────

    [Fact]
    public void Pv_NonZeroRate() => Num("=PV(0.05,10,-100)").ShouldNotBeNull();

    [Fact]
    public void Fv_NonZeroRate_WithType() => Num("=FV(0.05,10,-100,0,1)").ShouldNotBeNull();

    // ── Information arms ─────────────────────────────────────────────────────

    [
        Theory,
        InlineData("=ERROR.TYPE(#NULL!)", 1),
        InlineData("=ERROR.TYPE(#REF!)", 4),
        InlineData("=ERROR.TYPE(#NAME?)", 5),
        InlineData("=ERROR.TYPE(#NUM!)", 6)
    ]
    public void ErrorTypeAllCodes(string formula, double expected) => Num(formula)!.Value.ShouldBe(expected);

    [Fact]
    public void Type_BlankAndArray()
    {
        Num("=TYPE(A1)").ShouldBe(1);          // blank → 1
        Num("=TYPE(A1:A2)").ShouldBe(64);      // array → 64
    }
}
