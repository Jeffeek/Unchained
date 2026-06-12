namespace Unchained.Drawing.Constants;

/// <summary>JPEG segment marker bytes (ITU-T T.81 / ISO 10918-1).</summary>
internal static class JpegMarkers
{
    /// <summary>Prefix byte that precedes every JPEG marker.</summary>
    internal const byte MarkerPrefix = 0xFF;

    /// <summary>Start Of Image — must be the first two bytes (FF D8).</summary>
    internal const byte Soi = 0xD8;

    /// <summary>End Of Image — final two bytes (FF D9).</summary>
    internal const byte Eoi = 0xD9;

    /// <summary>Start Of Frame 0 — baseline sequential DCT.</summary>
    internal const byte Sof0 = 0xC0;

    /// <summary>Start Of Frame 1 — extended sequential DCT; treated as baseline.</summary>
    internal const byte Sof1 = 0xC1;

    /// <summary>Start Of Frame 2 — progressive DCT; unsupported by the BCL-only decoder.</summary>
    internal const byte Sof2 = 0xC2;

    /// <summary>Define Huffman Table.</summary>
    internal const byte Dht = 0xC4;

    /// <summary>Define Quantization Table.</summary>
    internal const byte Dqt = 0xDB;

    /// <summary>Define Restart Interval.</summary>
    internal const byte Dri = 0xDD;

    /// <summary>Start Of Scan.</summary>
    internal const byte Sos = 0xDA;

    /// <summary>APP0 / JFIF application extension marker.</summary>
    internal const byte App0Jfif = 0xE0;

    /// <summary>First restart marker RST0 — lower bound of the RST0–RST7 range.</summary>
    internal const byte RstFirst = 0xD0;

    /// <summary>Last restart marker RST7 — upper bound of the RST0–RST7 range.</summary>
    internal const byte RstLast = 0xD7;

    /// <summary>
    ///     Byte-stuffed zero: a JpegMarkers.ByteStuff byte emitted after a JpegMarkers.MarkerPrefix data byte in entropy-coded
    ///     scan
    ///     data so readers do not mistake it for a marker prefix.
    /// </summary>
    internal const byte ByteStuff = 0x00;

    /// <summary>Zero Run Length AC symbol — represents 16 consecutive AC zeros (ZRL).</summary>
    internal const byte ZrlAcCode = 0xF0;

    /// <summary>Low-nibble mask used to extract table ID or component sub-field from packed bytes.</summary>
    internal const byte NibbleMask = 0x0F;

    /// <summary>
    ///     Sampling-factor byte for 1×1 chroma subsampling — used in SOF and SOS segments
    ///     to indicate no subsampling on a component.
    /// </summary>
    internal const byte SamplingFactor1X1 = 0x11;

    // JPEG zig-zag scan order
    internal static readonly int[] ZigZag =
    [
        0, 1, 8, 16, 9, 2, 3, 10, 17, 24, 32, 25, 18, 11, 4, 5,
        12, 19, 26, 33, 40, 48, 41, 34, 27, 20, 13, 6, 7, 14, 21, 28,
        35, 42, 49, 56, 57, 50, 43, 36, 29, 22, 15, 23, 30, 37, 44, 51,
        58, 59, 52, 45, 38, 31, 39, 46, 53, 60, 61, 54, 47, 55, 62, 63
    ];
}
