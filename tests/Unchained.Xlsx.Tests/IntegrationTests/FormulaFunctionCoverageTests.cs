using Shouldly;
using Unchained.Xlsx.Engine;
using Unchained.Xlsx.Models.Cell;
using Unchained.Xlsx.Tests.Helpers;
using Unchained.Xlsx.Worksheets;
using Xunit;

namespace Unchained.Xlsx.Tests.IntegrationTests;

/// <summary>
///     Coverage-focused tests for the breadth of <c>FormulaFunctions</c>: untested functions,
///     error paths (wrong arity, domain errors), and coercion edges.
/// </summary>
public class FormulaFunctionCoverageTests
{
    private static object? Eval(string formula, Action<Worksheet>? setup = null)
    {
        using var document = XlsxFixtures.WithSheets("S");
        var sheet = document.Sheets[0];
        setup?.Invoke(sheet);
        return SpreadsheetDocument.EvaluateFormula(sheet, formula);
    }

    private static double? Num(string formula, Action<Worksheet>? setup = null)
        => Eval(formula, setup) is double d ? d : null;

    // ── Math breadth ─────────────────────────────────────────────────────────

    [
        Theory,
        InlineData("=SUM(1,2,3)", 6),
        InlineData("=PRODUCT(2,3,4)", 24),
        InlineData("=ABS(-7)", 7),
        InlineData("=SQRT(16)", 4),
        InlineData("=POWER(2,10)", 1024),
        InlineData("=INT(4.9)", 4),
        InlineData("=SIGN(-3)", -1),
        InlineData("=MOD(10,3)", 1),
        InlineData("=ROUND(3.14159,2)", 3.14),
        InlineData("=ROUNDUP(3.1,0)", 4),
        InlineData("=ROUNDDOWN(3.9,0)", 3),
        InlineData("=ROUNDUP(-3.1,0)", -4),
        InlineData("=ROUNDDOWN(-3.9,0)", -3),
        InlineData("=PI()", Math.PI),
        InlineData("=CBRT(27)", 3),
        InlineData("=SUMSQ(3,4)", 25),
        InlineData("=ARABIC(\"MMXXIV\")", 2024)
    ]
    public void MathReturnsNumber(string formula, double expected) =>
        Num(formula)!.Value.ShouldBe(expected, 1e-9);

    [
        Theory,
        InlineData("=SQRT(-1)"),
        InlineData("=LN(0)"),
        InlineData("=LOG10(-5)"),
        InlineData("=LOG(0)"),
        InlineData("=FACT(-1)")
    ]
    public void MathDomainErrors_ReturnNumberError(string formula) =>
        Eval(formula).ShouldBe(CellError.Number);

    [Fact]
    public void Mod_ByZero_ReturnsDivisionError() =>
        Eval("=MOD(5,0)").ShouldBe(CellError.DivisionByZero);

    [Fact]
    public void Quotient_ByZero_ReturnsDivisionError() =>
        Eval("=QUOTIENT(5,0)").ShouldBe(CellError.DivisionByZero);

    [
        Theory,
        InlineData("=SEC(0)", 1),
        InlineData("=CSC(1.5707963267948966)", 1),
        InlineData("=ASINH(0)", 0),
        InlineData("=ACOSH(1)", 0),
        InlineData("=ATANH(0)", 0),
        InlineData("=TAN(0)", 0),
        InlineData("=ASIN(0)", 0),
        InlineData("=ACOS(1)", 0),
        InlineData("=ATAN(0)", 0),
        InlineData("=ATAN2(1,0)", 0),
        InlineData("=SINH(0)", 0),
        InlineData("=COSH(0)", 1),
        InlineData("=TANH(0)", 0)
    ]
    public void MoreTrig(string formula, double expected) =>
        Num(formula)!.Value.ShouldBe(expected, 1e-9);

