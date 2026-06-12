using System.Globalization;

namespace Unchained.Drawing.Extensions;

internal static class ByteExtensions
{
    /// <summary>
    ///     Returns <see langword="true" /> for the six ASCII whitespace bytes:
    ///     NUL (0x00), HT (0x09), LF (0x0A), FF (0x0C), CR (0x0D), SP (0x20).
    /// </summary>
    internal static bool IsWhitespace(this byte b) => b is 0x00 or 0x09 or 0x0A or 0x0C or 0x0D or 0x20;

    /// <summary>
    ///     Extracts a single bit, MSB-first, from <paramref name="value" /> at the given bit
    ///     position within the byte (bit 0 = most-significant). Used for 1-bit-per-component
    ///     image sample unpacking.
    /// </summary>
    internal static int BitMsbFirst(this byte value, int bitPosition) => (value >> (7 - (bitPosition & 7))) & 1;

    /// <summary>Formats a byte as a two-digit uppercase hexadecimal string (e.g. <c>0F</c>).</summary>
    internal static string ToHex2(this byte value) => value.ToString("X2", CultureInfo.InvariantCulture);
}
