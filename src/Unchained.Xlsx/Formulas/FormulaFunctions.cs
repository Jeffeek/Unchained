using Unchained.Xlsx.Formatting;
using Unchained.Xlsx.Models.Cell;
using System.Globalization;

namespace Unchained.Xlsx.Formulas;

/// <summary>
///     The built-in formula function library — a broad, practical subset of the Excel function set
///     spanning math, statistics, text, logical, lookup, date/time, and information categories.
///     Functions needing lazy argument evaluation (IF, AND, conditional aggregates, lookups) are
///     dispatched first; the remainder are evaluated eagerly. Volatile functions (RAND, TODAY, NOW)
///     are supported but not re-evaluated on a schedule. This is not the full 450+ Excel library, but
///     covers the vast majority of real-world workbooks.
/// </summary>
internal static class FormulaFunctions
{
    public static FormulaValue Invoke(string name, IReadOnlyList<FormulaNode> args, FormulaEvaluator evaluator)
    {
        var upper = name.ToUpperInvariant();

        // ── Functions with lazy / structural argument handling ──────────────────
        switch (upper)
        {
            case "IF": return If(args, evaluator);
            case "IFS": return Ifs(args, evaluator);
            case "IFERROR": return IfError(args, evaluator, naOnly: false);
            case "IFNA": return IfError(args, evaluator, naOnly: true);
            case "SWITCH": return Switch(args, evaluator);
            case "AND": return BoolAggregate(args, evaluator, BoolMode.And);
            case "OR": return BoolAggregate(args, evaluator, BoolMode.Or);
            case "XOR": return BoolAggregate(args, evaluator, BoolMode.Xor);
            case "TRUE": return FormulaValue.FromBoolean(true);
            case "FALSE": return FormulaValue.FromBoolean(false);
            case "SUMIF": return SumIf(args, evaluator);
            case "AVERAGEIF": return AverageIf(args, evaluator);
            case "COUNTIF": return CountIf(args, evaluator);
            case "SUMIFS": return ConditionalSumIfs(args, evaluator, average: false);
            case "AVERAGEIFS": return ConditionalSumIfs(args, evaluator, average: true);
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
                "MIN" => MinMax(values, min: true),
                "MINA" => MinMaxA(values, min: true),
                "MAX" => MinMax(values, min: false),
                "MAXA" => MinMaxA(values, min: false),
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
                "ROUNDUP" => RoundDir(values, up: true),
                "ROUNDDOWN" => RoundDir(values, up: false),
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
                "STDEV" or "STDEV.S" => StdDev(values, sample: true),
                "STDEVP" or "STDEV.P" => StdDev(values, sample: false),
                "VAR" or "VAR.S" => Variance(values, sample: true),
                "VARP" or "VAR.P" => Variance(values, sample: false),
                "LARGE" => LargeSmall(values, largest: true),
                "SMALL" => LargeSmall(values, largest: false),
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
                "FIND" => Find(values, caseSensitive: true),
                "SEARCH" => Find(values, caseSensitive: false),
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
                "NOW" => Number(DateTimeSerializer.ToSerial(DateTime.Now, false)),
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

                // ── Additional math ──────────────────────────────────────────────
                "SUMSQ" => Number(Nums(values).Sum(static x => x * x)),
                "CBRT" => Unary(values, Math.Cbrt),
                "CEILING.MATH" => CeilingFloorMath(values, ceiling: true),
                "FLOOR.MATH" => CeilingFloorMath(values, ceiling: false),
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

                // ── Conditional / aggregate extras ───────────────────────────────
                "MAXIFS" => MinMaxIfs(args, evaluator, max: true),
                "MINIFS" => MinMaxIfs(args, evaluator, max: false),

                // ── Statistics extras ────────────────────────────────────────────
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

                // ── Text extras ──────────────────────────────────────────────────
                "TEXTBEFORE" => TextBeforeAfter(values, before: true),
                "TEXTAFTER" => TextBeforeAfter(values, before: false),
                "UNICHAR" => Str(char.ConvertFromUtf32((int)Num(values, 0))),
                "UNICODE" => Number(Text(values, 0).Length > 0 ? char.ConvertToUtf32(Text(values, 0), 0) : 0),
                "DOLLAR" => Dollar(values),
                "FIXED" => Fixed(values),
                "NUMBERVALUE" => Number(double.TryParse(Text(values, 0), NumberStyles.Any, CultureInfo.InvariantCulture, out var nvv) ? nvv : 0),

                // ── Date/time extras ─────────────────────────────────────────────
                "TIME" => Number(((Num(values, 0) * 3600) + (Num(values, 1) * 60) + Num(values, 2)) / 86400.0),
                "TIMEVALUE" => TimeValue(values),
                "DAYS" => Number(Math.Floor(Num(values, 0)) - Math.Floor(Num(values, 1))),
                "DAYS360" => Days360(values),
                "EDATE" => EDate(values),
                "WEEKNUM" => WeekNum(values),
                "ISOWEEKNUM" => IsoWeekNum(values),
                "YEARFRAC" => Number(Math.Abs(Num(values, 1) - Num(values, 0)) / 365.0),
                "DATEDIF" => DateDif(values),

                // ── Financial ────────────────────────────────────────────────────
                "PMT" => Number(Pmt(Num(values, 0), Num(values, 1), Num(values, 2), values.Count > 3 ? Num(values, 3) : 0, values.Count > 4 ? (int)Num(values, 4) : 0)),
                "FV" => Number(Fv(Num(values, 0), Num(values, 1), Num(values, 2), values.Count > 3 ? Num(values, 3) : 0, values.Count > 4 ? (int)Num(values, 4) : 0)),
                "PV" => Number(Pv(Num(values, 0), Num(values, 1), Num(values, 2), values.Count > 3 ? Num(values, 3) : 0, values.Count > 4 ? (int)Num(values, 4) : 0)),
                "NPER" => Number(NPer(Num(values, 0), Num(values, 1), Num(values, 2), values.Count > 3 ? Num(values, 3) : 0)),
                "NPV" => Npv(values),
                "SLN" => Number((Num(values, 0) - Num(values, 1)) / Num(values, 2)),
                "EFFECT" => Number(Math.Pow(1 + (Num(values, 0) / Num(values, 1)), Num(values, 1)) - 1),

                // ── Information extras ───────────────────────────────────────────
                "ISREF" => FormulaValue.FromBoolean(false),
                "ISFORMULA" => FormulaValue.FromBoolean(false),
                "TYPE" => Number(TypeOf(values)),
                "ERROR.TYPE" => ErrorType(values),

                _ => FormulaValue.FromError(CellError.Name)
            };
    }

    private static readonly Random SharedRandom = new();