    [Fact]
    public void RandBetween_StaysInRange()
    {
        var value = Num("=RANDBETWEEN(5,5)")!.Value;
        value.ShouldBe(5);
    }

    [Fact]
    public void Rand_IsInUnitInterval()
    {
        var value = Num("=RAND()")!.Value;
        value.ShouldBeGreaterThanOrEqualTo(0);
        value.ShouldBeLessThan(1);
    }

    [Fact]
    public void Base_And_Decimal_RoundTrip()
    {
        Eval("=BASE(0,16,4)").ShouldBe("0000");
        Num("=DECIMAL(\"zz\",36)").ShouldBe(1295);
    }

    // ── Aggregation breadth ──────────────────────────────────────────────────

    [Fact]
    public void Counting()
    {
        Num("=COUNT(A1:A4)", Fill).ShouldBe(2); // number + boolean count as number
        Num("=COUNTA(A1:A4)", Fill).ShouldBe(3);
        Num("=COUNTBLANK(A1:A4)", Fill).ShouldBe(1);
        return;

        static void Fill(Worksheet s)
        {
            s.SetValue(1, 1, 10.0);
            s.SetValue(2, 1, "text");
            s.SetValue(3, 1, true);
            // (4,1) intentionally blank
        }
    }

    [Fact]
    public void AverageA_And_MinMaxA_TreatTextAsZero()
    {
        Num("=AVERAGEA(A1:A2)", Fill).ShouldBe(5);
        Num("=MINA(A1:A2)", Fill).ShouldBe(0);
        Num("=MAXA(A1:A2)", Fill).ShouldBe(10);
        return;

        static void Fill(Worksheet s)
        {
            s.SetValue(1, 1, 10.0);
            s.SetValue(2, 1, "x"); // counts as 0 in *A variants
        }
    }

    [Fact]
    public void MinMax_EmptyRange_ReturnsZero()
    {
        Num("=MIN(A1:A3)").ShouldBe(0);
        Num("=MAX(A1:A3)").ShouldBe(0);
    }

    [Fact]
    public void Average_NoNumbers_ReturnsDivisionError() =>
        Eval("=AVERAGE(A1:A3)").ShouldBe(CellError.DivisionByZero);

    [Fact]
    public void Percentile_And_PercentRank()
    {
        Num("=PERCENTILE(A1:A5,0.5)", Fill).ShouldBe(3);
        Num("=PERCENTRANK(A1:A5,3)", Fill).ShouldBe(0.5);
        return;

        static void Fill(Worksheet s)
        {
            for (var i = 1; i <= 5; i++) s.SetValue(i, 1, i);
        }
    }

    [Fact]
    public void Rank_Ascending_And_Descending()
    {
        Num("=RANK(30,A1:A3)", Fill).ShouldBe(1);   // descending default
        Num("=RANK(30,A1:A3,1)", Fill).ShouldBe(3); // ascending
        return;

        static void Fill(Worksheet s)
        {
            s.SetValue(1, 1, 10.0);
            s.SetValue(2, 1, 30.0);
            s.SetValue(3, 1, 20.0);
        }
    }

    [Fact]
    public void Skew_And_TrimMean()
    {
        Num("=TRIMMEAN(A1:A6,0.5)", Fill)!.Value.ShouldBe(3.5, 1e-9);
        Num("=SKEW(A1:A6)", Fill).ShouldNotBeNull();
        return;

        static void Fill(Worksheet s)
        {
            for (var i = 1; i <= 6; i++) s.SetValue(i, 1, i);
        }
    }

    [Fact]
    public void Mode_NoRepeat_ReturnsNotAvailable() =>
        Eval("=MODE(1,2,3)").ShouldBe(CellError.NotAvailable);

    [Fact]
    public void Median_Empty_ReturnsNumberError() =>
        Eval("=MEDIAN(A1:A3)").ShouldBe(CellError.Number);

