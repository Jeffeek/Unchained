namespace Unchained.Xlsx.Formatting;

/// <summary>
///     Converts between Excel date/time serial numbers and <see cref="DateTime" />, honouring both
///     the 1900 (default) and 1904 (legacy Mac) date systems.
/// </summary>
/// <remarks>
///     The 1900 system carries the historical Lotus 1-2-3 bug: it treats 29 February 1900 (serial 60)
///     as a real date, so every serial from 61 onward is one greater than the true day count. The
///     1904 system has no such bug. See <c>research-notes.md</c> Difficulty 7.
/// </remarks>
internal static class DateTimeSerializer
{
    // Day after which the 1900 phantom-leap-day correction applies.
    private const int PhantomLeapSerial = 60;

    // 1900 base is 31 Dec 1899 so that serial 1 maps to 1 Jan 1900.
    private static readonly DateTime Base1900 = new(1899, 12, 31);
    private static readonly DateTime Base1904 = new(1904, 1, 1);

    /// <summary>The largest valid serial (corresponds to 31 Dec 9999 in the 1900 system).</summary>
    public const double MaxSerial = 2_958_465.99999999;

    /// <summary>
    ///     Converts a serial number to a <see cref="DateTime" />, or returns <see langword="null" />
    ///     for the phantom 29 Feb 1900 (serial 60) or an out-of-range value.
    /// </summary>
    public static DateTime? ToDateTime(double serial, bool date1904)
    {
        if (date1904)
            return serial < 0 ? null : Base1904.AddDays(serial);

        return serial switch
        {
            // Serials in [60, 61) map to the phantom 29 Feb 1900 that never existed.
            < 1 or > MaxSerial or >= PhantomLeapSerial and < PhantomLeapSerial + 1 => null,
            _ => serial <= PhantomLeapSerial ? Base1900.AddDays(serial) : Base1900.AddDays(serial - 1)
        };
    }

    /// <summary>Converts a <see cref="DateTime" /> to an Excel date/time serial number.</summary>
    public static double ToSerial(DateTime date, bool date1904)
    {
        if (date1904)
            return (date - Base1904).TotalDays;

        var serial = (date - Base1900).TotalDays;

        // Account for the phantom 29 Feb 1900 for dates on or after 1 Mar 1900.
        if (date >= new DateTime(1900, 3, 1))
            serial += 1;

        return serial;
    }
}