    private static bool IsInfoFunction(string upper) =>
        upper is "ISERROR" or "ISERR" or "ISNA" or "ISBLANK" or "ISNUMBER" or "ISTEXT" or "ISNONTEXT"
            or "ISLOGICAL" or "NA" or "IFERROR" or "IFNA" or "ERROR.TYPE" or "TYPE" or "ISREF" or "ISFORMULA";

    // ── Logical ─────────────────────────────────────────────────────────────────

    private static FormulaValue If(IReadOnlyList<FormulaNode> args, FormulaEvaluator ev)
    {
        if (args.Count < 2) return FormulaValue.FromError(CellError.Value);

        var condition = ev.Evaluate(args[0]);
        return condition.IsError
            ? condition
            : FormulaEvaluator.ToBoolean(condition)
                ? ev.Evaluate(args[1])
                : args.Count >= 3
                    ? ev.Evaluate(args[2])
                    : FormulaValue.FromBoolean(false);
    }

    private static FormulaValue Ifs(IReadOnlyList<FormulaNode> args, FormulaEvaluator ev)
    {
        for (var i = 0; i + 1 < args.Count; i += 2)
        {
            var condition = ev.Evaluate(args[i]);
            if (condition.IsError) return condition;
            if (FormulaEvaluator.ToBoolean(condition))
                return ev.Evaluate(args[i + 1]);
        }

        return FormulaValue.FromError(CellError.NotAvailable);
    }

    private static FormulaValue Switch(IReadOnlyList<FormulaNode> args, FormulaEvaluator ev)
    {
        if (args.Count < 2) return FormulaValue.FromError(CellError.Value);

        var subject = ev.Evaluate(args[0]);
        var i = 1;
        for (; i + 1 < args.Count; i += 2)
        {
            var candidate = ev.Evaluate(args[i]);
            if (ScalarEquals(subject, candidate))
                return ev.Evaluate(args[i + 1]);
        }

        // A trailing odd argument is the default.
        return i < args.Count ? ev.Evaluate(args[i]) : FormulaValue.FromError(CellError.NotAvailable);
    }

    private static FormulaValue IfError(IReadOnlyList<FormulaNode> args, FormulaEvaluator ev, bool naOnly)
    {
        if (args.Count < 2) return FormulaValue.FromError(CellError.Value);

        var value = ev.Evaluate(args[0]);
        var caught = value.IsError && (!naOnly || value.Error == CellError.NotAvailable);
        return caught ? ev.Evaluate(args[1]) : value;
    }

    private enum BoolMode { And, Or, Xor }

    private static FormulaValue BoolAggregate(IEnumerable<FormulaNode> args, FormulaEvaluator ev, BoolMode mode)
    {
        var any = false;
        var trueCount = 0;
        foreach (var v in args.SelectMany(arg => ev.Evaluate(arg).Flatten()))
        {
            if (v.IsError) return v;

            if (v.Kind == FormulaValueKind.Blank) continue;

            any = true;
            var truthy = FormulaEvaluator.ToBoolean(v);
            if (truthy) trueCount++;
            switch (mode)
            {
                case BoolMode.And when !truthy:
                    return FormulaValue.FromBoolean(false);
                case BoolMode.Or when truthy:
                    return FormulaValue.FromBoolean(true);
                case BoolMode.And:
                case BoolMode.Or:
                case BoolMode.Xor:
                break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
            }
        }

        return mode switch
        {
            BoolMode.And => FormulaValue.FromBoolean(any),
            BoolMode.Or => FormulaValue.FromBoolean(false),
            _ => FormulaValue.FromBoolean(trueCount % 2 == 1)
        };
    }

    // ── Conditional aggregates ──────────────────────────────────────────────────

    private static FormulaValue SumIf(IReadOnlyList<FormulaNode> args, FormulaEvaluator ev)
    {
        if (args.Count < 2) return FormulaValue.FromError(CellError.Value);

        var range = ev.Evaluate(args[0]).Flatten().ToList();
        var criterion = ev.Evaluate(args[1]);
        var sumRange = args.Count >= 3 ? ev.Evaluate(args[2]).Flatten().ToList() : range;

        var total = 0.0;
        for (var i = 0; i < range.Count; i++)
        {
            if (MatchesCriterion(range[i], criterion) && i < sumRange.Count && IsNumber(sumRange[i]))
                total += FormulaEvaluator.ToNumber(sumRange[i]);
        }

        return Number(total);
    }

    private static FormulaValue AverageIf(IReadOnlyList<FormulaNode> args, FormulaEvaluator ev)
    {
        if (args.Count < 2) return FormulaValue.FromError(CellError.Value);

        var range = ev.Evaluate(args[0]).Flatten().ToList();
        var criterion = ev.Evaluate(args[1]);
        var avgRange = args.Count >= 3 ? ev.Evaluate(args[2]).Flatten().ToList() : range;

        double total = 0;
        var count = 0;
        for (var i = 0; i < range.Count; i++)
        {
            if (!MatchesCriterion(range[i], criterion) || i >= avgRange.Count || !IsNumber(avgRange[i])) continue;

            total += FormulaEvaluator.ToNumber(avgRange[i]);
            count++;
        }

        return count == 0 ? FormulaValue.FromError(CellError.DivisionByZero) : Number(total / count);
    }

    private static FormulaValue CountIf(IReadOnlyList<FormulaNode> args, FormulaEvaluator ev)
    {
        if (args.Count < 2) return FormulaValue.FromError(CellError.Value);

        var range = ev.Evaluate(args[0]).Flatten().ToList();
        var criterion = ev.Evaluate(args[1]);
        return Number(range.Count(v => MatchesCriterion(v, criterion)));
    }

    private static FormulaValue ConditionalSumIfs(IReadOnlyList<FormulaNode> args, FormulaEvaluator ev, bool average)
    {
        // SUMIFS(sumRange, critRange1, crit1, ...); AVERAGEIFS shares the layout.
        if (args.Count < 3) return FormulaValue.FromError(CellError.Value);

        var aggregate = ev.Evaluate(args[0]).Flatten().ToList();
        var pairs = CriteriaPairs(args, ev, startIndex: 1);

        double total = 0;
        var count = 0;
        for (var i = 0; i < aggregate.Count; i++)
        {
            if (!RowMatchesAll(pairs, i)) continue;
            if (!IsNumber(aggregate[i])) continue;

            total += FormulaEvaluator.ToNumber(aggregate[i]);
            count++;
        }

        return !average
            ? Number(total)
            : count == 0
                ? FormulaValue.FromError(CellError.DivisionByZero)
                : Number(total / count);
    }

