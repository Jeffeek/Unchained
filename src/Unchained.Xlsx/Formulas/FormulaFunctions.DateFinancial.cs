using System.Globalization;
using System.Text;
using Unchained.Xlsx.Formatting;
using Unchained.Xlsx.Models.Cell;

namespace Unchained.Xlsx.Formulas;

internal static partial class FormulaFunctions
{
    private static readonly (int Value, string Symbol)[] RomanTable =
    [
        (1000, "M"), (900, "CM"), (500, "D"), (400, "CD"), (100, "C"), (90, "XC"),
        (50, "L"), (40, "XL"), (10, "X"), (9, "IX"), (5, "V"), (4, "IV"), (1, "I")
    ];

    private static FormulaValue TryDate(IReadOnlyList<FormulaValue> values, Func<DateTime, FormulaValue> fn)
    {
        var date = DateTimeSerializer.ToDateTime(Num(values, 0), false);
        return date is null ? FormulaValue.FromError(CellError.Number) : fn(date.Value);
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

    private static FormulaValue DatePart(IReadOnlyList<FormulaValue> values, Func<DateTime, int> selector) =>
        TryDate(values, date => Number(selector(date)));

    private static FormulaValue Weekday(IReadOnlyList<FormulaValue> values)
    {
        var type = values.Count > 1 ? (int)Num(values, 1) : 1;
        return TryDate(
            values,
            date =>
            {
                var dow = (int)date.DayOfWeek;
                return type switch
                {
                    2 => Number(dow == 0 ? 7 : dow),
                    3 => Number((dow + 6) % 7),
                    _ => Number(dow + 1)
                };
            }
        );
    }

    private static FormulaValue DateValue(IReadOnlyList<FormulaValue> values) =>
        DateTime.TryParse(Text(values, 0), CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? Number(DateTimeSerializer.ToSerial(d, false))
            : FormulaValue.FromError(CellError.Value);

    private static FormulaValue EoMonth(IReadOnlyList<FormulaValue> values) =>
        TryDate(
            values,
            date =>
            {
                var shifted = date.AddMonths((int)Num(values, 1));
                var eom = new DateTime(shifted.Year, shifted.Month, DateTime.DaysInMonth(shifted.Year, shifted.Month));
                return Number(DateTimeSerializer.ToSerial(eom, false));
            }
        );

    private static FormulaValue TimeValue(IReadOnlyList<FormulaValue> values) =>
        DateTime.TryParse(Text(values, 0), CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? Number(d.TimeOfDay.TotalSeconds / 86400.0)
            : FormulaValue.FromError(CellError.Value);

    private static FormulaValue Days360(IReadOnlyList<FormulaValue> values)
    {
        var d1 = DateTimeSerializer.ToDateTime(Num(values, 0), false) ?? DateTime.MinValue;
        var d2 = DateTimeSerializer.ToDateTime(Num(values, 1), false) ?? DateTime.MinValue;
        var usa = values.Count > 2 && FormulaEvaluator.ToBoolean(values[2]);
        var day1 = d1.Day;
        var day2 = d2.Day;
        if (usa)
        {
            // European: 30 or 31 → 30 for both dates.
            if (day1 >= 30) day1 = 30;
            if (day2 >= 30) day2 = 30;
        }
        else
        {
            // US method: start date 31 → 30.
            if (day1 == 31) day1 = 30;
            // End date 31 → 30 only if start date was also 31.
            if (day2 == 31 && d1.Day == 31) day2 = 30;
        }

        return Number((360 * (d2.Year - d1.Year)) + (30 * (d2.Month - d1.Month)) + (day2 - day1));
    }

    private static FormulaValue EDate(IReadOnlyList<FormulaValue> values) =>
        TryDate(
            values,
            date =>
            {
                var months = (int)Num(values, 1);
                var result = new DateTime(date.Year, date.Month, 1).AddMonths(months);
                var day = Math.Min(date.Day, DateTime.DaysInMonth(result.Year, result.Month));
                return Number(DateTimeSerializer.ToSerial(result.AddDays(day - 1), false));
            }
        );

    private static FormulaValue WeekNum(IReadOnlyList<FormulaValue> values)
    {
        var type = values.Count > 1 ? (int)Num(values, 1) : 1;
        return TryDate(
            values,
            date =>
            {
                var cal = type == 21 ? new GregorianCalendar(GregorianCalendarTypes.USEnglish) : new GregorianCalendar();
                var firstDay = type switch
                {
                    1 => DayOfWeek.Sunday,
                    _ => DayOfWeek.Monday
                };
                return Number(cal.GetWeekOfYear(date, CalendarWeekRule.FirstDay, firstDay));
            }
        );
    }

    private static FormulaValue IsoWeekNum(IReadOnlyList<FormulaValue> values) =>
        TryDate(
            values,
            static date =>
            {
                var dayOfWeek = (int)date.DayOfWeek;
                if (dayOfWeek == 0) dayOfWeek = 7;
                var thursday = date.AddDays(4 - dayOfWeek);
                var isoYear = thursday.Year;
                var jan4 = new DateTime(isoYear, 1, 4);
                var jan4Dow = (int)jan4.DayOfWeek;
                if (jan4Dow == 0) jan4Dow = 7;
                var mondayOfWk1 = jan4.AddDays(-(jan4Dow - 1));
                var week = ((thursday - mondayOfWk1).Days / 7) + 1;
                return Number(week);
            }
        );

    private static FormulaValue TryTwoDates(IReadOnlyList<FormulaValue> values, Func<DateTime, DateTime, FormulaValue> fn)
    {
        var d1 = DateTimeSerializer.ToDateTime(Num(values, 0), false);
        var d2 = DateTimeSerializer.ToDateTime(Num(values, 1), false);
        return d1 is null || d2 is null ? FormulaValue.FromError(CellError.Number) : fn(d1.Value, d2.Value);
    }

    private static FormulaValue DateDif(IReadOnlyList<FormulaValue> values) =>
        TryTwoDates(
            values,
            (d1, d2) =>
            {
                var unit = Text(values, 2);
                return unit switch
                {
                    "D" => Number(Math.Floor(Num(values, 1)) - Math.Floor(Num(values, 0))),
                    "M" => Number(((d2.Year - d1.Year) * 12) + d2.Month - d1.Month),
                    "Y" => Number(d2.Year - d1.Year),
                    "MD" => Number(
                        d2.Day >= d1.Day
                            ? d2.Day - d1.Day
                            : d2.Day + DateTime.DaysInMonth(d2.Year, d2.Month - 1) - d1.Day
                    ),
                    "YM" => Number(d2.Month - d1.Month),
                    "YD" => Number((new DateTime(d2.Year, d1.Month, d1.Day) - d1).Days),
                    _ => FormulaValue.FromError(CellError.Value)
                };
            }
        );

    private static FormulaValue YearFrac(IReadOnlyList<FormulaValue> values)
    {
        var basis = values.Count > 2 ? (int)Math.Round(Num(values, 2)) : 0;
        return TryTwoDates(
            values,
            (d1, d2) =>
            {
                var days = (d2 - d1).Days;

                switch (basis)
                {
                    case 0 or 3:
                        return Number(days / 365.0);
                    case 1:
                        return Number((double)days / (DateTime.IsLeapYear(d1.Year) ? 366 : 365));
                    case 4:
                    {
                        var day1 = Math.Min(d1.Day, 30);
                        var day2 = d2.Day == 31 && day1 >= 30 ? 30 : d2.Day;
                        var y = ((d2.Year - d1.Year) * 360) + ((d2.Month - d1.Month) * 30) + (day2 - day1);
                        return Number(y / 360.0);
                    }
                    default:
                        return Number(days / 365.0);
                }
            }
        );
    }

    // ── Combinators ─────────────────────────────────────────────────────────────

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

    private static string ToRoman(int value)
    {
        if (value is <= 0 or > 3999) return string.Empty;

        var sb = new StringBuilder();
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

        var sb = new StringBuilder();
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

    // ── Financial ───────────────────────────────────────────────────────────────

    private static double Pmt(double rate, double nper, double pv, double fv = 0, int type = 0)
    {
        if (rate == 0) return -(pv + fv) / nper;

        var factor = Math.Pow(1 + rate, nper);
        var annuity = (pv * factor) + fv;
        return annuity * rate / (1 + (rate * type)) / (1 - factor);
    }

    private static double Fv(double rate, double nper, double pmt, double pv = 0, int type = 0)
    {
        if (rate == 0) return -(pv + (pmt * nper));

        var factor = Math.Pow(1 + rate, nper);
        var annuity = pmt * (1 + (rate * type)) * (factor - 1) / rate;
        return -((pv * factor) + annuity);
    }

    private static double Pv(double rate, double nper, double pmt, double fv = 0, int type = 0)
    {
        if (rate == 0) return -(fv + (pmt * nper));

        var factor = Math.Pow(1 + rate, nper);
        var annuity = pmt * (1 + (rate * type)) * (factor - 1) / (rate * factor);
        return -(fv + annuity);
    }

    private static double NPer(double rate, double pmt, double pv, double fv = 0)
    {
        if (rate == 0) return -(pv + fv) / pmt;

        var numerator = (pmt * (1 + rate)) - (fv * rate);
        var denominator = (pv * rate) + pmt;
        return denominator == 0 ? double.NaN : Math.Log(numerator / denominator) / Math.Log(1 + rate);
    }

    private static FormulaValue Npv(IReadOnlyList<FormulaValue> values)
    {
        if (values.Count < 2) return FormulaValue.FromError(CellError.Value);

        var rate = Num(values, 0);
        if (rate < 0) return FormulaValue.FromError(CellError.Number);

        var cashflows = values.Skip(1).Select(FormulaEvaluator.ToNumber).ToList();
        if (cashflows.Count == 0) return Number(0);

        var npv = cashflows.Select((t1, t) => t1 / Math.Pow(1 + rate, t + 1)).Sum();
        return Number(npv);
    }

    // ── Information ─────────────────────────────────────────────────────────────

    private static FormulaValue TypeOf(IReadOnlyList<FormulaValue> values)
    {
        if (values.Count == 0) return Number(1);

        var v = values[0];
        return v.Kind switch
        {
            FormulaValueKind.Blank => Number(1),
            FormulaValueKind.Number => Number(2),
            FormulaValueKind.Text => Number(4),
            FormulaValueKind.Boolean => Number(16),
            FormulaValueKind.Error => Number(16),
            FormulaValueKind.Array => Number(64),
            _ => Number(1)
        };
    }

    private static FormulaValue ErrorType(IReadOnlyList<FormulaValue> values) =>
        values.Count == 0 || !values[0].IsError
            ? FormulaValue.FromError(CellError.Value)
            : values[0].Error switch
            {
                CellError.Null => Number(1),
                CellError.DivisionByZero => Number(2),
                CellError.Value => Number(3),
                CellError.Reference => Number(4),
                CellError.Name => Number(5),
                CellError.Number => Number(6),
                _ => Number(7)
            };
}
