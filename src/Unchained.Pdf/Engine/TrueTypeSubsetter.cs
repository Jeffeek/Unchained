using System.Buffers.Binary;

namespace Unchained.Pdf.Engine;

/// <summary>
///     Subsets an embedded TrueType font to the glyphs actually used in a PDF page,
///     reducing file size significantly for fonts with large glyph sets (CJK, symbol fonts).
///     Only modifies the <c>/glyf</c> and <c>/loca</c> tables; all other tables are kept intact
///     so font metrics and hinting are preserved. Unused glyph data is zeroed out.
///     Glyph 0 (.notdef) is always retained (required by the TrueType spec).
/// </summary>
internal static class TrueTypeSubsetter
{
    /// <summary>
    ///     Subsets the given TrueType font to include only the glyphs in <paramref name="usedGlyphIds" />,
    ///     plus glyph 0 (.notdef) and any composite-glyph components.
    ///     Returns the original bytes unchanged when subsetting fails or is not beneficial.
    /// </summary>
    internal static byte[] Subset(byte[] fontBytes, IReadOnlySet<int> usedGlyphIds)
    {
        if (fontBytes.Length < 12 || usedGlyphIds.Count == 0)
            return fontBytes;

        try
        {
            return SubsetCore(fontBytes, usedGlyphIds);
        }
        // ReSharper disable once EmptyGeneralCatchClause
        catch
        {
            return fontBytes; // on any parse failure, return original font intact
        }
    }

    private static byte[] SubsetCore(byte[] b, IEnumerable<int> usedGlyphIds)
    {
        var numTables = ReadU16(b, 4);

        // Build table directory index: tag → (checkSum, offset, length).
        var tables = new Dictionary<string, (uint CheckSum, int Offset, int Length)>();
        for (var i = 0; i < numTables; i++)
        {
            var e = 12 + (i * 16);
            if (e + 16 > b.Length)
                break;

            var tag = ReadTag(b, e);
            var cs = ReadU32(b, e + 4);
            var offset = (int)ReadU32(b, e + 8);
            var len = (int)ReadU32(b, e + 12);
            tables[tag] = (cs, offset, len);
        }

        // Require the essential subsetting tables.
        if (!tables.ContainsKey("head") || !tables.ContainsKey("loca") ||
            !tables.ContainsKey("glyf") || !tables.ContainsKey("maxp"))
            return b; // not a standard TrueType — skip subsetting

        // Read head: unitsPerEm, indexToLocFormat (0=short, 1=long).
        var headOff = tables["head"].Offset;
        var indexToLocFormat = ReadS16(b, headOff + 50);

        // Read maxp: numGlyphs.
        var maxpOff = tables["maxp"].Offset;
        var numGlyphs = ReadU16(b, maxpOff + 4);
        if (numGlyphs == 0) return b;

        // Read loca: array of numGlyphs+1 offsets into glyf.
        var locaOff = tables["loca"].Offset;
        var glyfOff = tables["glyf"].Offset;

        // Build full set of glyphs to keep (include .notdef + resolve composite components).
        var keepGlyphs = new HashSet<int> { 0 }; // always keep .notdef
        keepGlyphs.UnionWith(usedGlyphIds.Where(static g => g >= 0));

        // Resolve composite glyph components (TrueType composites reference other glyphs).
        var toExpand = new Queue<int>(keepGlyphs.Where(static g => g > 0));
        while (toExpand.Count > 0)
        {
            var gid = toExpand.Dequeue();
            if (gid >= numGlyphs)
                continue;

            var (gStart, gLen) = GetGlyphRange(b,
                locaOff,
                glyfOff,
                indexToLocFormat,
                gid);

            if (gLen < 2)
                continue;

            var contourCount = ReadS16(b, gStart);
            if (contourCount >= 0)
                continue; // simple glyph — no components

            // Composite glyph: parse component records.
            var pos = gStart + 10; // skip header
            while (pos + 4 <= gStart + gLen)
            {
                var flags = ReadU16(b, pos);
                var compGlyph = (int)ReadU16(b, pos + 2);
                if (keepGlyphs.Add(compGlyph))
                    toExpand.Enqueue(compGlyph);
                // Advance past this component record.
                pos += 4;                              // flags + glyphIndex
                pos += (flags & 1) != 0 ? 4 : 2;       // ARG_1_AND_2_ARE_WORDS
                if ((flags & 8) != 0) pos += 2;        // WE_HAVE_A_SCALE
                else if ((flags & 64) != 0) pos += 4;  // WE_HAVE_AN_X_AND_Y_SCALE
                else if ((flags & 128) != 0) pos += 8; // WE_HAVE_A_TWO_BY_TWO
                if ((flags & 32) == 0) break;          // MORE_COMPONENTS not set
            }
        }

        // If we're keeping all glyphs, subsetting has no benefit.
        if (keepGlyphs.Count >= numGlyphs) return b;

        // Rebuild glyf and loca tables with only kept glyphs.
        var newGlyfBytes = new List<byte>();
        var newLocaOffsets = new int[numGlyphs + 1];

        for (var gid = 0; gid < numGlyphs; gid++)
        {
            newLocaOffsets[gid] = newGlyfBytes.Count;
            if (!keepGlyphs.Contains(gid))
                continue;

            var (gStart, gLen) = GetGlyphRange(b,
                locaOff,
                glyfOff,
                indexToLocFormat,
                gid);

            if (gLen <= 0)
                continue;

            newGlyfBytes.AddRange(b.AsSpan(gStart, gLen).ToArray());
            // Pad to 4-byte boundary (TrueType requires 4-byte glyph alignment).
            while (newGlyfBytes.Count % 4 != 0) newGlyfBytes.Add(0);
            // Unused glyphs: loca[gid] == loca[gid+1] (zero-length entry)
        }

        newLocaOffsets[numGlyphs] = newGlyfBytes.Count;

        var newGlyf = newGlyfBytes.ToArray();

        // Build new loca table.
        byte[] newLoca;
        if (indexToLocFormat == 0)
        {
            // Short format: offsets are /2 (must fit in ushort).
            // Fall back to long format if the new glyf is too large.
            if (newGlyf.Length / 2 > 65535)
                indexToLocFormat = 1;
        }

        if (indexToLocFormat == 0)
        {
            newLoca = new byte[(numGlyphs + 1) * 2];
            for (var i = 0; i <= numGlyphs; i++)
                WriteU16(newLoca, i * 2, (ushort)(newLocaOffsets[i] / 2));
        }
        else
        {
            newLoca = new byte[(numGlyphs + 1) * 4];
            for (var i = 0; i <= numGlyphs; i++)
                WriteU32(newLoca, i * 4, (uint)newLocaOffsets[i]);
        }

        // Rebuild the font file with updated glyf and loca tables.
        // Also update head.indexToLocFormat if we switched from short to long.
        return RebuildFont(b,
            tables,
            indexToLocFormat,
            newGlyf,
            newLoca);
    }

