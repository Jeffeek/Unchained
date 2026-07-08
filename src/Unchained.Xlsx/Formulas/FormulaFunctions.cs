using System.Globalization;
using Unchained.Xlsx.Formatting;
using Unchained.Xlsx.Models.Cell;

namespace Unchained.Xlsx.Formulas;

/// <summary>
///     The built-in formula function library — a broad, practical subset of the Excel function set
///     spanning math, statistics, text, logical, lookup, date/time, and information categories.
///     Functions needing lazy argument evaluation (IF, AND, conditional aggregates, lookups) are
///     dispatched first; the remainder are evaluated eagerly. Volatile functions (RAND, TODAY, NOW)
///     are supported but not re-evaluated on a schedule. This is not the full 450+ Excel library, but
///     covers the vast majority of real-world workbooks.
/// </summary>
internal static partial class FormulaFunctions
{
    private static readonly Random SharedRandom = new();

    public static FormulaValue Invoke(string name, IReadOnlyList<FormulaNode> args, FormulaEvaluator evaluator)
    {
        var upper = name.ToUpperInvariant();

        // ── Functions with lazy / structural argument handling ──────────────────
        switch (upper)
        {
            case "IF": return If(args, evaluator);
            case "IFS": return Ifs(args, evaluator);
            case "IFERROR": return IfError(args, evaluator, false);
            case "IFNA": return IfError(args, evaluator, true);
            case "SWITCH": return Switch(args, evaluator);
            case "AND": return BoolAggregate(args, evaluator, BoolMode.And);
            case "OR": return BoolAggregate(args, evaluator, BoolMode.Or);
            case "XOR": return BoolAggregate(args, evaluator, BoolMode.Xor);
            case "TRUE": return FormulaValue.FromBoolean(true);
            case "FALSE": return FormulaValue.FromBoolean(false);
            case "SUMIF": return SumIf(args, evaluator);
            case "AVERAGEIF": return AverageIf(args, evaluator);
            case "COUNTIF": return CountIf(args, evaluator);
            case "SUMIFS": return ConditionalSumIfs(args, evaluator, false);
            case "AVERAGEIFS": return ConditionalSumIfs(args, evaluator, true);
            case "COUNTIFS": return CountIfs(args, evaluator);
            case "SUMPRODUCT": return SumProduct(args, evaluator);
            case "VLOOKUP": return VLookup(args, evaluator);
            case "HLOOKUP": return HLookup(args, evaluator);
            case "INDEX": return Index(args, evaluator);
            case "MATCH": return Match(args, evaluator);
            case "CHOOSE": return Choose(args, evaluator);
        }

        // ── Eager argument evaluation ────────────────────────────────────────────
        var values = args.Select(evaluator.Evaluate).ToList();
        var firstError = values.FirstOrDefault(static v => v.IsError);
        return firstError.IsError && !IsInfoFunction(upper)
            ? firstError
            : upper switch
            {
                // Math & aggregation.
                "SUM" => Number(Nums(values).Sum()),
                "PRODUCT" => Number(Nums(values).Aggregate(1.0, static (a, b) => a * b)),
                "AVERAGE" => Average(values),
                "AVERAGEA" => AverageA(values),
                "COUNT" => Number(Flatten(values).Count(IsNumber)),
                "COUNTA" => Number(Flatten(values).Count(static v => v.Kind != FormulaValueKind.Blank)),
                "COUNTBLANK" => Number(Flatten(values).Count(static v => v.Kind == FormulaValueKind.Blank)),
                "MIN" => MinMax(values, true),
                "MINA" => MinMaxA(values, true),
                "MAX" => MinMax(values, false),
                "MAXA" => MinMaxA(values, false),
                "ABS" => Unary(values, Math.Abs),
                "SQRT" => Guarded(values, Math.Sqrt, static x => x < 0, CellError.Number),
                "POWER" => Binary(values, Math.Pow),
                "EXP" => Unary(values, Math.Exp),
                "LN" => Guarded(values, Math.Log, static x => x <= 0, CellError.Number),
                "LOG10" => Guarded(values, Math.Log10, static x => x <= 0, CellError.Number),
                "LOG" => Log(values),
                "INT" => Unary(values, Math.Floor),
                "TRUNC" => Truncate(values),
                "SIGN" => Unary(values, static x => Math.Sign(x)),
                "MOD" => Binary(values, Modulo, CellError.DivisionByZero),
                "QUOTIENT" => Binary(values, static (a, b) => b == 0 ? double.NaN : Math.Truncate(a / b), CellError.DivisionByZero),
                "ROUND" => Round(values),
                "ROUNDUP" => RoundDir(values, true),
                "ROUNDDOWN" => RoundDir(values, false),
                "MROUND" => Binary(values, static (x, m) => m == 0 ? 0 : Math.Round(x / m, MidpointRounding.AwayFromZero) * m),
                "CEILING" => Binary(values, static (x, s) => s == 0 ? 0 : Math.Ceiling(x / s) * s),
                "FLOOR" => Binary(values, static (x, s) => s == 0 ? 0 : Math.Floor(x / s) * s),
                "EVEN" => Unary(values, static x => Math.Sign(x) * Math.Ceiling(Math.Abs(x) / 2) * 2),
                "ODD" => Unary(values, OddOf),
                "GCD" => Number(GcdOf(Nums(values))),
                "LCM" => Number(LcmOf(Nums(values))),
                "FACT" => Guarded(values, Factorial, static x => x < 0, CellError.Number),
                "SQRTPI" => Unary(values, static x => Math.Sqrt(x * Math.PI)),
                "PI" => Number(Math.PI),
                "RAND" => Number(SharedRandom.NextDouble()),
                "RANDBETWEEN" => RandBetween(values),
                // Trig.
                "SIN" => Unary(values, Math.Sin),
                "COS" => Unary(values, Math.Cos),
                "TAN" => Unary(values, Math.Tan),
                "ASIN" => Unary(values, Math.Asin),
                "ACOS" => Unary(values, Math.Acos),
                "ATAN" => Unary(values, Math.Atan),
                "ATAN2" => Binary(values, static (x, y) => Math.Atan2(y, x)),
                "SINH" => Unary(values, Math.Sinh),
                "COSH" => Unary(values, Math.Cosh),
                "TANH" => Unary(values, Math.Tanh),
                "DEGREES" => Unary(values, static x => x * 180.0 / Math.PI),
                "RADIANS" => Unary(values, static x => x * Math.PI / 180.0),
                // Statistics.
                "MEDIAN" => Median(values),
                "MODE" or "MODE.SNGL" => Mode(values),
                "STDEV" or "STDEV.S" => StdDev(values, true),
                "STDEVP" or "STDEV.P" => StdDev(values, false),
                "VAR" or "VAR.S" => Variance(values, true),
                "VARP" or "VAR.P" => Variance(values, false),
                "LARGE" => LargeSmall(values, true),
                "SMALL" => LargeSmall(values, false),
                "RANK" or "RANK.EQ" => Rank(values),
                "PERCENTILE" or "PERCENTILE.INC" => Percentile(values),
                // Text.
                "LEN" => Number(Text(values, 0).Length),
                "LEFT" => TextNumber(values, static (s, n) => s[..Math.Min(Math.Max((int)n, 0), s.Length)], 1),
                "RIGHT" => TextNumber(values, static (s, n) => s[^Math.Min(Math.Max((int)n, 0), s.Length)..], 1),
                "MID" => Mid(values),
                "UPPER" => Str(Text(values, 0).ToUpperInvariant()),
                "LOWER" => Str(Text(values, 0).ToLowerInvariant()),
                "PROPER" => Str(CultureInfo.InvariantCulture.TextInfo.ToTitleCase(Text(values, 0).ToLowerInvariant())),
                "TRIM" => Str(TrimInner(Text(values, 0))),
                "CLEAN" => Str(new string(Text(values, 0).Where(static c => !char.IsControl(c)).ToArray())),
                "CONCATENATE" or "CONCAT" => Str(string.Concat(Flatten(values).Select(FormulaEvaluator.ToText))),
                "TEXTJOIN" => TextJoin(values),
                "REPT" => Rept(values),
                "FIND" => Find(values, true),
                "SEARCH" => Find(values, false),
                "SUBSTITUTE" => Substitute(values),
                "REPLACE" => Replace(values),
                "EXACT" => FormulaValue.FromBoolean(Text(values, 0) == Text(values, 1)),
                "CHAR" => Str(((char)(int)Num(values, 0)).ToString()),
                "CODE" => Number(Text(values, 0).Length > 0 ? Text(values, 0)[0] : 0),
                "VALUE" => Number(double.TryParse(Text(values, 0), NumberStyles.Any, CultureInfo.InvariantCulture, out var pv) ? pv : 0),
                "TEXT" => TextFn(values),
                "T" => values.Count > 0 && values[0].Kind == FormulaValueKind.Text ? values[0] : Str(string.Empty),
                "N" => Number(values.Count > 0 ? FormulaEvaluator.ToNumber(values[0]) : 0),
                // Logical (non-lazy).
                "NOT" => FormulaValue.FromBoolean(values.Count > 0 && !FormulaEvaluator.ToBoolean(values[0])),
                // Date / time.
                "DATE" => DateFn(values),
                "TODAY" => Number(Math.Floor(DateTimeSerializer.ToSerial(DateTime.Today, false))),
                "NOW" => Number(Math.Floor(DateTimeSerializer.ToSerial(DateTime.Now, false))),
                "YEAR" => DatePart(values, static d => d.Year),
                "MONTH" => DatePart(values, static d => d.Month),
                "DAY" => DatePart(values, static d => d.Day),
                "HOUR" => DatePart(values, static d => d.Hour),
                "MINUTE" => DatePart(values, static d => d.Minute),
                "SECOND" => DatePart(values, static d => d.Second),
                "WEEKDAY" => Weekday(values),
                "DATEVALUE" => DateValue(values),
                "EOMONTH" => EoMonth(values),
                // Information.
                "ISERROR" => FormulaValue.FromBoolean(values.Count > 0 && values[0].IsError),
                "ISERR" => FormulaValue.FromBoolean(values.Count > 0 && values[0].IsError && values[0].Error != CellError.NotAvailable),
                "ISNA" => FormulaValue.FromBoolean(values.Count > 0 && values[0].IsError && values[0].Error == CellError.NotAvailable),
                "ISNUMBER" => FormulaValue.FromBoolean(values.Count > 0 && values[0].Kind == FormulaValueKind.Number),
                "ISTEXT" => FormulaValue.FromBoolean(values.Count > 0 && values[0].Kind == FormulaValueKind.Text),
                "ISNONTEXT" => FormulaValue.FromBoolean(values.Count > 0 && values[0].Kind != FormulaValueKind.Text),
                "ISBLANK" => FormulaValue.FromBoolean(values.Count > 0 && values[0].Kind == FormulaValueKind.Blank),
                "ISLOGICAL" => FormulaValue.FromBoolean(values.Count > 0 && values[0].Kind == FormulaValueKind.Boolean),
                "ISEVEN" => FormulaValue.FromBoolean((long)Num(values, 0) % 2 == 0),
                "ISODD" => FormulaValue.FromBoolean((long)Num(values, 0) % 2 != 0),
                "NA" => FormulaValue.FromError(CellError.NotAvailable),
                // Additional math.
                "SUMSQ" => Number(Nums(values).Sum(static x => x * x)),
                "CBRT" => Unary(values, Math.Cbrt),
                "CEILING.MATH" => CeilingFloorMath(values, true),
                "FLOOR.MATH" => CeilingFloorMath(values, false),
                "ROMAN" => Str(ToRoman((int)Num(values, 0))),
                "ARABIC" => Number(FromRoman(Text(values, 0))),
                "BASE" => Str(ToBase((long)Num(values, 0), (int)Num(values, 1), values.Count > 2 ? (int)Num(values, 2) : 0)),
                "DECIMAL" => Number(FromBase(Text(values, 0), (int)Num(values, 1))),
                "COMBIN" => Number(Combinations(Num(values, 0), Num(values, 1))),
                "PERMUT" => Number(Permutations(Num(values, 0), Num(values, 1))),
                "FACTDOUBLE" => Number(DoubleFactorial((int)Num(values, 0))),
                "SEC" => Unary(values, static x => 1.0 / Math.Cos(x)),
                "CSC" => Unary(values, static x => 1.0 / Math.Sin(x)),
                "COT" => Unary(values, static x => 1.0 / Math.Tan(x)),
                "ASINH" => Unary(values, Math.Asinh),
                "ACOSH" => Unary(values, Math.Acosh),
                "ATANH" => Unary(values, Math.Atanh),
                // Conditional / aggregate extras.
                "MAXIFS" => MinMaxIfs(args, evaluator, true),
                "MINIFS" => MinMaxIfs(args, evaluator, false),
                // Statistics extras.
                "GEOMEAN" => GeoMean(values),
                "HARMEAN" => HarMean(values),
                "AVEDEV" => AveDev(values),
                "DEVSQ" => DevSq(values),
                "MODE.MULT" => Mode(values),
                "PERCENTRANK" or "PERCENTRANK.INC" => PercentRank(values),
                "QUARTILE" or "QUARTILE.INC" => Quartile(values),
                "SKEW" => Skew(values),
                "TRIMMEAN" => TrimMean(values),
                "RANK.AVG" => Rank(values),
                // Text extras.
                "TEXTBEFORE" => TextBeforeAfter(values, true),
                "TEXTAFTER" => TextBeforeAfter(values, false),
                "UNICHAR" => Str(char.ConvertFromUtf32((int)Num(values, 0))),
                "UNICODE" => Number(Text(values, 0).Length > 0 ? char.ConvertToUtf32(Text(values, 0), 0) : 0),
                "DOLLAR" => Dollar(values),
                "FIXED" => Fixed(values),
                "NUMBERVALUE" => Number(double.TryParse(Text(values, 0), NumberStyles.Any, CultureInfo.InvariantCulture, out var nvv) ? nvv : 0),
                // Date/time extras.
                "TIME" => Number(((Num(values, 0) * 3600) + (Num(values, 1) * 60) + Num(values, 2)) / 86400.0),
                "TIMEVALUE" => TimeValue(values),
                "DAYS" => Number(Math.Floor(Num(values, 0)) - Math.Floor(Num(values, 1))),
                "DAYS360" => Days360(values),
                "EDATE" => EDate(values),
                "WEEKNUM" => WeekNum(values),
                "ISOWEEKNUM" => IsoWeekNum(values),
                "YEARFRAC" => YearFrac(values),
                "DATEDIF" => DateDif(values),
                // Financial.
                "PMT" => Number(Pmt(Num(values, 0), Num(values, 1), Num(values, 2), values.Count > 3 ? Num(values, 3) : 0, values.Count > 4 ? (int)Num(values, 4) : 0)),
                "FV" => Number(Fv(Num(values, 0), Num(values, 1), Num(values, 2), values.Count > 3 ? Num(values, 3) : 0, values.Count > 4 ? (int)Num(values, 4) : 0)),
                "PV" => Number(Pv(Num(values, 0), Num(values, 1), Num(values, 2), values.Count > 3 ? Num(values, 3) : 0, values.Count > 4 ? (int)Num(values, 4) : 0)),
                "NPER" => Number(NPer(Num(values, 0), Num(values, 1), Num(values, 2), values.Count > 3 ? Num(values, 3) : 0)),
                "NPV" => Npv(values),
                "SLN" => Number((Num(values, 0) - Num(values, 1)) / Num(values, 2)),
                "EFFECT" => Number(Math.Pow(1 + (Num(values, 0) / Num(values, 1)), Num(values, 1)) - 1),
                // Information extras.
                "ISREF" => FormulaValue.FromBoolean(false),
                "ISFORMULA" => FormulaValue.FromBoolean(false),
                "TYPE" => TypeOf(values),
                "ERROR.TYPE" => ErrorType(values),
                _ => FormulaValue.FromError(CellError.Name)
            };
    }

    private static bool IsInfoFunction(string upper) =>
        upper is "ISERROR" or "ISERR" or "ISNA" or "ISBLANK" or "ISNUMBER" or "ISTEXT" or "ISNONTEXT"
            or "ISLOGICAL" or "NA" or "IFERROR" or "IFNA" or "ERROR.TYPE" or "TYPE" or "ISREF" or "ISFORMULA";
}
