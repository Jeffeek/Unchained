namespace Unchained.Drawing.Constants;

/// <summary>PNG format constants (ISO 15948 / PNG spec).</summary>
internal static class PngConstants
{
    /// <summary>8-byte PNG file signature (PNG spec §5.2).</summary>
    internal static readonly byte[] Signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>
    /// Reversed CRC-32 polynomial used for PNG chunk CRC computation.
    /// Equivalent to the standard polynomial 0x04C11DB7 reflected bit-by-bit.
    /// </summary>
    internal const uint Crc32Polynomial = 0xEDB88320u;

    /// <summary>CRC-32 initial value and final XOR mask (all bits set).</summary>
    internal const uint Crc32Init = 0xFFFFFFFFu;

    internal static uint[] CtcTable = BuildCrcTable();

    private static uint[] BuildCrcTable()
    {
        var t = new uint[256];
        for (var n = 0u; n < 256; n++)
        {
            var c = n;
            for (var k = 0; k < 8; k++)
                c = (c & 1) != 0 ? Crc32Polynomial ^ (c >> 1) : c >> 1;

            t[n] = c;
        }

        return t;
    }

    internal const string IHDR = "IHDR";
    internal const string IDAT = "IDAT";
    internal const string PLTE = "PLTE";
    // ReSharper disable once InconsistentNaming
    internal const string TRNS = "tRNS";
    internal const string IEND = "IEND";
}