    // Rebuilds the TrueType font with the updated glyf and loca tables.
    private static byte[] RebuildFont(
        byte[] orig,
        Dictionary<string, (uint CheckSum, int Offset, int Length)> tables,
        short indexToLocFormat,
        byte[] newGlyf,
        byte[] newLoca
    )
    {
        // Collect all tables, replacing glyf and loca.
        var tableOrder = tables.Keys.OrderBy(static t => t, StringComparer.Ordinal).ToList();

        // Calculate new offsets (all aligned to 4 bytes).
        // Offset table (12 bytes) + table records (16 bytes each) = header size.
        var headerSize = 12 + (tableOrder.Count * 16);
        var currentOffset = headerSize;
        var newOffsets = new Dictionary<string, int>();
        var newLengths = new Dictionary<string, int>();

        foreach (var tag in tableOrder)
        {
            newOffsets[tag] = currentOffset;
            var len = tag switch
            {
                "glyf" => newGlyf.Length,
                "loca" => newLoca.Length,
                _ => tables[tag].Length
            };
            newLengths[tag] = len;
            currentOffset += len;
            // Pad to 4-byte boundary.
            if (currentOffset % 4 != 0) currentOffset += 4 - (currentOffset % 4);
        }

        var totalSize = currentOffset;
        var result = new byte[totalSize];

        // Write offset table (sfVersion from original).
        Array.Copy(orig, 0, result, 0, 4); // sfVersion
        WriteU16(result, 4, (ushort)tableOrder.Count);
        // searchRange, entrySelector, rangeShift (not critical for reading, but write them).
        var n = tableOrder.Count;
        var sr = 1;
        while (sr * 2 <= n) sr *= 2;
        WriteU16(result, 6, (ushort)(sr * 16));
        WriteU16(result, 8, (ushort)(int)Math.Log2(sr));
        WriteU16(result, 10, (ushort)((n - sr) * 16));

        // Write table records.
        for (var i = 0; i < tableOrder.Count; i++)
        {
            var tag = tableOrder[i];
            var entry = 12 + (i * 16);
            WriteTag(result, entry, tag);
            WriteU32(result, entry + 4, ComputeCheckSum(tag, tables, newGlyf, newLoca));
            WriteU32(result, entry + 8, (uint)newOffsets[tag]);
            WriteU32(result, entry + 12, (uint)newLengths[tag]);
        }

        // Write table data.
        foreach (var tag in tableOrder)
        {
            var off = newOffsets[tag];
            switch (tag)
            {
                case "glyf":
                    Array.Copy(newGlyf, 0, result, off, newGlyf.Length);
                break;
                case "loca":
                    Array.Copy(newLoca, 0, result, off, newLoca.Length);
                break;
                default:
                {
                    var src = tables[tag];
                    var len = Math.Min(src.Length, orig.Length - src.Offset);
                    Array.Copy(orig, src.Offset, result, off, len);
                    break;
                }
            }
        }

        // Update head.indexToLocFormat in the new font.
        var newHeadOff = newOffsets["head"];
        WriteS16(result, newHeadOff + 50, indexToLocFormat);

        // Zero out head.checkSumAdjustment (offset 8 in head table) — must be recalculated.
        WriteU32(result, newHeadOff + 8, 0);

        return result;
    }

