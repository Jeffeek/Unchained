using System.Globalization;

namespace Unchained.Drawing.Primitives.Extensions;

internal static class ByteExtensions
{
    extension(byte b)
    {
        /// <summary>
        ///     Returns <see langword="true" /> for the six ASCII whitespace bytes:
        ///     NUL (0x00), HT (0x09), LF (0x0A), FF (0x0C), CR (0x0D), SP (0x20).
        /// </summary>
        internal bool IsWhitespace() => b is 0x00 or 0x09 or 0x0A or 0x0C or 0x0D or 0x20;

        /// <summary>
        ///     Extracts a single bit, MSB-first, from <paramref name="b" /> at the given bit
        ///     position within the byte (bit 0 = most-significant). Used for 1-bit-per-component
        ///     image sample unpacking.
        /// </summary>
        internal int BitMsbFirst(int bitPosition) => (b >> (7 - (bitPosition & 7))) & 1;

        /// <summary>Formats a byte as a two-digit uppercase hexadecimal string (e.g. <c>0F</c>).</summary>
        internal string ToHex2() => b.ToString("X2", CultureInfo.InvariantCulture);
    }
}