    private static FormulaValue CountIfs(IReadOnlyList<FormulaNode> args, FormulaEvaluator ev)
    {
        if (args.Count < 2) return FormulaValue.FromError(CellError.Value);

        var pairs = CriteriaPairs(args, ev, startIndex: 0);
        if (pairs.Count == 0) return Number(0);

        var length = pairs.Min(static p => p.Range.Count);
        var count = 0;
        for (var i = 0; i < length; i++)
        {
            if (RowMatchesAll(pairs, i))
                count++;
        }

        return Number(count);
    }

    private static List<(List<FormulaValue> Range, FormulaValue Criterion)> CriteriaPairs(IReadOnlyList<FormulaNode> args, FormulaEvaluator ev, int startIndex)
    {
        var pairs = new List<(List<FormulaValue>, FormulaValue)>();
        for (var i = startIndex; i + 1 < args.Count; i += 2)
            pairs.Add((ev.Evaluate(args[i]).Flatten().ToList(), ev.Evaluate(args[i + 1])));
        return pairs;
    }

    private static bool RowMatchesAll(IEnumerable<(List<FormulaValue> Range, FormulaValue Criterion)> pairs, int row) =>
        pairs.All(p => row < p.Range.Count && MatchesCriterion(p.Range[row], p.Criterion));

    private static FormulaValue SumProduct(IReadOnlyCollection<FormulaNode> args, FormulaEvaluator ev)
    {
        if (args.Count == 0) return Number(0);

        var arrays = args.Select(a => ev.Evaluate(a).Flatten().ToList()).ToList();
        var length = arrays.Min(static a => a.Count);
        double total = 0;
        for (var i = 0; i < length; i++)
        {
            var product = arrays.Aggregate(1.0, (current, arr) => current * FormulaEvaluator.ToNumber(arr[i]));
            total += product;
        }

        return Number(total);
    }

    // ── Lookup ──────────────────────────────────────────────────────────────────

    private static FormulaValue VLookup(IReadOnlyList<FormulaNode> args, FormulaEvaluator ev)
    {
        if (args.Count < 3) return FormulaValue.FromError(CellError.Value);

        var key = ev.Evaluate(args[0]);
        var table = ev.Evaluate(args[1]);
        var colIndex = (int)FormulaEvaluator.ToNumber(ev.Evaluate(args[2]));
        var approximate = args.Count < 4 || FormulaEvaluator.ToBoolean(ev.Evaluate(args[3]));
        if (table.Kind != FormulaValueKind.Array || table.Columns == 0)
            return FormulaValue.FromError(CellError.NotAvailable);

        var matchRow = FindRow(table, key, approximate, column: 0);
        return matchRow < 0
            ? FormulaValue.FromError(CellError.NotAvailable)
            : colIndex < 1 || colIndex > table.Columns
                ? FormulaValue.FromError(CellError.Reference)
                : table.At(matchRow, colIndex - 1);
    }

    private static FormulaValue HLookup(IReadOnlyList<FormulaNode> args, FormulaEvaluator ev)
    {
        if (args.Count < 3) return FormulaValue.FromError(CellError.Value);

        var key = ev.Evaluate(args[0]);
        var table = ev.Evaluate(args[1]);
        var rowIndex = (int)FormulaEvaluator.ToNumber(ev.Evaluate(args[2]));
        var approximate = args.Count < 4 || FormulaEvaluator.ToBoolean(ev.Evaluate(args[3]));
        if (table.Kind != FormulaValueKind.Array || table.Columns == 0)
            return FormulaValue.FromError(CellError.NotAvailable);

        var matchCol = FindColumn(table, key, approximate);
        return matchCol < 0
            ? FormulaValue.FromError(CellError.NotAvailable)
            : rowIndex < 1 || rowIndex > table.Rows
                ? FormulaValue.FromError(CellError.Reference)
                : table.At(rowIndex - 1, matchCol);
    }

    private static FormulaValue Index(IReadOnlyList<FormulaNode> args, FormulaEvaluator ev)
    {
        if (args.Count < 2) return FormulaValue.FromError(CellError.Value);

        var array = ev.Evaluate(args[0]);
        var rowNum = (int)FormulaEvaluator.ToNumber(ev.Evaluate(args[1]));
        var colNum = args.Count >= 3 ? (int)FormulaEvaluator.ToNumber(ev.Evaluate(args[2])) : 0;

        if (array.Kind != FormulaValueKind.Array)
            return rowNum <= 1 ? array : FormulaValue.FromError(CellError.Reference);

        // Single-row or single-column arrays accept one index.
        if (array.Rows == 1 && colNum == 0)
        {
            colNum = rowNum;
            rowNum = 1;
        }

        if (array.Columns == 1 && colNum == 0) colNum = 1;
        if (colNum == 0) colNum = 1;

        return rowNum < 1 || rowNum > array.Rows || colNum < 1 || colNum > array.Columns
            ? FormulaValue.FromError(CellError.Reference)
            : array.At(rowNum - 1, colNum - 1);
    }

    private static FormulaValue Match(IReadOnlyList<FormulaNode> args, FormulaEvaluator ev)
    {
        if (args.Count < 2) return FormulaValue.FromError(CellError.Value);

        var key = ev.Evaluate(args[0]);
        var array = ev.Evaluate(args[1]).Flatten().ToList();
        var matchType = args.Count >= 3 ? (int)FormulaEvaluator.ToNumber(ev.Evaluate(args[2])) : 1;

        switch (matchType)
        {
            case 0:
                for (var i = 0; i < array.Count; i++)
                {
                    if (ScalarEquals(array[i], key))
                        return Number(i + 1);
                }

                return FormulaValue.FromError(CellError.NotAvailable);
            case > 0:
            {
                var best = -1;
                for (var i = 0; i < array.Count; i++)
                {
                    if (Compare(array[i], key) <= 0)
                        best = i;
                }

                return best < 0 ? FormulaValue.FromError(CellError.NotAvailable) : Number(best + 1);
            }
            default:
            {
                var best = -1;
                for (var i = 0; i < array.Count; i++)
                {
                    if (Compare(array[i], key) >= 0)
                        best = i;
                }

                return best < 0 ? FormulaValue.FromError(CellError.NotAvailable) : Number(best + 1);
            }
        }
    }

    private static FormulaValue Choose(IReadOnlyList<FormulaNode> args, FormulaEvaluator ev)
    {
        if (args.Count < 2) return FormulaValue.FromError(CellError.Value);

        var index = (int)FormulaEvaluator.ToNumber(ev.Evaluate(args[0]));
        return index < 1 || index >= args.Count ? FormulaValue.FromError(CellError.Value) : ev.Evaluate(args[index]);
    }

