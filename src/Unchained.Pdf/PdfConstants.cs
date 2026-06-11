namespace Unchained.Pdf;

/// <summary>PDF specification constants (ISO 32000-1).</summary>
internal static class PdfConstants
{
    // ── Glyph space (ISO 32000-1 §9.2.4) ────────────────────────────────────

    /// <summary>
    /// PDF glyph-space units per em: one em = 1000 glyph units.
    /// All glyph advance widths and bounding-box values are expressed in this unit.
    /// Divide by this value to convert to text-space units.
    /// </summary>
    internal const double GlyphSpaceUnitsPerEm = 1000.0;

    /// <summary>
    /// Horizontal scale (Tz operator) is a percentage; divide by this to obtain a ratio.
    /// Default Tz = 100 → ratio = 1.0 (ISO 32000-1 §9.3.4).
    /// </summary>
    internal const double HorizontalScaleDivisor = 100.0;

    // ── Cross-reference table (ISO 32000-1 §7.5.4) ──────────────────────────

    /// <summary>
    /// Generation number used for the mandatory free head entry (object 0) in an xref table.
    /// The free list head always carries generation 65535.
    /// </summary>
    internal const int XrefFreeGenerationNumber = 65535;

    /// <summary>
    /// Number of bytes scanned from the file start or end when searching for the
    /// linearisation marker (/Linearized) or the xref/%%EOF trailer.
    /// </summary>
    internal const int XrefScanWindowBytes = 1024;

    // ── UTF-16 encoding (ISO 32000-1 §7.9.2.2) ──────────────────────────────

    /// <summary>First byte of the UTF-16 big-endian byte-order mark (0xFE 0xFF).</summary>
    internal const byte Utf16BeBomByte0 = 0xFE;

    /// <summary>Second byte of the UTF-16 big-endian byte-order mark.</summary>
    internal const byte Utf16BeBomByte1 = 0xFF;

    // ── Printable ASCII range ────────────────────────────────────────────────

    /// <summary>First printable ASCII character (space, 0x20).</summary>
    internal const byte PrintableAsciiMin = 0x20;

    /// <summary>Last printable ASCII character (tilde ~, 0x7E).</summary>
    internal const byte PrintableAsciiMax = 0x7E;
}
