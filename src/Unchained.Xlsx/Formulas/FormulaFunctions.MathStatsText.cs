using System.Globalization;
using Unchained.Xlsx.Formatting;
using Unchained.Xlsx.Models.Cell;

namespace Unchained.Xlsx.Formulas;

internal static partial class FormulaFunctions
{
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

    private static FormulaValue GeoMean(IEnumerable<FormulaValue> values)
    {
        var nums = Nums(values).ToList();
        if (nums.Count == 0 || nums.Any(static x => x <= 0))
            return FormulaValue.FromError(CellError.Number);

        return Number(Math.Exp(nums.Average(static x => Math.Log(x))));
    }

    private static FormulaValue HarMean(IEnumerable<FormulaValue> values)
    {
        var nums = Nums(values).ToList();
        if (nums.Count == 0) return FormulaValue.FromError(CellError.Number);

        var recip = nums.Sum(static x => 1.0 / x);
        return recip == 0 ? FormulaValue.FromError(CellError.DivisionByZero) : Number(nums.Count / recip);
    }

    private static FormulaValue AveDev(IEnumerable<FormulaValue> values)
    {
        var nums = Nums(values).ToList();
        if (nums.Count == 0) return FormulaValue.FromError(CellError.Number);

        var mean = nums.Average();
        return Number(nums.Sum(x => Math.Abs(x - mean)) / nums.Count);
    }

    private static FormulaValue DevSq(IEnumerable<FormulaValue> values)
    {
        var nums = Nums(values).ToList();
        if (nums.Count < 2) return FormulaValue.FromError(CellError.DivisionByZero);

        var mean = nums.Average();
        return Number(nums.Sum(x => (x - mean) * (x - mean)));
    }

    private static FormulaValue PercentRank(IReadOnlyList<FormulaValue> values)
    {
        var arr = values[0].Flatten().Where(IsNumber).Select(FormulaEvaluator.ToNumber).OrderBy(static x => x).ToList();
        if (arr.Count == 0) return FormulaValue.FromError(CellError.Number);

        var x = Num(values, 1);
        var below = arr.Count(e => e < x);
        var eq = arr.Count(e => e.Equals(x));
        return eq == 0
            ? Number((double)below / Math.Max(arr.Count - 1, 1))
            : Number((below + (0.5 * eq)) / Math.Max(arr.Count - 1, 1));
    }

    private static FormulaValue Quartile(IReadOnlyList<FormulaValue> values)
    {
        var arr = values[0].Flatten().Where(IsNumber).Select(FormulaEvaluator.ToNumber).OrderBy(static x => x).ToList();
        if (arr.Count == 0) return FormulaValue.FromError(CellError.Number);

        var q = Num(values, 1);
        if (q is < 0 or > 4) return FormulaValue.FromError(CellError.Number);

        var rank = q / 4.0 * (arr.Count - 1);
        var lo = (int)Math.Floor(rank);
        var hi = (int)Math.Ceiling(rank);
        if (hi >= arr.Count) hi = arr.Count - 1;
        return Number(arr[lo] + ((rank - lo) * (arr[hi] - arr[lo])));
    }

    private static FormulaValue Skew(IEnumerable<FormulaValue> values)
    {
        var nums = Nums(values).ToList();
        if (nums.Count < 3) return FormulaValue.FromError(CellError.Number);

        var mean = nums.Average();
        var n = (double)nums.Count;
        var s = Math.Pow(nums.Sum(x => Math.Pow(x - mean, 3)) / n, 1.0 / 3.0);
        return s == 0 ? Number(0) : Number(n * nums.Average(x => Math.Pow((x - mean) / s, 3)));
    }

    private static FormulaValue TrimMean(IReadOnlyList<FormulaValue> values)
    {
        var nums = Nums(values).ToList();
        if (nums.Count < 2) return FormulaValue.FromError(CellError.Number);

        var percent = Math.Clamp(Num(values, 1), 0, 1);
        var count = (int)Math.Round(nums.Count * percent);
        if (count * 2 >= nums.Count) return FormulaValue.FromError(CellError.Number);

        nums.Sort();
        return Number(nums[count..^count].Average());
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
        try { return Str(NumberFormatter.Format(x, format, false)); }
        catch { return Str(x.ToString("G15", CultureInfo.InvariantCulture)); }
    }

    private static FormulaValue TextBeforeAfter(IReadOnlyList<FormulaValue> values, bool before)
    {
        var text = Text(values, 0);
        var delimiter = Text(values, 1);
        var instance = values.Count > 2 ? (int)Num(values, 2) : 1;
        if (instance < 1 || string.IsNullOrEmpty(delimiter)) return Str(string.Empty);

        var occurrences = 0;
        var start = 0;
        while (start <= text.Length)
        {
            var pos = text.IndexOf(delimiter, start, StringComparison.Ordinal);
            if (pos < 0) break;

            if (++occurrences == instance)
            {
                var end = before ? pos : text.Length;
                var s = before ? start : pos + delimiter.Length;
                return Str(text[s..end]);
            }

            start = pos + delimiter.Length;
        }

        return Str(string.Empty);
    }

    private static FormulaValue Dollar(IReadOnlyList<FormulaValue> values)
    {
        var x = Num(values, 0);
        var decimals = values.Count > 1 ? (int)Num(values, 1) : 2;
        decimals = Math.Clamp(decimals, 0, 10);
        return Str("$" + x.ToString("F" + decimals, CultureInfo.InvariantCulture));
    }

    private static FormulaValue Fixed(IReadOnlyList<FormulaValue> values)
    {
        var x = Num(values, 0);
        var decimals = values.Count > 1 ? (int)Num(values, 1) : 2;
        decimals = Math.Clamp(decimals, 0, 30);
        var thousands = values.Count > 2 && FormulaEvaluator.ToBoolean(values[2]);
        var formatted = x.ToString("F" + decimals, CultureInfo.InvariantCulture);
        return thousands && decimals < 15
            ? Str(string.Join(",", formatted.Split('.')))
            : Str(formatted);
    }

    // ── Utility helpers ─────────────────────────────────────────────────────────

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
}
