using System.Buffers.Binary;

namespace Unchained.Drawing.Fonts;

/// <summary>
///     Reads font metrics from a TrueType or OpenType font file by parsing the minimal set
///     of tables needed for a PDF /FontDescriptor: OS/2, hhea, head, and post.
///     All values are returned in glyph-space units (1000 units/em after normalisation),
///     matching the expected scale for PDF /FontDescriptor entries.
///     ISO 32000-1 §9.8.1, OpenType Specification §5.
/// </summary>
internal static class TrueTypeMetrics
{
    /// <summary>Target units-per-em after normalisation; widths and bounding boxes use a 1000-unit em square.</summary>
    private const int NormalizedUnitsPerEm = 1000;

    /// <summary><see cref="NormalizedUnitsPerEm" /> as <see langword="double" /> for scale calculations.</summary>
    private const double NormalizedUnitsPerEmDouble = 1000.0;

    /// <summary>Estimated cap-height as a fraction of the ascender when the OS/2 table is absent (≈72%).</summary>
    private const double CapHeightAscentRatio = 0.72;

    /// <summary>
    ///     Helvetica/Arial fallback metrics used when real metrics cannot be extracted,
    ///     expressed in 1000-unit glyph space.
    /// </summary>
    internal static readonly FontMetrics HelveticaFallback =
        new(-166,
            -225,
            NormalizedUnitsPerEm,
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

    private static FontMetrics Parse(byte[] b)
    {
        // TrueType/OpenType offset table starts at byte 0.
        // Bytes 0–3: sfVersion (0x00010000 for TrueType, 'OTTO' for CFF).
        // Bytes 4–5: numTables.
        if (b.Length < 12) return HelveticaFallback;

        var numTables = ReadU16(b, 4);
        // Table directory entries start at offset 12; each is 16 bytes.
        // Find the OS/2, hhea, head, and post table offsets.
        int? os2Off = null, hheaOff = null, headOff = null;
        for (var i = 0; i < numTables; i++)
        {
            var entry = 12 + (i * 16);
            if (entry + 16 > b.Length) break;

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
        var unitsPerEm = headOff is { } ho && ho + 54 <= b.Length
            ? ReadU16(b, ho + 18)
            : NormalizedUnitsPerEm;
        if (unitsPerEm == 0) unitsPerEm = NormalizedUnitsPerEm;
        var scale = NormalizedUnitsPerEmDouble / unitsPerEm;

        // head table: font bounding box (at offsets 36–43, signed shorts).
        var xMin = headOff is { } ho2 && ho2 + 44 <= b.Length ? (int)Scale(ReadS16(b, ho2 + 36), scale) : -166;
        var yMin = headOff is { } ho3 && ho3 + 44 <= b.Length ? (int)Scale(ReadS16(b, ho3 + 38), scale) : -225;
        var xMax = headOff is { } ho4 && ho4 + 44 <= b.Length ? (int)Scale(ReadS16(b, ho4 + 40), scale) : NormalizedUnitsPerEm;
        var yMax = headOff is { } ho5 && ho5 + 44 <= b.Length ? (int)Scale(ReadS16(b, ho5 + 42), scale) : 931;

        // OS/2 table (preferred source for typographic metrics):
        // sTypoAscender at offset 68, sTypoDescender at 70, sCapHeight at 88 (v2+).
        int ascent, descent, capHeight;
        if (os2Off is { } oo && oo + 90 <= b.Length)
        {
            ascent = (int)Scale(ReadS16(b, oo + 68), scale);
            descent = (int)Scale(ReadS16(b, oo + 70), scale);
            // sCapHeight is at OS/2 v2+ (offset 88); version at offset 0.
            var os2Version = ReadU16(b, oo);
            capHeight = os2Version >= 2 && oo + 90 <= b.Length
                ? (int)Scale(ReadS16(b, oo + 88), scale)
                : (int)(ascent * CapHeightAscentRatio);
        }
        else if (hheaOff is { } hh && hh + 36 <= b.Length)
        {
            // Fallback to hhea ascender/descender.
            ascent = (int)Scale(ReadS16(b, hh + 4), scale);
            descent = (int)Scale(ReadS16(b, hh + 6), scale);
            capHeight = (int)(ascent * CapHeightAscentRatio);
        }
        else
            return HelveticaFallback;

        // StemV: approximate from OS/2 usWeightClass if available.
        // Common formula: StemV ≈ (usWeightClass/65)^2 + 50.
        var stemV = 80;

        // ReSharper disable once InvertIf
        if (os2Off is { } oo2 && oo2 + 10 <= b.Length)
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

    private static string ReadTag(IReadOnlyList<byte> b, int o) =>
        new([.. new[] { b[o], b[o + 1], b[o + 2], b[o + 3] }.Select(static c => (char)c)]);

    private static ushort ReadU16(byte[] b, int o) =>
        BinaryPrimitives.ReadUInt16BigEndian(b.AsSpan(o));

    private static uint ReadU32(byte[] b, int o) =>
        BinaryPrimitives.ReadUInt32BigEndian(b.AsSpan(o));

    private static short ReadS16(byte[] b, int o) =>
        BinaryPrimitives.ReadInt16BigEndian(b.AsSpan(o));

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
