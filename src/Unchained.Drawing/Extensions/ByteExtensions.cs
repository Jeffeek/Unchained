namespace Unchained.Drawing.Extensions;

internal static class ByteExtensions
{
    /// <summary>
    /// Returns <see langword="true"/> for the six ASCII whitespace bytes:
    /// NUL (0x00), HT (0x09), LF (0x0A), FF (0x0C), CR (0x0D), SP (0x20).
    /// </summary>
    internal static bool IsWhitespace(this byte b) => b is 0x00 or 0x09 or 0x0A or 0x0C or 0x0D or 0x20;
}