    private static uint ComputeCheckSum(
        string tag,
        IReadOnlyDictionary<string, (uint CheckSum, int Offset, int Length)> tables,
        byte[] newGlyf,
        byte[] newLoca
    ) =>
        tag switch
        {
            // For glyf and loca use the new data; for others reuse the original checksum.
            "glyf" => TableCheckSum(newGlyf),
            "loca" => TableCheckSum(newLoca),
            _ => tables[tag].CheckSum
        };

    private static uint TableCheckSum(byte[] data)
    {
        uint sum = 0;
        var i = 0;
        while (i + 3 < data.Length)
        {
            sum += ((uint)data[i] << 24) | ((uint)data[i + 1] << 16) |
                   ((uint)data[i + 2] << 8) | data[i + 3];
            i += 4;
        }

        // Handle trailing bytes.
        if (i >= data.Length)
            return sum;

        uint last = 0;
        for (var j = 0; j < data.Length - i; j++)
            last |= (uint)data[i + j] << (24 - (j * 8));
        sum += last;

        return sum;
    }

    private static (int Start, int Length) GetGlyphRange(
        byte[] b,
        int locaOff,
        int glyfOff,
        short indexToLocFormat,
        int gid
    )
    {
        int start, end;
        if (indexToLocFormat == 0)
        {
            start = glyfOff + (ReadU16(b, locaOff + (gid * 2)) * 2);
            end = glyfOff + (ReadU16(b, locaOff + ((gid + 1) * 2)) * 2);
        }
        else
        {
            start = glyfOff + (int)ReadU32(b, locaOff + (gid * 4));
            end = glyfOff + (int)ReadU32(b, locaOff + ((gid + 1) * 4));
        }

        var len = Math.Max(0, end - start);
        // Sanity check against buffer bounds.
        if (start < 0 || start + len > b.Length)
            return (glyfOff, 0);

        return (start, len);
    }

    // ── Primitive read/write helpers ────────────────────────────────────────────

    private static string ReadTag(byte[] b, int o) =>
        new([.. new[] { b[o], b[o + 1], b[o + 2], b[o + 3] }.Select(static c => (char)c)]);

    private static void WriteTag(byte[] b, int o, string tag)
    {
        for (var i = 0; i < 4 && i < tag.Length; i++)
            b[o + i] = (byte)tag[i];
    }

    private static ushort ReadU16(byte[] b, int o) =>
        BinaryPrimitives.ReadUInt16BigEndian(b.AsSpan(o));

    private static short ReadS16(byte[] b, int o) =>
        BinaryPrimitives.ReadInt16BigEndian(b.AsSpan(o));

    private static uint ReadU32(byte[] b, int o) =>
        BinaryPrimitives.ReadUInt32BigEndian(b.AsSpan(o));

    private static void WriteU16(byte[] b, int o, ushort v) =>
        BinaryPrimitives.WriteUInt16BigEndian(b.AsSpan(o), v);

    private static void WriteS16(byte[] b, int o, short v) =>
        BinaryPrimitives.WriteInt16BigEndian(b.AsSpan(o), v);

    private static void WriteU32(byte[] b, int o, uint v) =>
        BinaryPrimitives.WriteUInt32BigEndian(b.AsSpan(o), v);
}