    [Fact]
    public void GeoMean_WithNonPositive_ReturnsNumberError() =>
        Eval("=GEOMEAN(2,-1)").ShouldBe(CellError.Number);

    // ── Text breadth ─────────────────────────────────────────────────────────

    [
        Theory,
        InlineData("=LEN(\"hello\")", 5),
        InlineData("=UNICODE(\"A\")", 65)
    ]
    public void TextNumeric(string formula, double expected) => Num(formula)!.Value.ShouldBe(expected);

    [
        Theory,
        InlineData("=LEFT(\"hello\",2)", "he"),
        InlineData("=RIGHT(\"hello\",2)", "lo"),
        InlineData("=MID(\"hello\",2,3)", "ell"),
        InlineData("=UPPER(\"abc\")", "ABC"),
        InlineData("=LOWER(\"ABC\")", "abc"),
        InlineData("=CLEAN(\"a\tb\")", "ab"),
        InlineData("=CONCATENATE(\"a\",\"b\",\"c\")", "abc"),
        InlineData("=CONCAT(\"x\",\"y\")", "xy"),
        InlineData("=UNICHAR(8364)", "€"),
        InlineData("=T(\"text\")", "text"),
        InlineData("=T(5)", "")
    ]
    public void TextReturnsString(string formula, string expected) => Eval(formula).ShouldBe(expected);

    [Fact]
    public void Mid_OutOfRange_ReturnsEmpty() => Eval("=MID(\"abc\",10,2)").ShouldBe("");

    [Fact]
    public void Find_NotFound_ReturnsValueError() => Eval("=FIND(\"z\",\"abc\")").ShouldBe(CellError.Value);

    [Fact]
    public void Find_StartBeyondLength_ReturnsValueError() =>
        Eval("=FIND(\"a\",\"abc\",10)").ShouldBe(CellError.Value);

    [Fact]
    public void Substitute_NthInstance() => Eval("=SUBSTITUTE(\"a-a-a\",\"a\",\"X\",2)").ShouldBe("a-X-a");

    [Fact]
    public void Substitute_EmptyOld_ReturnsOriginal() => Eval("=SUBSTITUTE(\"abc\",\"\",\"X\")").ShouldBe("abc");

    [Fact]
    public void N_CoercesToNumber() => Num("=N(42)").ShouldBe(42);

    [Fact]
    public void Value_ParsesNumber() => Num("=VALUE(\"123.5\")").ShouldBe(123.5);

    [Fact]
    public void NumberValue_ParsesNumber() => Num("=NUMBERVALUE(\"7.25\")").ShouldBe(7.25);

    [Fact]
    public void Dollar_And_Fixed()
    {
        Eval("=DOLLAR(1234.5,2)").ShouldBe("$1,234.50");
        Eval("=FIXED(1234.5,1,TRUE)").ShouldBe("1234.5");
        Eval("=FIXED(1234.5,1)").ShouldBe("1,234.5");
    }

    [Fact]
    public void TextBeforeAfter_NoDelimiter()
    {
        Eval("=TEXTBEFORE(\"abc\",\"-\")").ShouldBe("abc");
        Eval("=TEXTAFTER(\"abc\",\"-\")").ShouldBe("");
    }

    // ── Logical / conditional ────────────────────────────────────────────────

    [Fact]
    public void If_NoFalseBranch_ReturnsFalse() => Eval("=IF(1>2,\"yes\")").ShouldBe(false);

    [Fact]
    public void If_ConditionError_Propagates() => Eval("=IF(1/0,1,2)").ShouldBe(CellError.DivisionByZero);

    [Fact]
    public void If_TooFewArgs_ReturnsValueError() => Eval("=IF(TRUE)").ShouldBe(CellError.Value);

    [Fact]
    public void IfError_CatchesError() => Eval("=IFERROR(1/0,99)").ShouldBe(99.0);

    [Fact]
    public void IfError_PassesThroughGood() => Num("=IFERROR(5,99)").ShouldBe(5);

