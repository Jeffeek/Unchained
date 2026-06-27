using System.Globalization;

namespace Unchained.Xlsx.Formatting;

/// <summary>
///     Formats a numeric cell value using an Excel number-format code. This is a partial
///     implementation covering the common v0.1.0 cases — general, fixed/decimal, thousands,
///     percentage, scientific, text, and basic date/time patterns. Complex format features
///     (conditional sections, colour codes, locale brackets, fill/skip characters) fall back to a
///     plain culture-invariant rendering. See <c>research-notes.md</c> Difficulty 2.
/// </summary>
internal static class NumberFormatter
{
    /// <summary>Formats <paramref name="value" /> using <paramref name="formatCode" />, honouring the date system.</summary>
    public static string Format(double value, string formatCode, bool date1904)
    {
        if (string.IsNullOrEmpty(formatCode) || formatCode.Equals("General", StringComparison.OrdinalIgnoreCase))
            return value.ToString("G15", CultureInfo.InvariantCulture);

        // Text placeholder only.
        if (formatCode == "@")
            return value.ToString("G15", CultureInfo.InvariantCulture);

        // Use the positive section for non-negative values, the negative section for negatives.
        var sections = SplitSections(formatCode);
        var (section, useAbsoluteValue) = SelectSection(sections, value);

        return IsDateTimeFormatCode(section)
            ? FormatDateTime(value, section, date1904)
            : FormatNumeric(useAbsoluteValue ? Math.Abs(value) : value, section);
    }

    /// <summary>
    ///     Returns whether <paramref name="formatCode" /> is a date/time format: contains an unquoted,
    ///     unbracketed date/time placeholder character.
    /// </summary>
    public static bool IsDateTimeFormatCode(string formatCode)
    {
        bool inQuotes = false, inBrackets = false;
        for (var i = 0; i < formatCode.Length; i++)
        {
            var c = formatCode[i];
            switch (c)
            {
                case '"':
                    inQuotes = !inQuotes;
                    continue;
                case '[':
                    inBrackets = true;
                    continue;
                case ']':
                    inBrackets = false;
                    continue;
                case '\\':
                    i++;
                    continue; // escaped char
            }

            if (inQuotes || inBrackets)
                continue;

            if (c is 'y' or 'Y' or 'd' or 'D' or 'h' or 'H' or 's' or 'S' or 'm' or 'M')
                return true;
        }

        return false;
    }

    private static (string Section, bool UseAbsoluteValue) SelectSection(IReadOnlyList<string> sections, double value) =>
        // When a dedicated negative section exists, it renders the magnitude (the section itself
        // carries the sign convention, e.g. parentheses).
        value switch
        {
            < 0 when sections.Count > 1 => (sections[1], true),
            0 when sections.Count > 2 => (sections[2], false),
            _ => (sections[0], false)
        };

    private static List<string> SplitSections(string formatCode)
    {
        var sections = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;

        foreach (var c in formatCode)
        {
            switch (c)
            {
                case '"':
                    inQuotes = !inQuotes;
                    current.Append(c);
                break;
                case ';' when !inQuotes:
                    sections.Add(current.ToString());
                    current.Clear();
                break;
                default:
                    current.Append(c);
                break;
            }
        }

        sections.Add(current.ToString());
        return sections;
    }

    private static string FormatNumeric(double value, string section)
    {
        var absValue = Math.Abs(value);

        // Percentage.
        if (section.Contains('%'))
        {
            var decimals = CountDecimals(section);
            return absValue.ToString("P" + decimals, CultureInfo.InvariantCulture)
                .Replace(" %", "%"); // .NET inserts a space before %
        }

        // Scientific.
        if (section.Contains("E+", StringComparison.OrdinalIgnoreCase) ||
            section.Contains("e+", StringComparison.OrdinalIgnoreCase))
        {
            var decimals = CountDecimals(section.Split('E', 'e')[0]);
            var sci = value.ToString("E" + decimals, CultureInfo.InvariantCulture);
            return sci.Replace("E", "E", StringComparison.Ordinal);
        }

        var decimalPlaces = CountDecimals(section);
        var grouping = section.Contains("#,##") || section.Contains("#,#") || section.Contains(",0");
        var format = grouping ? "N" + decimalPlaces : "F" + decimalPlaces;
        var rendered = value.ToString(format, CultureInfo.InvariantCulture);

        var (prefix, suffix) = ExtractLiteralAffixes(section);
        return prefix + rendered + suffix;
    }

