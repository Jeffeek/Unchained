using System.Collections.Frozen;

namespace Unchained.Xlsx.Formatting;

/// <summary>
///     The ECMA-376 built-in number formats (ids 0–163). These never appear in <c>styles.xml</c>;
///     the ids are implicit. Only the well-defined ids are listed; locale-specific ids in the
///     27–36 / 49–58 ranges are treated as date/time by the detection heuristic.
/// </summary>
internal static class BuiltInNumberFormats
{
    public static readonly FrozenDictionary<int, string> Codes = new Dictionary<int, string>
    {
        [0] = "General",
        [1] = "0",
        [2] = "0.00",
        [3] = "#,##0",
        [4] = "#,##0.00",
        [9] = "0%",
        [10] = "0.00%",
        [11] = "0.00E+00",
        [12] = "# ?/?",
        [13] = "# ??/??",
        [14] = "mm-dd-yy",
        [15] = "d-mmm-yy",
        [16] = "d-mmm",
        [17] = "mmm-yy",
        [18] = "h:mm AM/PM",
        [19] = "h:mm:ss AM/PM",
        [20] = "h:mm",
        [21] = "h:mm:ss",
        [22] = "m/d/yy h:mm",
        [37] = "#,##0 ;(#,##0)",
        [38] = "#,##0 ;[Red](#,##0)",
        [39] = "#,##0.00;(#,##0.00)",
        [40] = "#,##0.00;[Red](#,##0.00)",
        [44] = "_(\"$\"* #,##0.00_);_(\"$\"* (#,##0.00);_(\"$\"* \"-\"??_);_(@_)",
        [45] = "mm:ss",
        [46] = "[h]:mm:ss",
        [47] = "mmss.0",
        [48] = "##0.0E+0",
        [49] = "@"
    }.ToFrozenDictionary();

    // Built-in ids whose format is a date and/or time.
    private static readonly FrozenSet<int> DateTimeIds =
        new[] { 14, 15, 16, 17, 18, 19, 20, 21, 22, 45, 46, 47 }.ToFrozenSet();

    /// <summary>Returns the format code for a built-in id, or <see langword="null" /> if not a known built-in.</summary>
    public static string? GetCode(int id) => Codes.GetValueOrDefault(id);

    /// <summary>
    ///     Returns whether the given built-in id (or locale-specific range) denotes a date/time format.
    /// </summary>
    public static bool IsDateTimeBuiltIn(int id) =>
        DateTimeIds.Contains(id) || id is >= 27 and <= 36 || id is >= 50 and <= 58;
}