    // ReSharper disable once BadListLineBreaks
    private static int FindRow(FormulaValue table, FormulaValue key, bool approximate, int column)
    {
        var match = -1;
        for (var r = 0; r < table.Rows; r++)
        {
            var cell = table.At(r, column);
            if (!approximate)
            {
                if (ScalarEquals(cell, key)) return r;
            }
            else if (Compare(cell, key) <= 0)
                match = r;
            else
                break;
        }

        return match;
    }

    private static int FindColumn(FormulaValue table, FormulaValue key, bool approximate)
    {
        var match = -1;
        for (var c = 0; c < table.Columns; c++)
        {
            var cell = table.At(0, c);
            if (!approximate)
            {
                if (ScalarEquals(cell, key)) return c;
            }
            else if (Compare(cell, key) <= 0)
                match = c;
            else
                break;
        }

        return match;
    }

    // ── Criterion matching ──────────────────────────────────────────────────────

    private static bool MatchesCriterion(FormulaValue value, FormulaValue criterion)
    {
        if (criterion.Kind != FormulaValueKind.Text)
            return FormulaEvaluator.ToNumber(value).Equals(FormulaEvaluator.ToNumber(criterion));

        var text = criterion.Text ?? string.Empty;
        foreach (var op in new[] { "<>", ">=", "<=", ">", "<", "=" })
        {
            if (!text.StartsWith(op, StringComparison.Ordinal)) continue;

            var rhs = text[op.Length..];
            return double.TryParse(rhs, NumberStyles.Any, CultureInfo.InvariantCulture, out var num) && IsNumber(value)
                ? CompareNum(FormulaEvaluator.ToNumber(value), num, op)
                : CompareText(FormulaEvaluator.ToText(value), rhs, op);
        }

        return string.Equals(FormulaEvaluator.ToText(value), text, StringComparison.OrdinalIgnoreCase);
    }

    private static bool CompareNum(double a, double b, string op) => op switch
    {
        ">" => a > b, "<" => a < b, ">=" => a >= b, "<=" => a <= b, "<>" => !a.Equals(b), _ => a.Equals(b)
    };

    private static bool CompareText(string a, string b, string op)
    {
        var cmp = string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
        return op switch { "<>" => cmp != 0, ">" => cmp > 0, "<" => cmp < 0, ">=" => cmp >= 0, "<=" => cmp <= 0, _ => cmp == 0 };
    }

    private static bool ScalarEquals(FormulaValue a, FormulaValue b) =>
        IsNumber(a) && IsNumber(b)
            ? FormulaEvaluator.ToNumber(a).Equals(FormulaEvaluator.ToNumber(b))
            : string.Equals(FormulaEvaluator.ToText(a), FormulaEvaluator.ToText(b), StringComparison.OrdinalIgnoreCase);

    private static int Compare(FormulaValue a, FormulaValue b) =>
        IsNumber(a) && IsNumber(b)
            ? FormulaEvaluator.ToNumber(a).CompareTo(FormulaEvaluator.ToNumber(b))
            : string.Compare(FormulaEvaluator.ToText(a), FormulaEvaluator.ToText(b), StringComparison.OrdinalIgnoreCase);

    // ── Math helpers ────────────────────────────────────────────────────────────

    private static double Modulo(double a, double b) => b == 0 ? double.NaN : a - (b * Math.Floor(a / b));

    private static FormulaValue Log(IReadOnlyList<FormulaValue> values)
    {
        var x = Num(values, 0);
        if (x <= 0) return FormulaValue.FromError(CellError.Number);

        var baseN = values.Count > 1 ? Num(values, 1) : 10;
        return Number(Math.Log(x, baseN));
    }

    private static FormulaValue Truncate(IReadOnlyList<FormulaValue> values)
    {
        var x = Num(values, 0);
        var digits = values.Count > 1 ? (int)Num(values, 1) : 0;
        var factor = Math.Pow(10, digits);
        return Number(Math.Truncate(x * factor) / factor);
    }

    private static double OddOf(double x)
    {
        var rounded = Math.Sign(x) * Math.Ceiling(Math.Abs(x));
        if (rounded % 2 == 0) rounded += Math.Sign(x) == 0 ? 1 : Math.Sign(x);
        return rounded == 0 ? 1 : rounded;
    }

    private static double Factorial(double x)
    {
        var n = (int)Math.Floor(x);
        double result = 1;
        for (var i = 2; i <= n; i++) result *= i;
        return result;
    }

    private static double GcdOf(IEnumerable<double> values) =>
        values.Select(static v => (long)Math.Abs(v)).Aggregate(0L, Gcd);

    private static double LcmOf(IEnumerable<double> values) =>
        values.Select(static v => (long)Math.Abs(v)).Aggregate(1L, static (a, b) => b == 0 ? 0 : Math.Abs(a / Gcd(a, b) * b));

    private static long Gcd(long a, long b)
    {
        while (b != 0)
            (a, b) = (b, a % b);
        return Math.Abs(a);
    }

    private static FormulaValue RandBetween(IReadOnlyList<FormulaValue> values)
    {
        var lo = (int)Num(values, 0);
        var hi = (int)Num(values, 1);
        if (hi < lo) (lo, hi) = (hi, lo);
        return Number(SharedRandom.Next(lo, hi + 1));
    }

    // ── Statistics helpers ──────────────────────────────────────────────────────

    private static FormulaValue Median(IEnumerable<FormulaValue> values)
    {
        var nums = Nums(values).OrderBy(static x => x).ToList();
        if (nums.Count == 0) return FormulaValue.FromError(CellError.Number);

        var mid = nums.Count / 2;
        return Number(nums.Count % 2 == 1 ? nums[mid] : (nums[mid - 1] + nums[mid]) / 2);
    }

    private static FormulaValue Mode(IEnumerable<FormulaValue> values)
    {
        var groups = Nums(values).GroupBy(static x => x).Select(static g => (Value: g.Key, Count: g.Count())).ToList();
        if (groups.Count == 0 || groups.All(static g => g.Count == 1)) return FormulaValue.FromError(CellError.NotAvailable);

        var max = groups.Max(static g => g.Count);
        return Number(groups.First(g => g.Count == max).Value);
    }

    private static FormulaValue StdDev(IEnumerable<FormulaValue> values, bool sample)
    {
        var variance = Variance(values, sample);
        return variance.IsError ? variance : Number(Math.Sqrt(variance.Number));
    }

    private static FormulaValue Variance(IEnumerable<FormulaValue> values, bool sample)
    {
        var nums = Nums(values).ToList();
        var divisor = sample ? nums.Count - 1 : nums.Count;
        if (divisor <= 0) return FormulaValue.FromError(CellError.DivisionByZero);

        var mean = nums.Average();
        return Number(nums.Sum(x => (x - mean) * (x - mean)) / divisor);
    }