    [Fact]
    public void Ifs_NoMatch_ReturnsNotAvailable() => Eval("=IFS(FALSE,1,FALSE,2)").ShouldBe(CellError.NotAvailable);

    [Fact]
    public void And_Or_Behaviour()
    {
        Eval("=AND(TRUE,TRUE)").ShouldBe(true);
        Eval("=AND(TRUE,FALSE)").ShouldBe(false);
        Eval("=OR(FALSE,FALSE)").ShouldBe(false);
        Eval("=OR(FALSE,TRUE)").ShouldBe(true);
    }

    [Fact]
    public void Choose_OutOfRange_ReturnsValueError() => Eval("=CHOOSE(9,\"a\",\"b\")").ShouldBe(CellError.Value);

    // ── Lookup error paths ───────────────────────────────────────────────────

    [Fact]
    public void Vlookup_NotFound_ReturnsNotAvailable() =>
        Eval(
                "=VLOOKUP(\"Z\",A1:B2,2,FALSE)",
                static s =>
                {
                    s.SetValue(1, 1, "A");
                    s.SetValue(1, 2, 1.0);
                    s.SetValue(2, 1, "B");
                    s.SetValue(2, 2, 2.0);
                }
            )
            .ShouldBe(CellError.NotAvailable);

    [Fact]
    public void Vlookup_BadColumn_ReturnsReferenceError() =>
        Eval(
                "=VLOOKUP(\"A\",A1:B2,5,FALSE)",
                static s =>
                {
                    s.SetValue(1, 1, "A");
                    s.SetValue(1, 2, 1.0);
                    s.SetValue(2, 1, "B");
                    s.SetValue(2, 2, 2.0);
                }
            )
            .ShouldBe(CellError.Reference);

    [Fact]
    public void Hlookup_FindsAcrossColumns() =>
        Eval(
                "=HLOOKUP(\"B\",A1:C2,2,FALSE)",
                static s =>
                {
                    s.SetValue(1, 1, "A");
                    s.SetValue(1, 2, "B");
                    s.SetValue(1, 3, "C");
                    s.SetValue(2, 1, 10.0);
                    s.SetValue(2, 2, 20.0);
                    s.SetValue(2, 3, 30.0);
                }
            )
            .ShouldBe(20.0);

    [Fact]
    public void Match_DescendingType()
    {
        Num("=MATCH(20,A1:A3,-1)", Fill).ShouldBe(2);
        return;

        static void Fill(Worksheet s)
        {
            s.SetValue(1, 1, 30.0);
            s.SetValue(2, 1, 20.0);
            s.SetValue(3, 1, 10.0);
        }
    }

    [Fact]
    public void Match_NotFound_ReturnsNotAvailable() =>
        Eval("=MATCH(99,A1:A3,0)", static s => s.SetValue(1, 1, 1.0)).ShouldBe(CellError.NotAvailable);

    [Fact]
    public void Index_OutOfBounds_ReturnsReferenceError() =>
        Eval("=INDEX(A1:A2,9)", static s => s.SetValue(1, 1, 1.0)).ShouldBe(CellError.Reference);

    // ── Date / time breadth ──────────────────────────────────────────────────

    [Fact]
    public void TimeFunctions()
    {
        Num("=HOUR(DATE(2023,1,1)+0.5)").ShouldBe(12);
        Num("=MINUTE(DATE(2023,1,1)+0.5)").ShouldBe(0);
        Num("=SECOND(DATE(2023,1,1))").ShouldBe(0);
        Num("=TIME(6,30,0)")!.Value.ShouldBe(0.27083333, 1e-6);
    }

    [Fact]
    public void Weekday_Variants()
    {
        // 2023-06-15 is a Thursday.
        Num("=WEEKDAY(DATE(2023,6,15))").ShouldBe(5);   // Sunday=1
        Num("=WEEKDAY(DATE(2023,6,15),2)").ShouldBe(4); // Monday=1
        Num("=WEEKDAY(DATE(2023,6,15),3)").ShouldBe(3); // Monday=0
    }

