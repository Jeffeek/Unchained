using System.Buffers.Binary;

namespace Unchained.Drawing.Primitives.Tests.Fonts;

/// <summary>
///     Builds minimal-but-valid TrueType/OpenType byte arrays for exercising
///     <c>TrueTypeMetrics.Parse</c> without shipping binary font assets. Only the head, OS/2, and
///     hhea tables (and just the fields the parser reads) are populated; everything else is zero.
/// </summary>
internal static class SyntheticTrueType
{
    public static byte[] Build(
        int unitsPerEm,
        int os2Version,
        int typoAscender,
        int typoDescender,
        int capHeight,
        int weightClass,
        bool includeOs2,
        bool includeHhea,
        int hheaAscender = 0,
        int hheaDescender = 0
    )
    {
        // Table sizes (generous enough for every offset the parser reads).
        const int headLen = 56;
        const int os2Len = 96;
        const int hheaLen = 36;

        var tables = new List<(string Tag, byte[] Data)>();

        // head: unitsPerEm @18, bbox xMin/yMin/xMax/yMax @36/38/40/42.
        var head = new byte[headLen];
        WriteU16(head, 18, (ushort)unitsPerEm);
        WriteS16(head, 36, -100); // xMin
        WriteS16(head, 38, -200); // yMin
        WriteS16(head, 40, 900);  // xMax
        WriteS16(head, 42, 800);  // yMax
        tables.Add(("head", head));

        if (includeOs2)
        {
            var os2 = new byte[os2Len];
            WriteU16(os2, 0, (ushort)os2Version);
            WriteU16(os2, 4, (ushort)weightClass);
            WriteS16(os2, 68, (short)typoAscender);
            WriteS16(os2, 70, (short)typoDescender);
            WriteS16(os2, 88, (short)capHeight);
            tables.Add(("OS/2", os2));
        }

        if (includeHhea)
        {
            var hhea = new byte[hheaLen];
            WriteS16(hhea, 4, (short)hheaAscender);
            WriteS16(hhea, 6, (short)hheaDescender);
            tables.Add(("hhea", hhea));
        }

        var numTables = tables.Count;
        const int dirStart = 12;
        var dataStart = dirStart + (numTables * 16);

        var total = dataStart + tables.Sum(static t => t.Data.Length);
        var font = new byte[total];

        // Offset table: sfVersion 0x00010000, numTables, searchRange/entrySelector/rangeShift (zeroed).
        BinaryPrimitives.WriteUInt32BigEndian(font.AsSpan(0), 0x00010000u);
        WriteU16(font, 4, (ushort)numTables);

        var dataOffset = dataStart;
        for (var i = 0; i < numTables; i++)
        {
            var (tag, data) = tables[i];
            var entry = dirStart + (i * 16);
            for (var c = 0; c < 4; c++)
                font[entry + c] = (byte)tag[c];
            BinaryPrimitives.WriteUInt32BigEndian(font.AsSpan(entry + 8), (uint)dataOffset);   // offset
            BinaryPrimitives.WriteUInt32BigEndian(font.AsSpan(entry + 12), (uint)data.Length); // length
            data.CopyTo(font.AsSpan(dataOffset));
            dataOffset += data.Length;
        }

        return font;
    }

    private static void WriteU16(byte[] b, int o, ushort v) =>
        BinaryPrimitives.WriteUInt16BigEndian(b.AsSpan(o), v);

    private static void WriteS16(byte[] b, int o, short v) =>
        BinaryPrimitives.WriteInt16BigEndian(b.AsSpan(o), v);
}