    private static FormulaValue LargeSmall(IReadOnlyList<FormulaValue> values, bool largest)
    {
        if (values.Count < 2) return FormulaValue.FromError(CellError.Value);
        // First argument(s) are the array; the last is k.
        var k = (int)FormulaEvaluator.ToNumber(values[^1]);
        var arr = values
            .Take(values.Count - 1)
            .SelectMany(static v => v.Flatten())
            .Where(IsNumber)
            .Select(FormulaEvaluator.ToNumber)
            .ToList();
        if (k < 1 || k > arr.Count)
            return FormulaValue.FromError(CellError.Number);

        arr.Sort();
        return Number(largest ? arr[^k] : arr[k - 1]);
    }

    private static FormulaValue Rank(IReadOnlyList<FormulaValue> values)
    {
        if (values.Count < 2) return FormulaValue.FromError(CellError.Value);

        var number = FormulaEvaluator.ToNumber(values[0]);
        var arr = values[1].Flatten().Where(IsNumber).Select(FormulaEvaluator.ToNumber).ToList();
        var ascending = values.Count >= 3 && FormulaEvaluator.ToNumber(values[2]) != 0;
        var sorted = ascending ? arr.OrderBy(static x => x).ToList() : arr.OrderByDescending(static x => x).ToList();
        var idx = sorted.IndexOf(number);
        return idx < 0 ? FormulaValue.FromError(CellError.NotAvailable) : Number(idx + 1);
    }

    private static FormulaValue Percentile(IReadOnlyList<FormulaValue> values)
    {
        var arr = values[0].Flatten().Where(IsNumber).Select(FormulaEvaluator.ToNumber).OrderBy(static x => x).ToList();
        if (arr.Count == 0) return FormulaValue.FromError(CellError.Number);

        var p = Num(values, 1);
        var rank = p * (arr.Count - 1);
        var lo = (int)Math.Floor(rank);
        var hi = (int)Math.Ceiling(rank);
        return lo == hi ? Number(arr[lo]) : Number(arr[lo] + ((rank - lo) * (arr[hi] - arr[lo])));
    }

    // ── Text helpers ────────────────────────────────────────────────────────────

    private static FormulaValue Mid(IReadOnlyList<FormulaValue> values)
    {
        var s = Text(values, 0);
        var start = (int)Num(values, 1);
        var len = (int)Num(values, 2);
        if (start < 1 || start > s.Length || len < 0) return Str(string.Empty);

        var from = start - 1;
        return Str(s.Substring(from, Math.Min(len, s.Length - from)));
    }

    private static FormulaValue TextJoin(IReadOnlyList<FormulaValue> values)
    {
        var delimiter = Text(values, 0);
        var ignoreEmpty = values.Count > 1 && FormulaEvaluator.ToBoolean(values[1]);
        var parts = values.Skip(2)
            .SelectMany(static v => v.Flatten())
            .Where(v => !ignoreEmpty || v.Kind != FormulaValueKind.Blank)
            .Select(FormulaEvaluator.ToText);
        return Str(string.Join(delimiter, parts));
    }

    private static FormulaValue Rept(IReadOnlyList<FormulaValue> values)
    {
        var s = Text(values, 0);
        var count = Math.Max(0, (int)Num(values, 1));
        return Str(string.Concat(Enumerable.Repeat(s, count)));
    }

    private static FormulaValue Find(IReadOnlyList<FormulaValue> values, bool caseSensitive)
    {
        var needle = Text(values, 0);
        var haystack = Text(values, 1);
        var start = values.Count > 2 ? Math.Max(1, (int)Num(values, 2)) - 1 : 0;
        if (start > haystack.Length) return FormulaValue.FromError(CellError.Value);

        var idx = haystack.IndexOf(needle, start, caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);
        return idx < 0 ? FormulaValue.FromError(CellError.Value) : Number(idx + 1);
    }

    private static FormulaValue Substitute(IReadOnlyList<FormulaValue> values)
    {
        var s = Text(values, 0);
        var oldText = Text(values, 1);
        var newText = Text(values, 2);
        if (oldText.Length == 0) return Str(s);

        if (values.Count < 4) return Str(s.Replace(oldText, newText));

        // Replace only the Nth occurrence.
        var instance = (int)Num(values, 3);
        var found = 0;
        var index = 0;
        while ((index = s.IndexOf(oldText, index, StringComparison.Ordinal)) >= 0)
        {
            found++;
            if (found == instance)
                return Str(s[..index] + newText + s[(index + oldText.Length)..]);

            index += oldText.Length;
        }

        return Str(s);
    }

    private static FormulaValue Replace(IReadOnlyList<FormulaValue> values)
    {
        var s = Text(values, 0);
        var start = (int)Num(values, 1);
        var len = (int)Num(values, 2);
        var newText = Text(values, 3);
        if (start < 1) return Str(s);

        var from = Math.Min(start - 1, s.Length);
        var remove = Math.Min(Math.Max(len, 0), s.Length - from);
        return Str(s[..from] + newText + s[(from + remove)..]);
    }

    private static string TrimInner(string s)
    {
        // Excel TRIM collapses internal runs of spaces to one and trims ends.
        var parts = s.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(' ', parts);
    }

    private static FormulaValue TextFn(IReadOnlyList<FormulaValue> values)
    {
        var x = Num(values, 0);
        var format = Text(values, 1);
        try { return Str(NumberFormatter.Format(x, format, date1904: false)); }
        catch { return Str(x.ToString("G15", CultureInfo.InvariantCulture)); }
    }

    // ── Date / time helpers ─────────────────────────────────────────────────────

    private static FormulaValue DateFn(IReadOnlyList<FormulaValue> values)
    {
        try
        {
            var date = new DateTime((int)Num(values, 0), 1, 1)
                .AddMonths((int)Num(values, 1) - 1)
                .AddDays((int)Num(values, 2) - 1);
            return Number(DateTimeSerializer.ToSerial(date, false));
        }
        catch { return FormulaValue.FromError(CellError.Number); }
    }

    private static FormulaValue DatePart(IReadOnlyList<FormulaValue> values, Func<DateTime, int> selector)
    {
        var date = DateTimeSerializer.ToDateTime(Num(values, 0), false);
        return date is null ? FormulaValue.FromError(CellError.Number) : Number(selector(date.Value));
    }

    private static FormulaValue Weekday(IReadOnlyList<FormulaValue> values)
    {
        var date = DateTimeSerializer.ToDateTime(Num(values, 0), false);
        if (date is null) return FormulaValue.FromError(CellError.Number);

        var type = values.Count > 1 ? (int)Num(values, 1) : 1;
        var dow = (int)date.Value.DayOfWeek; // Sunday = 0
        return type switch
        {
            2 => Number(dow == 0 ? 7 : dow),       // Monday = 1
            3 => Number((dow + 6) % 7),            // Monday = 0
            _ => Number(dow + 1)                   // Sunday = 1
        };
    }

