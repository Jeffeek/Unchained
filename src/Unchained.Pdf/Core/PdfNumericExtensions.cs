namespace Unchained.Pdf.Core;

/// <summary>
///     Extension methods for parsing numeric PDF primitives.
/// </summary>
internal static class PdfNumericExtensions
{
    /// <summary>
    ///     Parses a raw byte span as a signed integer (handles optional leading '+'/'-' sign).
    /// </summary>
    public static long ParseRawInteger(ReadOnlySpan<byte> span)
    {
        var negative = span[0] == (byte)'-';
        var start = (negative || span[0] == (byte)'+') ? 1 : 0;
        long value = 0;
        for (var i = start; i < span.Length; i++)
            value = (value * 10) + (span[i] - '0');

        return negative ? -value : value;
    }
}