    /// <summary>
    ///     Extracts literal prefix/suffix characters around the numeric placeholder body (e.g. the
    ///     parentheses in <c>(0.00)</c> or a currency/units literal). Placeholder and grouping
    ///     characters are not literals.
    /// </summary>
    private static (string Prefix, string Suffix) ExtractLiteralAffixes(string section)
    {
        var start = 0;
        while (start < section.Length && !IsPlaceholderChar(section[start]))
            start++;

        var end = section.Length - 1;
        while (end >= start && !IsPlaceholderChar(section[end]))
            end--;

        var prefix = CleanLiteral(section[..start]);
        var suffix = end + 1 < section.Length ? CleanLiteral(section[(end + 1)..]) : string.Empty;
        return (prefix, suffix);

        static bool IsPlaceholderChar(char c) => c is '0' or '#' or '?';

        static string CleanLiteral(string raw) =>
            raw.Replace("\"", string.Empty).Replace("\\", string.Empty);
    }

    private static int CountDecimals(string section)
    {
        var dot = section.IndexOf('.');
        if (dot < 0)
            return 0;

        var count = 0;
        for (var i = dot + 1; i < section.Length && section[i] is '0' or '#'; i++)
            count++;
        return count;
    }

    private static string FormatDateTime(double serial, string formatCode, bool date1904)
    {
        var dateTime = DateTimeSerializer.ToDateTime(serial, date1904);
        if (dateTime is null)
            return serial.ToString("G15", CultureInfo.InvariantCulture);

        var net = ToNetDateFormat(formatCode);
        return dateTime.Value.ToString(net, CultureInfo.InvariantCulture);
    }

    /// <summary>Translates a (simple) Excel date/time format code into a .NET custom format string.</summary>
    private static string ToNetDateFormat(string excel)
    {
        var result = new System.Text.StringBuilder(excel.Length);
        var hasAmPm = excel.Contains("AM/PM", StringComparison.OrdinalIgnoreCase);
        var inQuotes = false;
        var lastTimeUnit = '\0'; // tracks whether we just saw an hour, for m = minutes

        for (var i = 0; i < excel.Length; i++)
        {
            var c = excel[i];

            if (c == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (inQuotes)
            {
                result.Append(c);
                continue;
            }

            // AM/PM marker → tt.
            if (hasAmPm && i + 5 <= excel.Length && excel.Substring(i, 5).Equals("AM/PM", StringComparison.OrdinalIgnoreCase))
            {
                result.Append("tt");
                i += 4;
                continue;
            }

            switch (c)
            {
                case 'y':
                case 'd':
                    result.Append(c);
                    lastTimeUnit = '\0';
                break;
                case 's':
                    result.Append('s');
                    lastTimeUnit = 's';
                break;
                case 'm':
                case 'M':
                {
                    // 'm' is minutes when it directly follows an hour or directly precedes seconds;
                    // otherwise it is a month.
                    var isMinute = lastTimeUnit is 'h' or 'H' || NextNonMSatChar(excel, i) is 's';
                    result.Append(isMinute ? 'm' : 'M');
                    break;
                }
                case 'h':
                    result.Append(hasAmPm ? 'h' : 'H');
                    lastTimeUnit = 'h';
                break;
                case 'H':
                    result.Append('H');
                    lastTimeUnit = 'H';
                break;
                default:
                    result.Append(c);
                    lastTimeUnit = '\0';
                break;
            }
        }

        return result.ToString();

        static char NextNonMSatChar(string s, int i)
        {
            for (var j = i + 1; j < s.Length; j++)
            {
                if (s[j] is not ('m' or 'M'))
                    return s[j];
            }

            return '\0';
        }
    }
}