    private static FormulaValue DateValue(IReadOnlyList<FormulaValue> values) =>
        DateTime.TryParse(Text(values, 0), CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? Number(DateTimeSerializer.ToSerial(d, false))
            : FormulaValue.FromError(CellError.Value);

    private static FormulaValue EoMonth(IReadOnlyList<FormulaValue> values)
    {
        var date = DateTimeSerializer.ToDateTime(Num(values, 0), false);
        if (date is null) return FormulaValue.FromError(CellError.Number);

        var shifted = date.Value.AddMonths((int)Num(values, 1));
        var eom = new DateTime(shifted.Year, shifted.Month, DateTime.DaysInMonth(shifted.Year, shifted.Month));
        return Number(DateTimeSerializer.ToSerial(eom, false));
    }

    // ── Combinators ─────────────────────────────────────────────────────────────

    private static FormulaValue Average(IEnumerable<FormulaValue> values)
    {
        var nums = Nums(values).ToList();
        return nums.Count == 0 ? FormulaValue.FromError(CellError.DivisionByZero) : Number(nums.Average());
    }

    private static FormulaValue AverageA(IEnumerable<FormulaValue> values)
    {
        var nums = Flatten(values)
            .Where(static v => v.Kind != FormulaValueKind.Blank)
            .Select(FormulaEvaluator.ToNumber)
            .ToList();
        return nums.Count == 0 ? FormulaValue.FromError(CellError.DivisionByZero) : Number(nums.Average());
    }

    private static FormulaValue MinMax(IEnumerable<FormulaValue> values, bool min)
    {
        var nums = Nums(values).ToList();
        return nums.Count == 0 ? Number(0) : Number(min ? nums.Min() : nums.Max());
    }

    private static FormulaValue MinMaxA(IEnumerable<FormulaValue> values, bool min)
    {
        var nums = Flatten(values)
            .Where(static v => v.Kind != FormulaValueKind.Blank)
            .Select(FormulaEvaluator.ToNumber)
            .ToList();
        return nums.Count == 0 ? Number(0) : Number(min ? nums.Min() : nums.Max());
    }

    private static FormulaValue Round(IReadOnlyList<FormulaValue> values)
    {
        var x = Num(values, 0);
        var digits = (int)Num(values, 1);
        return Number(Math.Round(x, Math.Clamp(digits, 0, 15), MidpointRounding.AwayFromZero));
    }

    private static FormulaValue RoundDir(IReadOnlyList<FormulaValue> values, bool up)
    {
        var x = Num(values, 0);
        var digits = (int)Num(values, 1);
        var factor = Math.Pow(10, digits);
        var scaled = x * factor;
        var rounded = up
            ? (x >= 0 ? Math.Ceiling(scaled) : Math.Floor(scaled))
            : (x >= 0 ? Math.Floor(scaled) : Math.Ceiling(scaled));
        return Number(rounded / factor);
    }

    private static FormulaValue Unary(IReadOnlyList<FormulaValue> values, Func<double, double> fn) =>
        Number(fn(Num(values, 0)));

    private static FormulaValue Guarded(IReadOnlyList<FormulaValue> values, Func<double, double> fn, Func<double, bool> invalid, CellError error)
    {
        var x = Num(values, 0);
        return invalid(x) ? FormulaValue.FromError(error) : Number(fn(x));
    }

    private static FormulaValue Binary(IReadOnlyList<FormulaValue> values, Func<double, double, double> fn, CellError? nanError = null)
    {
        var result = fn(Num(values, 0), Num(values, 1));
        return double.IsNaN(result) && nanError is { } err ? FormulaValue.FromError(err) : Number(result);
    }

    private static FormulaValue TextNumber(IReadOnlyList<FormulaValue> values, Func<string, double, string> fn, double defaultN)
    {
        var s = Text(values, 0);
        var n = values.Count > 1 ? Num(values, 1) : defaultN;
        return Str(fn(s, n));
    }

    private static IEnumerable<FormulaValue> Flatten(IEnumerable<FormulaValue> values) =>
        values.SelectMany(static v => v.Flatten());

    private static IEnumerable<double> Nums(IEnumerable<FormulaValue> values) =>
        Flatten(values).Where(IsNumber).Select(FormulaEvaluator.ToNumber);

    private static bool IsNumber(FormulaValue v) => v.Kind is FormulaValueKind.Number or FormulaValueKind.Boolean;

    private static double Num(IReadOnlyList<FormulaValue> values, int index) =>
        index < values.Count ? FormulaEvaluator.ToNumber(values[index]) : 0;

    private static string Text(IReadOnlyList<FormulaValue> values, int index) =>
        index < values.Count ? FormulaEvaluator.ToText(values[index]) : string.Empty;

    private static FormulaValue Number(double value) => FormulaValue.FromNumber(value);
    private static FormulaValue Str(string value) => FormulaValue.FromText(value);

    // ── Extra math helpers ──────────────────────────────────────────────────────

    private static FormulaValue CeilingFloorMath(IReadOnlyList<FormulaValue> values, bool ceiling)
    {
        var x = Num(values, 0);
        var significance = values.Count > 1 ? Num(values, 1) : 1;
        if (significance == 0) return Number(0);

        var sig = Math.Abs(significance);
        var scaled = x / sig;
        var rounded = ceiling ? Math.Ceiling(scaled) : Math.Floor(scaled);
        return Number(rounded * sig);
    }

    private static double Combinations(double n, double k)
    {
        int ni = (int)n, ki = (int)k;
        if (ki < 0 || ki > ni) return 0;

        double result = 1;
        for (var i = 0; i < ki; i++) result = result * (ni - i) / (i + 1);
        return Math.Round(result);
    }

    private static double Permutations(double n, double k)
    {
        int ni = (int)n, ki = (int)k;
        if (ki < 0 || ki > ni) return 0;

        double result = 1;
        for (var i = 0; i < ki; i++) result *= ni - i;
        return result;
    }

    private static double DoubleFactorial(int n)
    {
        double result = 1;
        for (var i = n; i > 1; i -= 2) result *= i;
        return result;
    }

    private static readonly (int Value, string Symbol)[] RomanTable =
    [
        (1000, "M"), (900, "CM"), (500, "D"), (400, "CD"), (100, "C"), (90, "XC"),
        (50, "L"), (40, "XL"), (10, "X"), (9, "IX"), (5, "V"), (4, "IV"), (1, "I")
    ];

    private static string ToRoman(int value)
    {
        if (value is <= 0 or > 3999) return string.Empty;

        var sb = new System.Text.StringBuilder();
        foreach (var (v, sym) in RomanTable)
        {
            while (value >= v)
            {
                sb.Append(sym);
                value -= v;
            }
        }

        return sb.ToString();
    }

    private static double FromRoman(string roman)
    {
        var map = new Dictionary<char, int> { ['I'] = 1, ['V'] = 5, ['X'] = 10, ['L'] = 50, ['C'] = 100, ['D'] = 500, ['M'] = 1000 };
        var upper = roman.ToUpperInvariant();
        var total = 0;
        for (var i = 0; i < upper.Length; i++)
        {
            if (!map.TryGetValue(upper[i], out var cur)) return 0;

            var next = i + 1 < upper.Length && map.TryGetValue(upper[i + 1], out var n) ? n : 0;
            total += cur < next ? -cur : cur;
        }

        return total;
    }

    private static string ToBase(long value, int radix, int minLength)
    {
        if (radix is < 2 or > 36) return string.Empty;

        const string digits = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        if (value == 0) return "0".PadLeft(Math.Max(1, minLength), '0');

        var sb = new System.Text.StringBuilder();
        var v = Math.Abs(value);
        while (v > 0)
        {
            sb.Insert(0, digits[(int)(v % radix)]);
            v /= radix;
        }

        return sb.ToString().PadLeft(minLength, '0');
    }

    private static double FromBase(string text, int radix)
    {
        if (radix is < 2 or > 36) return 0;

        const string digits = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        double result = 0;
        foreach (var d in text.Trim().ToUpperInvariant().Select(static ch => digits.IndexOf(ch)))
        {
            if (d < 0 || d >= radix) return 0;

            result = (result * radix) + d;
        }

        return result;
    }

    // ── Extra conditional aggregate ─────────────────────────────────────────────

    private static FormulaValue MinMaxIfs(IReadOnlyList<FormulaNode> args, FormulaEvaluator ev, bool max)
    {
        if (args.Count < 3) return FormulaValue.FromError(CellError.Value);

        var range = ev.Evaluate(args[0]).Flatten().ToList();
        var pairs = CriteriaPairs(args, ev, startIndex: 1);
        var matches = new List<double>();
        for (var i = 0; i < range.Count; i++)
        {
            if (RowMatchesAll(pairs, i) && IsNumber(range[i]))
                matches.Add(FormulaEvaluator.ToNumber(range[i]));
        }

        return Number(matches.Count == 0 ? 0 : max ? matches.Max() : matches.Min());
    }

    // ── Extra statistics ────────────────────────────────────────────────────────

    private static FormulaValue GeoMean(IEnumerable<FormulaValue> values)
    {
        var nums = Nums(values).ToList();
        return nums.Count == 0 || nums.Any(static x => x <= 0) ? FormulaValue.FromError(CellError.Number) : Number(Math.Pow(nums.Aggregate(1.0, static (a, b) => a * b), 1.0 / nums.Count));
    }

    private static FormulaValue HarMean(IEnumerable<FormulaValue> values)
    {
        var nums = Nums(values).ToList();
        return nums.Count == 0 || nums.Any(static x => x <= 0) ? FormulaValue.FromError(CellError.Number) : Number(nums.Count / nums.Sum(static x => 1.0 / x));
    }

    private static FormulaValue AveDev(IEnumerable<FormulaValue> values)
    {
        var nums = Nums(values).ToList();
        if (nums.Count == 0) return FormulaValue.FromError(CellError.Number);

        var mean = nums.Average();
        return Number(nums.Average(x => Math.Abs(x - mean)));
    }

    private static FormulaValue DevSq(IEnumerable<FormulaValue> values)
    {
        var nums = Nums(values).ToList();
        if (nums.Count == 0) return Number(0);

        var mean = nums.Average();
        return Number(nums.Sum(x => (x - mean) * (x - mean)));
    }

    private static FormulaValue PercentRank(IReadOnlyList<FormulaValue> values)
    {
        var arr = values[0].Flatten().Where(IsNumber).Select(FormulaEvaluator.ToNumber).OrderBy(static x => x).ToList();
        if (arr.Count <= 1) return FormulaValue.FromError(CellError.Number);

        var x = Num(values, 1);
        var below = arr.Count(v => v < x);
        var rank = below / (arr.Count - 1.0);
        return Number(Math.Round(rank, 3));
    }

    private static FormulaValue Quartile(IReadOnlyList<FormulaValue> values)
    {
        var arr = values[0].Flatten().Where(IsNumber).Select(FormulaEvaluator.ToNumber).OrderBy(static x => x).ToList();
        if (arr.Count == 0) return FormulaValue.FromError(CellError.Number);

        var q = (int)Num(values, 1);
        var p = q / 4.0;
        var rank = p * (arr.Count - 1);
        var lo = (int)Math.Floor(rank);
        var hi = (int)Math.Ceiling(rank);
        return Number(lo == hi ? arr[lo] : arr[lo] + ((rank - lo) * (arr[hi] - arr[lo])));
    }

    private static FormulaValue Skew(IEnumerable<FormulaValue> values)
    {
        var nums = Nums(values).ToList();
        if (nums.Count < 3) return FormulaValue.FromError(CellError.DivisionByZero);

        var mean = nums.Average();
        var sd = Math.Sqrt(nums.Sum(x => (x - mean) * (x - mean)) / (nums.Count - 1));
        if (sd == 0) return FormulaValue.FromError(CellError.DivisionByZero);

        var n = (double)nums.Count;
        var sum = nums.Sum(x => Math.Pow((x - mean) / sd, 3));
        return Number(n / ((n - 1.0) * (n - 2.0)) * sum);
    }

    private static FormulaValue TrimMean(IReadOnlyList<FormulaValue> values)
    {
        var arr = values[0].Flatten().Where(IsNumber).Select(FormulaEvaluator.ToNumber).OrderBy(static x => x).ToList();
        if (arr.Count == 0) return FormulaValue.FromError(CellError.Number);

        var percent = Num(values, 1);
        var trim = (int)(arr.Count * percent / 2);
        var kept = arr.Skip(trim).Take(arr.Count - (2 * trim)).ToList();
        return kept.Count == 0 ? FormulaValue.FromError(CellError.Number) : Number(kept.Average());
    }

    // ── Extra text ──────────────────────────────────────────────────────────────

    private static FormulaValue TextBeforeAfter(IReadOnlyList<FormulaValue> values, bool before)
    {
        var s = Text(values, 0);
        var delimiter = Text(values, 1);
        if (delimiter.Length == 0) return Str(before ? string.Empty : s);

        var idx = s.IndexOf(delimiter, StringComparison.Ordinal);
        return idx < 0 ? before ? Str(s) : Str(string.Empty) : Str(before ? s[..idx] : s[(idx + delimiter.Length)..]);
    }

    private static FormulaValue Dollar(IReadOnlyList<FormulaValue> values)
    {
        var x = Num(values, 0);
        var decimals = values.Count > 1 ? (int)Num(values, 1) : 2;
        return Str("$" + Math.Round(x, Math.Max(0, decimals)).ToString("N" + Math.Max(0, decimals), CultureInfo.InvariantCulture));
    }

    private static FormulaValue Fixed(IReadOnlyList<FormulaValue> values)
    {
        var x = Num(values, 0);
        var decimals = values.Count > 1 ? (int)Num(values, 1) : 2;
        var noCommas = values.Count > 2 && FormulaEvaluator.ToBoolean(values[2]);
        var rounded = Math.Round(x, Math.Max(0, decimals));
        return Str(rounded.ToString((noCommas ? "F" : "N") + Math.Max(0, decimals), CultureInfo.InvariantCulture));
    }

    // ── Extra date/time ─────────────────────────────────────────────────────────

    private static FormulaValue TimeValue(IReadOnlyList<FormulaValue> values) =>
        TimeSpan.TryParse(Text(values, 0), CultureInfo.InvariantCulture, out var ts)
            ? Number(ts.TotalDays)
            : FormulaValue.FromError(CellError.Value);

    private static FormulaValue Days360(IReadOnlyList<FormulaValue> values)
    {
        var start = DateTimeSerializer.ToDateTime(Num(values, 0), false);
        var end = DateTimeSerializer.ToDateTime(Num(values, 1), false);
        if (start is null || end is null) return FormulaValue.FromError(CellError.Number);

        var d1 = Math.Min(start.Value.Day, 30);
        var d2 = end.Value.Day;
        if (d2 == 31 && d1 == 30) d2 = 30;
        return Number(((end.Value.Year - start.Value.Year) * 360) + ((end.Value.Month - start.Value.Month) * 30) + (d2 - d1));
    }

    private static FormulaValue EDate(IReadOnlyList<FormulaValue> values)
    {
        var date = DateTimeSerializer.ToDateTime(Num(values, 0), false);
        return date is null ? FormulaValue.FromError(CellError.Number) : Number(DateTimeSerializer.ToSerial(date.Value.AddMonths((int)Num(values, 1)), false));
    }

    private static FormulaValue WeekNum(IReadOnlyList<FormulaValue> values)
    {
        var date = DateTimeSerializer.ToDateTime(Num(values, 0), false);
        if (date is null) return FormulaValue.FromError(CellError.Number);

        var jan1 = new DateTime(date.Value.Year, 1, 1);
        var days = (date.Value - jan1).Days;
        return Number(((days + (int)jan1.DayOfWeek) / 7.0) + 1);
    }

    private static FormulaValue IsoWeekNum(IReadOnlyList<FormulaValue> values)
    {
        var date = DateTimeSerializer.ToDateTime(Num(values, 0), false);
        return date is null ? FormulaValue.FromError(CellError.Number) : Number(ISOWeek.GetWeekOfYear(date.Value));
    }

    private static FormulaValue DateDif(IReadOnlyList<FormulaValue> values)
    {
        var start = DateTimeSerializer.ToDateTime(Num(values, 0), false);
        var end = DateTimeSerializer.ToDateTime(Num(values, 1), false);
        if (start is null || end is null) return FormulaValue.FromError(CellError.Number);

        var unit = Text(values, 2).ToUpperInvariant();
        return unit switch
        {
            "Y" => Number((int)((end.Value - start.Value).Days / 365.25)),
            "M" => Number(((end.Value.Year - start.Value.Year) * 12d) + end.Value.Month - start.Value.Month),
            "D" => Number((end.Value - start.Value).Days),
            "MD" => Number(Math.Abs(end.Value.Day - start.Value.Day)),
            _ => FormulaValue.FromError(CellError.Number)
        };
    }

    // ── Financial ────────────────────────────────────────────────────────────────

    private static double Pmt(double rate, double nper, double pv, double fv, int type)
    {
        if (rate == 0) return nper == 0 ? 0 : -(pv + fv) / nper;

        var pvif = Math.Pow(1 + rate, nper);
        var pmt = -rate * ((pv * pvif) + fv) / ((1 + (rate * type)) * (pvif - 1));
        return pmt;
    }

    private static double Fv(double rate, double nper, double pmt, double pv, int type)
    {
        if (rate == 0) return -(pv + (pmt * nper));

        var pvif = Math.Pow(1 + rate, nper);
        return -((pv * pvif) + (pmt * (1 + (rate * type)) * (pvif - 1) / rate));
    }

    private static double Pv(double rate, double nper, double pmt, double fv, int type)
    {
        if (rate == 0) return -(fv + (pmt * nper));

        var pvif = Math.Pow(1 + rate, nper);
        return -(fv + (pmt * (1 + (rate * type)) * (pvif - 1) / rate)) / pvif;
    }

    private static double NPer(double rate, double pmt, double pv, double fv)
    {
        if (rate == 0) return pmt == 0 ? 0 : -(pv + fv) / pmt;

        return Math.Log((pmt - (fv * rate)) / (pmt + (pv * rate))) / Math.Log(1 + rate);
    }

    private static FormulaValue Npv(IReadOnlyList<FormulaValue> values)
    {
        var rate = Num(values, 0);
        var flows = values.Skip(1).SelectMany(v => v.Flatten()).Where(IsNumber).Select(FormulaEvaluator.ToNumber).ToList();
        double total = 0;
        for (var i = 0; i < flows.Count; i++)
            total += flows[i] / Math.Pow(1 + rate, i + 1);
        return Number(total);
    }

    // ── Information ──────────────────────────────────────────────────────────────

    private static double TypeOf(IReadOnlyList<FormulaValue> values)
    {
        if (values.Count == 0) return 1;

        return values[0].Kind switch
        {
            FormulaValueKind.Number or FormulaValueKind.Blank => 1,
            FormulaValueKind.Text => 2,
            FormulaValueKind.Boolean => 4,
            FormulaValueKind.Error => 16,
            FormulaValueKind.Array => 64,
            _ => 1
        };
    }

    private static FormulaValue ErrorType(IReadOnlyList<FormulaValue> values)
    {
        if (values.Count == 0 || !values[0].IsError)
            return FormulaValue.FromError(CellError.NotAvailable);

        return Number(values[0].Error switch
        {
            CellError.Null => 1,
            CellError.DivisionByZero => 2,
            CellError.Value => 3,
            CellError.Reference => 4,
            CellError.Name => 5,
            CellError.Number => 6,
            CellError.NotAvailable => 7,
            _ => 8
        });
    }
}
