namespace Unchained.Studio.Studio.Xlsx;

/// <summary>
///     A single entry in the formula helper: a function name, its argument signature, a category,
///     and a one-line description. Used to drive the formula-bar autocomplete in the XLSX tab.
/// </summary>
/// <param name="Name">The function name as typed in a formula (e.g. <c>SUM</c>).</param>
/// <param name="Signature">The argument hint shown after the name (e.g. <c>(number1, [number2], …)</c>).</param>
/// <param name="Category">The grouping shown in the helper (Math, Statistical, Text, …).</param>
/// <param name="Description">A short one-line description of what the function does.</param>
public sealed record FormulaFunctionInfo(
    string Name,
    string Signature,
    string Category,
    string Description
);

/// <summary>
///     The catalogue of formula functions the Unchained.Xlsx in-engine evaluator supports, used by the
///     formula helper. Hand-authored to mirror <c>Unchained.Xlsx.Formulas.FormulaFunctions</c>; keep in
///     sync when functions are added to the evaluator.
/// </summary>
public static class FormulaCatalog
{
    /// <summary>All catalogued functions, ordered by category then name.</summary>
    public static readonly IReadOnlyList<FormulaFunctionInfo> All =
    [
        // ── Math ──────────────────────────────────────────────────────────────
        new("SUM", "(number1, [number2], …)", "Math", "Adds all the numbers in the arguments or ranges."),
        new("SUMIF", "(range, criteria, [sum_range])", "Math", "Adds the cells that meet a single condition."),
        new("SUMIFS", "(sum_range, range1, criteria1, …)", "Math", "Adds cells that meet multiple conditions."),
        new("SUMPRODUCT", "(array1, [array2], …)", "Math", "Multiplies corresponding values and sums the results."),
        new("SUMSQ", "(number1, [number2], …)", "Math", "Returns the sum of the squares of the arguments."),
        new("PRODUCT", "(number1, [number2], …)", "Math", "Multiplies all the numbers in the arguments."),
        new("QUOTIENT", "(numerator, denominator)", "Math", "Returns the integer portion of a division."),
        new("MOD", "(number, divisor)", "Math", "Returns the remainder after division."),
        new("ABS", "(number)", "Math", "Returns the absolute value of a number."),
        new("SIGN", "(number)", "Math", "Returns the sign of a number (-1, 0, or 1)."),
        new("ROUND", "(number, num_digits)", "Math", "Rounds a number to a specified number of digits."),
        new("ROUNDUP", "(number, num_digits)", "Math", "Rounds a number up, away from zero."),
        new("ROUNDDOWN", "(number, num_digits)", "Math", "Rounds a number down, toward zero."),
        new("MROUND", "(number, multiple)", "Math", "Rounds a number to the nearest multiple."),
        new("CEILING", "(number, significance)", "Math", "Rounds up to the nearest multiple of significance."),
        new("FLOOR", "(number, significance)", "Math", "Rounds down to the nearest multiple of significance."),
        new("INT", "(number)", "Math", "Rounds a number down to the nearest integer."),
        new("TRUNC", "(number, [num_digits])", "Math", "Truncates a number to an integer or given digits."),
        new("POWER", "(number, power)", "Math", "Returns a number raised to a power."),
        new("SQRT", "(number)", "Math", "Returns the positive square root."),
        new("SQRTPI", "(number)", "Math", "Returns the square root of (number × π)."),
        new("EXP", "(number)", "Math", "Returns e raised to the power of a number."),
        new("LN", "(number)", "Math", "Returns the natural logarithm."),
        new("LOG", "(number, [base])", "Math", "Returns the logarithm to a specified base."),
        new("LOG10", "(number)", "Math", "Returns the base-10 logarithm."),
        new("GCD", "(number1, [number2], …)", "Math", "Returns the greatest common divisor."),
        new("LCM", "(number1, [number2], …)", "Math", "Returns the least common multiple."),
        new("FACT", "(number)", "Math", "Returns the factorial of a number."),
        new("FACTDOUBLE", "(number)", "Math", "Returns the double factorial of a number."),
        new("COMBIN", "(number, number_chosen)", "Math", "Returns the number of combinations."),
        new("PERMUT", "(number, number_chosen)", "Math", "Returns the number of permutations."),
        new("EVEN", "(number)", "Math", "Rounds up to the nearest even integer."),
        new("ODD", "(number)", "Math", "Rounds up to the nearest odd integer."),
        new("PI", "()", "Math", "Returns the value of π."),
        new("RAND", "()", "Math", "Returns a random number between 0 and 1."),
        new("RANDBETWEEN", "(bottom, top)", "Math", "Returns a random integer in a range."),
        new("ROMAN", "(number)", "Math", "Converts an Arabic numeral to Roman text."),
        new("ARABIC", "(text)", "Math", "Converts a Roman numeral to an Arabic number."),
        new("BASE", "(number, radix, [min_length])", "Math", "Converts a number to text in a given base."),
        new("DECIMAL", "(text, radix)", "Math", "Converts a text representation in a base to a number."),

        // ── Trigonometry ────────────────────────────────────────────────────────
        new("SIN", "(number)", "Trig", "Returns the sine of an angle (radians)."),
        new("COS", "(number)", "Trig", "Returns the cosine of an angle (radians)."),
        new("TAN", "(number)", "Trig", "Returns the tangent of an angle (radians)."),
        new("ASIN", "(number)", "Trig", "Returns the arcsine of a number."),
        new("ACOS", "(number)", "Trig", "Returns the arccosine of a number."),
        new("ATAN", "(number)", "Trig", "Returns the arctangent of a number."),
        new("ATAN2", "(x_num, y_num)", "Trig", "Returns the arctangent from x- and y-coordinates."),
        new("SINH", "(number)", "Trig", "Returns the hyperbolic sine."),
        new("COSH", "(number)", "Trig", "Returns the hyperbolic cosine."),
        new("TANH", "(number)", "Trig", "Returns the hyperbolic tangent."),
        new("ASINH", "(number)", "Trig", "Returns the inverse hyperbolic sine."),
        new("ACOSH", "(number)", "Trig", "Returns the inverse hyperbolic cosine."),
        new("ATANH", "(number)", "Trig", "Returns the inverse hyperbolic tangent."),
        new("SEC", "(number)", "Trig", "Returns the secant of an angle."),
        new("CSC", "(number)", "Trig", "Returns the cosecant of an angle."),
        new("COT", "(number)", "Trig", "Returns the cotangent of an angle."),
        new("DEGREES", "(angle)", "Trig", "Converts radians to degrees."),
        new("RADIANS", "(angle)", "Trig", "Converts degrees to radians."),

        // ── Statistical ──────────────────────────────────────────────────────────
        new("AVERAGE", "(number1, [number2], …)", "Statistical", "Returns the arithmetic mean."),
        new("AVERAGEA", "(value1, [value2], …)", "Statistical", "Averages values including text and logicals."),
        new("AVERAGEIF", "(range, criteria, [average_range])", "Statistical", "Averages cells meeting one condition."),
        new("AVERAGEIFS", "(average_range, range1, criteria1, …)", "Statistical", "Averages cells meeting multiple conditions."),
        new("COUNT", "(value1, [value2], …)", "Statistical", "Counts the cells that contain numbers."),
        new("COUNTA", "(value1, [value2], …)", "Statistical", "Counts the cells that are not empty."),
        new("COUNTBLANK", "(range)", "Statistical", "Counts empty cells in a range."),
        new("COUNTIF", "(range, criteria)", "Statistical", "Counts cells meeting one condition."),
        new("COUNTIFS", "(range1, criteria1, …)", "Statistical", "Counts cells meeting multiple conditions."),
        new("MAX", "(number1, [number2], …)", "Statistical", "Returns the largest value."),
        new("MAXA", "(value1, [value2], …)", "Statistical", "Returns the largest value including logicals."),
        new("MAXIFS", "(max_range, range1, criteria1, …)", "Statistical", "Maximum of cells meeting conditions."),
        new("MIN", "(number1, [number2], …)", "Statistical", "Returns the smallest value."),
        new("MINA", "(value1, [value2], …)", "Statistical", "Returns the smallest value including logicals."),
        new("MINIFS", "(min_range, range1, criteria1, …)", "Statistical", "Minimum of cells meeting conditions."),
        new("MEDIAN", "(number1, [number2], …)", "Statistical", "Returns the median value."),
        new("MODE", "(number1, [number2], …)", "Statistical", "Returns the most frequent value."),
        new("LARGE", "(array, k)", "Statistical", "Returns the k-th largest value."),
        new("SMALL", "(array, k)", "Statistical", "Returns the k-th smallest value."),
        new("RANK", "(number, ref, [order])", "Statistical", "Returns the rank of a number in a list."),
        new("STDEV", "(number1, [number2], …)", "Statistical", "Estimates standard deviation of a sample."),
        new("STDEVP", "(number1, [number2], …)", "Statistical", "Standard deviation of an entire population."),
        new("VAR", "(number1, [number2], …)", "Statistical", "Estimates variance of a sample."),
        new("VARP", "(number1, [number2], …)", "Statistical", "Variance of an entire population."),
        new("GEOMEAN", "(number1, [number2], …)", "Statistical", "Returns the geometric mean."),
        new("HARMEAN", "(number1, [number2], …)", "Statistical", "Returns the harmonic mean."),
        new("PERCENTILE", "(array, k)", "Statistical", "Returns the k-th percentile of values."),
        new("QUARTILE", "(array, quart)", "Statistical", "Returns the quartile of a data set."),
        new("PERCENTRANK", "(array, x, [sig])", "Statistical", "Rank of a value as a percentage of a set."),
        new("SKEW", "(number1, [number2], …)", "Statistical", "Returns the skewness of a distribution."),
        new("AVEDEV", "(number1, [number2], …)", "Statistical", "Average of absolute deviations from the mean."),
        new("DEVSQ", "(number1, [number2], …)", "Statistical", "Sum of squares of deviations from the mean."),
        new("TRIMMEAN", "(array, percent)", "Statistical", "Mean of the interior of a data set."),

        // ── Logical ────────────────────────────────────────────────────────────
        new("IF", "(logical_test, value_if_true, value_if_false)", "Logical", "Returns one value if true, another if false."),
        new("IFS", "(test1, value1, [test2, value2], …)", "Logical", "Returns the value for the first true condition."),
        new("IFERROR", "(value, value_if_error)", "Logical", "Returns a fallback if the value is an error."),
        new("IFNA", "(value, value_if_na)", "Logical", "Returns a fallback if the value is #N/A."),
        new("SWITCH", "(expr, value1, result1, …, [default])", "Logical", "Matches an expression against values."),
        new("AND", "(logical1, [logical2], …)", "Logical", "TRUE if all arguments are TRUE."),
        new("OR", "(logical1, [logical2], …)", "Logical", "TRUE if any argument is TRUE."),
        new("XOR", "(logical1, [logical2], …)", "Logical", "Returns the exclusive OR of arguments."),
        new("NOT", "(logical)", "Logical", "Reverses the logic of its argument."),
        new("TRUE", "()", "Logical", "Returns the logical value TRUE."),
        new("FALSE", "()", "Logical", "Returns the logical value FALSE."),

        // ── Lookup ────────────────────────────────────────────────────────────────
        new("VLOOKUP", "(lookup_value, table, col_index, [range_lookup])", "Lookup", "Looks up a value in the first column of a table."),
        new("HLOOKUP", "(lookup_value, table, row_index, [range_lookup])", "Lookup", "Looks up a value in the first row of a table."),
        new("INDEX", "(array, row_num, [column_num])", "Lookup", "Returns a value at a position in a range."),
        new("MATCH", "(lookup_value, lookup_array, [match_type])", "Lookup", "Returns the position of a value in a range."),
        new("CHOOSE", "(index_num, value1, [value2], …)", "Lookup", "Returns a value from a list by index."),

        // ── Text ──────────────────────────────────────────────────────────────────
        new("CONCAT", "(text1, [text2], …)", "Text", "Joins text from ranges and strings."),
        new("CONCATENATE", "(text1, [text2], …)", "Text", "Joins several text items into one."),
        new("TEXTJOIN", "(delimiter, ignore_empty, text1, …)", "Text", "Joins text with a delimiter."),
        new("LEN", "(text)", "Text", "Returns the number of characters in text."),
        new("LEFT", "(text, [num_chars])", "Text", "Returns characters from the start of text."),
        new("RIGHT", "(text, [num_chars])", "Text", "Returns characters from the end of text."),
        new("MID", "(text, start_num, num_chars)", "Text", "Returns characters from the middle of text."),
        new("UPPER", "(text)", "Text", "Converts text to uppercase."),
        new("LOWER", "(text)", "Text", "Converts text to lowercase."),
        new("PROPER", "(text)", "Text", "Capitalises the first letter of each word."),
        new("TRIM", "(text)", "Text", "Removes extra spaces from text."),
        new("CLEAN", "(text)", "Text", "Removes non-printable characters."),
        new("REPT", "(text, number_times)", "Text", "Repeats text a given number of times."),
        new("FIND", "(find_text, within_text, [start])", "Text", "Finds text within text (case-sensitive)."),
        new("SEARCH", "(find_text, within_text, [start])", "Text", "Finds text within text (case-insensitive)."),
        new("SUBSTITUTE", "(text, old_text, new_text, [n])", "Text", "Replaces occurrences of text."),
        new("REPLACE", "(old_text, start, num_chars, new_text)", "Text", "Replaces part of text by position."),
        new("EXACT", "(text1, text2)", "Text", "TRUE if two text values are identical."),
        new("CHAR", "(number)", "Text", "Returns the character for a code number."),
        new("CODE", "(text)", "Text", "Returns the code of the first character."),
        new("UNICHAR", "(number)", "Text", "Returns the Unicode character for a number."),
        new("UNICODE", "(text)", "Text", "Returns the Unicode code point of a character."),
        new("VALUE", "(text)", "Text", "Converts text to a number."),
        new("NUMBERVALUE", "(text, [decimal], [group])", "Text", "Converts locale-formatted text to a number."),
        new("TEXT", "(value, format_text)", "Text", "Formats a number as text with a format code."),
        new("FIXED", "(number, [decimals], [no_commas])", "Text", "Formats a number with fixed decimals."),
        new("DOLLAR", "(number, [decimals])", "Text", "Formats a number as currency text."),
        new("TEXTBEFORE", "(text, delimiter, [instance])", "Text", "Returns text before a delimiter."),
        new("TEXTAFTER", "(text, delimiter, [instance])", "Text", "Returns text after a delimiter."),
        new("T", "(value)", "Text", "Returns the text referred to by a value."),

        // ── Date & Time ──────────────────────────────────────────────────────────
        new("DATE", "(year, month, day)", "Date", "Builds a date from year, month, and day."),
        new("TODAY", "()", "Date", "Returns today's date."),
        new("NOW", "()", "Date", "Returns the current date and time."),
        new("YEAR", "(serial_number)", "Date", "Returns the year of a date."),
        new("MONTH", "(serial_number)", "Date", "Returns the month of a date."),
        new("DAY", "(serial_number)", "Date", "Returns the day of the month of a date."),
        new("HOUR", "(serial_number)", "Date", "Returns the hour of a time."),
        new("MINUTE", "(serial_number)", "Date", "Returns the minute of a time."),
        new("SECOND", "(serial_number)", "Date", "Returns the second of a time."),
        new("TIME", "(hour, minute, second)", "Date", "Builds a time value."),
        new("WEEKDAY", "(serial_number, [return_type])", "Date", "Returns the day of the week as a number."),
        new("WEEKNUM", "(serial_number, [return_type])", "Date", "Returns the week number of a date."),
        new("ISOWEEKNUM", "(date)", "Date", "Returns the ISO week number of a date."),
        new("DATEVALUE", "(date_text)", "Date", "Converts a date in text to a serial number."),
        new("TIMEVALUE", "(time_text)", "Date", "Converts a time in text to a serial number."),
        new("DAYS", "(end_date, start_date)", "Date", "Returns the number of days between two dates."),
        new("DAYS360", "(start_date, end_date, [method])", "Date", "Days between dates on a 360-day year."),
        new("EDATE", "(start_date, months)", "Date", "Returns a date a number of months away."),
        new("EOMONTH", "(start_date, months)", "Date", "Returns the last day of a month offset."),
        new("YEARFRAC", "(start_date, end_date, [basis])", "Date", "Year fraction between two dates."),
        new("DATEDIF", "(start_date, end_date, unit)", "Date", "Difference between dates in a unit."),

        // ── Information ──────────────────────────────────────────────────────────
        new("ISBLANK", "(value)", "Information", "TRUE if the value is empty."),
        new("ISNUMBER", "(value)", "Information", "TRUE if the value is a number."),
        new("ISTEXT", "(value)", "Information", "TRUE if the value is text."),
        new("ISNONTEXT", "(value)", "Information", "TRUE if the value is not text."),
        new("ISLOGICAL", "(value)", "Information", "TRUE if the value is a logical value."),
        new("ISERROR", "(value)", "Information", "TRUE if the value is any error."),
        new("ISERR", "(value)", "Information", "TRUE if the value is an error other than #N/A."),
        new("ISNA", "(value)", "Information", "TRUE if the value is #N/A."),
        new("ISEVEN", "(number)", "Information", "TRUE if the number is even."),
        new("ISODD", "(number)", "Information", "TRUE if the number is odd."),
        new("ISFORMULA", "(reference)", "Information", "TRUE if the cell contains a formula."),
        new("ISREF", "(value)", "Information", "TRUE if the value is a reference."),
        new("NA", "()", "Information", "Returns the #N/A error value."),
        new("N", "(value)", "Information", "Converts a value to a number."),
        new("TYPE", "(value)", "Information", "Returns a number indicating the data type."),
        new("ERROR.TYPE", "(error_val)", "Information", "Returns a number matching an error type."),

        // ── Financial ────────────────────────────────────────────────────────────
        new("PMT", "(rate, nper, pv, [fv], [type])", "Financial", "Returns the periodic payment for a loan."),
        new("FV", "(rate, nper, pmt, [pv], [type])", "Financial", "Returns the future value of an investment."),
        new("PV", "(rate, nper, pmt, [fv], [type])", "Financial", "Returns the present value of an investment."),
        new("NPER", "(rate, pmt, pv, [fv], [type])", "Financial", "Returns the number of periods for a loan."),
        new("NPV", "(rate, value1, [value2], …)", "Financial", "Net present value from a discount rate."),
        new("SLN", "(cost, salvage, life)", "Financial", "Straight-line depreciation for one period."),
        new("EFFECT", "(nominal_rate, npery)", "Financial", "Returns the effective annual interest rate.")
    ];

    /// <summary>Returns catalogue entries whose name starts with <paramref name="prefix" /> (case-insensitive).</summary>
    public static IEnumerable<FormulaFunctionInfo> Search(string? prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix))
            return All;

        var term = prefix.Trim().TrimStart('=').ToUpperInvariant();
        return All
            .Where(f => f.Name.StartsWith(term, StringComparison.OrdinalIgnoreCase))
            .OrderBy(static f => f.Name, StringComparer.OrdinalIgnoreCase);
    }
}
