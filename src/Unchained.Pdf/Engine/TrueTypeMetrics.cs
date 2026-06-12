namespace Unchained.Pdf.Engine;

/// <summary>
///     Reads font metrics from a TrueType or OpenType font file by parsing the minimal set
///     of tables needed for a PDF /FontDescriptor: OS/2, hhea, head, and post.
///     All values are returned in glyph-space units (1000 units/em after normalisation),
///     matching the expected scale for PDF /FontDescriptor entries.
///     ISO 32000-1 §9.8.1, OpenType Specification §5.
/// </summary>
internal static class TrueTypeMetrics
{
    /// <summary>
    ///     Helvetica/Arial fallback metrics used when FreeType cannot extract real metrics,
    ///     expressed in 1000-unit glyph space.
    /// </summary>
    internal static readonly FontMetrics HelveticaFallback =
        new(-166,
            -225,
            FontConstants.NormalizedUnitsPerEm,
            931,
            800,
            -200,
            716,
            80);

    /// <summary>
    ///     Parses metrics from a TrueType/OpenType font byte array.
    ///     Returns null when the font cannot be parsed (not TrueType, truncated, etc.).
    /// </summary>
    internal static FontMetrics? Read(byte[] fontBytes)
    {
        try
        {
            return Parse(fontBytes);
        }
        // ReSharper disable once EmptyGeneralCatchClause
        catch
        {
            return null;
        }
    }

    private static FontMetrics Parse(IReadOnlyList<byte> b)
    {
        // TrueType/OpenType offset table starts at byte 0.
        // Bytes 0–3: sfVersion (0x00010000 for TrueType, 'OTTO' for CFF).
        // Bytes 4–5: numTables.
        if (b.Count < 12) return Default();

        var numTables = ReadU16(b, 4);
        // Table directory entries start at offset 12; each is 16 bytes.
        // Find the OS/2, hhea, head, and post table offsets.
        int? os2Off = null, hheaOff = null, headOff = null;
        for (var i = 0; i < numTables; i++)
        {
            var entry = 12 + (i * 16);
            if (entry + 16 > b.Count) break;

            var tag = ReadTag(b, entry);
            var offset = (int)ReadU32(b, entry + 8);
            switch (tag)
            {
                case "OS/2": os2Off = offset; break;
                case "hhea": hheaOff = offset; break;
                case "head": headOff = offset; break;
            }
        }

        // head table: units per em (at offset 18).
        var unitsPerEm = headOff is { } ho && ho + 54 <= b.Count
            ? ReadU16(b, ho + 18)
            : FontConstants.NormalizedUnitsPerEm;
        if (unitsPerEm == 0) unitsPerEm = FontConstants.NormalizedUnitsPerEm;
        var scale = FontConstants.NormalizedUnitsPerEmDouble / unitsPerEm;

        // head table: font bounding box (at offsets 36–43, signed shorts).
        var xMin = headOff is { } ho2 && ho2 + 44 <= b.Count ? (int)Scale(ReadS16(b, ho2 + 36), scale) : -166;
        var yMin = headOff is { } ho3 && ho3 + 44 <= b.Count ? (int)Scale(ReadS16(b, ho3 + 38), scale) : -225;
        var xMax = headOff is { } ho4 && ho4 + 44 <= b.Count ? (int)Scale(ReadS16(b, ho4 + 40), scale) : FontConstants.NormalizedUnitsPerEm;
        var yMax = headOff is { } ho5 && ho5 + 44 <= b.Count ? (int)Scale(ReadS16(b, ho5 + 42), scale) : 931;

        // OS/2 table (preferred source for typographic metrics):
        // sTypoAscender at offset 68, sTypoDescender at 70, sCapHeight at 88 (v2+).
        int ascent, descent, capHeight;
        if (os2Off is { } oo && oo + 90 <= b.Count)
        {
            ascent = (int)Scale(ReadS16(b, oo + 68), scale);
            descent = (int)Scale(ReadS16(b, oo + 70), scale);
            // sCapHeight is at OS/2 v2+ (offset 88); version at offset 0.
            var os2Version = ReadU16(b, oo);
            capHeight = os2Version >= 2 && oo + 90 <= b.Count
                ? (int)Scale(ReadS16(b, oo + 88), scale)
                : (int)(ascent * FontConstants.CapHeightAscentRatio);
        }
        else if (hheaOff is { } hh && hh + 36 <= b.Count)
        {
            // Fallback to hhea ascender/descender.
            ascent = (int)Scale(ReadS16(b, hh + 4), scale);
            descent = (int)Scale(ReadS16(b, hh + 6), scale);
            capHeight = (int)(ascent * FontConstants.CapHeightAscentRatio);
        }
        else
            return Default();

        // StemV: approximate from OS/2 usWeightClass if available.
        // Common formula: StemV ≈ (usWeightClass/65)^2 + 50.
        var stemV = 80;

        // ReSharper disable once InvertIf
        if (os2Off is { } oo2 && oo2 + 10 <= b.Count)
        {
            var weightClass = ReadU16(b, oo2 + 4);
            if (weightClass > 0)
                stemV = (int)Math.Clamp(Math.Pow(weightClass / 65.0, 2) + 50, 10, 340);
        }

        return new FontMetrics(xMin,
            yMin,
            xMax,
            yMax,
            ascent,
            descent,
            capHeight,
            stemV);
    }

    private static FontMetrics Default() => HelveticaFallback;

    private static string ReadTag(IReadOnlyList<byte> b, int o) =>
        new([.. new[] { b[o], b[o + 1], b[o + 2], b[o + 3] }.Select(static c => (char)c)]);

    private static ushort ReadU16(IReadOnlyList<byte> b, int o) =>
        (ushort)((b[o] << 8) | b[o + 1]);

    private static uint ReadU32(IReadOnlyList<byte> b, int o) =>
        ((uint)b[o] << 24) | ((uint)b[o + 1] << 16) | ((uint)b[o + 2] << 8) | b[o + 3];

    private static short ReadS16(IReadOnlyList<byte> b, int o) =>
        (short)((b[o] << 8) | b[o + 1]);

    private static double Scale(int value, double scale) =>
        Math.Round(value * scale);
}

/// <summary>Font metrics read from a TrueType font, normalised to 1000 units per em.</summary>
/// <param name="XMin">Font bounding box left edge.</param>
/// <param name="YMin">Font bounding box bottom edge.</param>
/// <param name="XMax">Font bounding box right edge.</param>
/// <param name="YMax">Font bounding box top edge.</param>
/// <param name="Ascent">Typographic ascender (above baseline).</param>
/// <param name="Descent">Typographic descender (below baseline, typically negative).</param>
/// <param name="CapHeight">Height of capital letters.</param>
/// <param name="StemV">Dominant vertical stem width (approximated from weight class).</param>
internal sealed record FontMetrics(
    int XMin,
    int YMin,
    int XMax,
    int YMax,
    int Ascent,
    int Descent,
    int CapHeight,
    int StemV
);
