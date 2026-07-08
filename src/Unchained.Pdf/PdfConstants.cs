namespace Unchained.Pdf;

/// <summary>PDF specification constants (ISO 32000-1).</summary>
internal static class PdfConstants
{
    // ── Cross-reference table (ISO 32000-1 §7.5.4) ──────────────────────────

    /// <summary>
    ///     Generation number used for the mandatory free head entry (object 0) in an xref table.
    ///     The free list head always carries generation 65535.
    /// </summary>
    internal const int XrefFreeGenerationNumber = 65535;

    /// <summary>
    ///     Number of bytes scanned from the file start or end when searching for the
    ///     linearisation marker (/Linearized) or the xref/%%EOF trailer.
    /// </summary>
    internal const int XrefScanWindowBytes = 1024;

    // ── UTF-16 encoding (ISO 32000-1 §7.9.2.2) ──────────────────────────────

    /// <summary>First byte of the UTF-16 big-endian byte-order mark (0xFE 0xFF).</summary>
    internal const byte Utf16BeBomByte0 = 0xFE;

    /// <summary>Second byte of the UTF-16 big-endian byte-order mark.</summary>
    internal const byte Utf16BeBomByte1 = 0xFF;

    // ── Resource resolution limits ────────────────────────────────────────────

    /// <summary>Maximum depth when recursing into nested Form XObjects — prevents infinite loops on malformed documents.</summary>
    internal const int MaxFormXObjectDepth = 10;

    // ── Device color spaces (ISO 32000-1 §8.6.2) ────────────────────────────

    /// <summary>Device gray color space name.</summary>
    internal const string DeviceGray = "DeviceGray";

    /// <summary>Device RGB color space name.</summary>
    internal const string DeviceRgb = "DeviceRGB";

    /// <summary>Device CMYK color space name.</summary>
    internal const string DeviceCmyk = "DeviceCMYK";

    /// <summary>Calibrated gray color space name.</summary>
    internal const string CalGray = "CalGray";

    /// <summary>Calibrated RGB color space name.</summary>
    internal const string CalRgb = "CalRGB";

    /// <summary>ICC-based color space name.</summary>
    internal const string IccBased = "ICCBased";

    /// <summary>Separation color space name.</summary>
    internal const string Separation = "Separation";

    /// <summary>DeviceN color space name.</summary>
    internal const string DeviceN = "DeviceN";

    /// <summary>Indexed color space name.</summary>
    internal const string Indexed = "Indexed";

    /// <summary>Lab color space name.</summary>
    internal const string Lab = "Lab";

    /// <summary>Form XObject subtype value.</summary>
    internal const string XObjectForm = "Form";

    // ── Standard 14 fonts (ISO 32000-1 §9.6.2) ────────────────────────────────

    /// <summary>Helvetica (sans-serif) Standard 14 font name.</summary>
    internal const string FontHelvetica = "Helvetica";

    /// <summary>Helvetica-Bold Standard 14 font name.</summary>
    internal const string FontHelveticaBold = "Helvetica-Bold";

    // ── Printable ASCII range ────────────────────────────────────────────────

    /// <summary>First printable ASCII character (space, 0x20).</summary>
    internal const byte PrintableAsciiMin = 0x20;

    /// <summary>Last printable ASCII character (tilde ~, 0x7E).</summary>
    internal const byte PrintableAsciiMax = 0x7E;

    // ── PDF/A compliance ─────────────────────────────────────────────────────

    /// <summary>PDF/A-1b conformance marker in XMP metadata.</summary>
    internal const string PdfAIdentifier = "pdfuaid";
}