    [Fact]
    public void DateValue_And_TimeValue()
    {
        Num("=DATEVALUE(\"2023-06-15\")").ShouldBe(Num("=DATE(2023,6,15)"));
        Num("=TIMEVALUE(\"12:00:00\")")!.Value.ShouldBe(0.5, 1e-9);
    }

    [Fact]
    public void DateValue_Invalid_ReturnsValueError() => Eval("=DATEVALUE(\"not a date\")").ShouldBe(CellError.Value);

    [Fact]
    public void WeekNum_And_Days360()
    {
        Num("=WEEKNUM(DATE(2023,1,1))").ShouldNotBeNull();
        Num("=DAYS360(DATE(2023,1,31),DATE(2023,3,31))").ShouldBe(60);
    }

    [Fact]
    public void YearFrac() => Num("=YEARFRAC(DATE(2023,1,1),DATE(2024,1,1))")!.Value.ShouldBe(1.0, 0.01);

    [Fact]
    public void Date_Invalid_ReturnsNumberError() => Eval("=YEAR(\"abc\")").ShouldBe(CellError.Number);

    // ── Financial ────────────────────────────────────────────────────────────

    [Fact]
    public void Financial_ZeroRate()
    {
        Num("=PMT(0,10,1000)").ShouldBe(-100);
        Num("=FV(0,10,-100)").ShouldBe(1000);
        Num("=PV(0,10,-100)").ShouldBe(1000);
        Num("=NPER(0,-100,1000)").ShouldBe(10);
    }

    [Fact]
    public void Financial_SLN_And_Effect()
    {
        Num("=SLN(10000,1000,5)").ShouldBe(1800);
        Num("=EFFECT(0.1,4)")!.Value.ShouldBe(0.103812890625, 1e-9);
    }

    [Fact]
    public void NPer_NonZeroRate() => Num("=NPER(0.05,-100,1000)").ShouldNotBeNull();

    // ── Information ──────────────────────────────────────────────────────────

    [Fact]
    public void IsFunctions()
    {
        Eval("=ISERROR(1/0)").ShouldBe(true);
        Eval("=ISERR(1/0)").ShouldBe(true);
        Eval("=ISERR(NA())").ShouldBe(false); // #N/A is not "err"
        Eval("=ISNUMBER(5)").ShouldBe(true);
        Eval("=ISTEXT(\"x\")").ShouldBe(true);
        Eval("=ISNONTEXT(5)").ShouldBe(true);
        Eval("=ISBLANK(A1)").ShouldBe(true);
        Eval("=ISREF(A1)").ShouldBe(false);
        Eval("=ISFORMULA(A1)").ShouldBe(false);
    }

    [Fact]
    public void TypeOf_Variants()
    {
        Num("=TYPE(5)").ShouldBe(2);
        Num("=TYPE(\"x\")").ShouldBe(4);
        Num("=TYPE(TRUE)").ShouldBe(16);
        Num("=TYPE(1/0)").ShouldBe(16);
    }

    [
        Theory,
        InlineData("=ERROR.TYPE(1/0)", 2),
        InlineData("=ERROR.TYPE(NA())", 7)
    ]
    public void ErrorTypeCodes(string formula, double expected) => Num(formula)!.Value.ShouldBe(expected);

    [Fact]
    public void ErrorType_NoError_ReturnsNotAvailable() => Eval("=ERROR.TYPE(5)").ShouldBe(CellError.NotAvailable);

    // ── Unknown function & blank ─────────────────────────────────────────────

    [Fact]
    public void UnknownFunction_ReturnsNameError() => Eval("=BOGUSFN(1)").ShouldBe(CellError.Name);

    [Fact]
    public void BlankCellReference_ReturnsNull() => Eval("=A1").ShouldBeNull();
}
