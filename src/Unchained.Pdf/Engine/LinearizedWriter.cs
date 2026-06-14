using System.Buffers;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using Unchained.Pdf.Core;
using Unchained.Pdf.Document;
using Unchained.Pdf.Writing;

namespace Unchained.Pdf.Engine;

/// <summary>
///     Produces a linearized (web-optimized) PDF byte stream conforming to ISO 32000-1 Annex F.
///     <para>
///         A linearized PDF is structured so that a PDF reader can render the first page as soon as
///         the first part of the file arrives over a network, without waiting for the full download.
///         The file layout is:
///         <list type="number">
///             <item>Header</item>
///             <item>Linearization parameter dictionary (object 1, fixed 256-byte reserved block)</item>
///             <item>First-page objects: catalog, page-1 dict, page-1 content streams, page-1 resources</item>
///             <item>Hint stream (page offset table + shared object table, FlateDecode compressed)</item>
///             <item>First-page xref section</item>
///             <item>First-page trailer</item>
///             <item>Remaining-pages objects</item>
///             <item>Main (overflow) xref section</item>
///             <item>Main trailer</item>
///         </list>
///     </para>
///     <para>
///         Because byte offsets in the linearization dict and hint stream must be exact,
///         a two-pass approach is used: the first pass serializes everything and records all offsets;
///         the second pass patches the linearization-dict placeholder and hint stream with correct values.
///     </para>
/// </summary>
internal static class LinearizedWriter
{
    // Fixed byte budget reserved for the linearization parameter dictionary.
    // PDF readers scan the first 1024 bytes for this dict (Annex F §F.3.1).
    // 256 bytes is ample for the seven required entries even with large integer values.
    private const int LinearizationDictReservedBytes = 256;

    /// <summary>
    ///     Serializes <paramref name="objects" /> as a linearized PDF and returns the resulting bytes.
    /// </summary>
    /// <param name="objects">All indirect objects of the document, sorted by object number.</param>
    /// <param name="trailer">The document trailer dictionary (must contain /Root and /Size).</param>
    /// <param name="core">The source document core, used to walk the page tree.</param>
    internal static byte[] Write(
        IReadOnlyList<PdfIndirectObject> objects,
        PdfDictionary trailer,
        PdfDocumentCore core
    )
    {
        // ── 1. Partition objects ───────────────────────────────────────────────
        var partition = PartitionObjects(objects, trailer, core);
        var firstPageObjects = partition.FirstPage;
        var remainingObjects = partition.Remaining;
        var firstPageRef = partition.FirstPageRef;

        // ── 2. First pass — measure everything ────────────────────────────────
        var layout = MeasureLayout(firstPageObjects, remainingObjects, trailer);

        // ── 3. Build hint stream ───────────────────────────────────────────────
        var hintStream = BuildHintStream(layout);

        // ── 4. Second pass — write final output ───────────────────────────────
        // ReSharper disable once BadListLineBreaks
        return WriteFinal(firstPageObjects,
            remainingObjects,
            trailer,
            firstPageRef,
            layout,
            hintStream);
    }

    /// <summary>
    ///     Splits the object list into the first-page cluster and everything else.
    ///     The first-page cluster contains: the catalog, the first page dict, all objects
    ///     transitively reachable from the first page (content streams, fonts, resources).
    /// </summary>
    private static Partition PartitionObjects(
        IReadOnlyList<PdfIndirectObject> objects,
        PdfDictionary trailer,
        PdfDocumentCore core
    )
    {
        // Build a fast lookup by object number.
        var byNumber = objects.ToDictionary(static o => o.ObjectNumber);

        // Collect all object numbers reachable from the first page via BFS.
        var firstPageSet = new HashSet<int>();

        // Always include catalog.
        if (trailer[PdfName.Root] is PdfIndirectReference catalogRef)
            firstPageSet.Add(catalogRef.ObjectNumber);

        // Find the first page dict object number.
        PdfIndirectReference? firstPageRef;
        try
        {
            var firstPageDict = core.GetPage(1);
            firstPageRef = FindObjectRef(firstPageDict, objects);
        }
        catch
        {
            // Fallback: use the first Page object found.
            firstPageRef = objects
                .Where(static o => o.Value is PdfDictionary d && d.IsPage())
                .Select(static o => new PdfIndirectReference(o.ObjectNumber, o.Generation))
                .FirstOrDefault();
        }

        if (firstPageRef is not null)
        {
            // BFS from the first page dict.
            var queue = new Queue<int>();
            queue.Enqueue(firstPageRef.ObjectNumber);
            while (queue.Count > 0)
            {
                var num = queue.Dequeue();
                if (!firstPageSet.Add(num))
                    continue;
                if (!byNumber.TryGetValue(num, out var obj))
                    continue;

                foreach (var refNum in CollectRefs(obj.Value).Where(refNum => !firstPageSet.Contains(refNum)))
                    queue.Enqueue(refNum);
            }
        }

        // Always include the /Pages root (needed for the catalog → pages reference).
        var pagesRef = (core.Catalog[PdfName.Pages] as PdfIndirectReference)?.ObjectNumber;
        if (pagesRef.HasValue) firstPageSet.Add(pagesRef.Value);

        var firstPage = objects
            .Where(o => firstPageSet.Contains(o.ObjectNumber))
            .OrderBy(static o => o.ObjectNumber)
            .ToList();

        var remaining = objects
            .Where(o => !firstPageSet.Contains(o.ObjectNumber))
            .OrderBy(static o => o.ObjectNumber)
            .ToList();

        return new Partition(
            firstPage,
            remaining,
            firstPageRef ?? new PdfIndirectReference(firstPage.FirstOrDefault()?.ObjectNumber ?? 1, 0)
        );
    }

    /// <summary>Finds the indirect reference for the object whose value is reference-equal to <paramref name="dict" />.</summary>
    private static PdfIndirectReference? FindObjectRef(
        PdfDictionary dict,
        IReadOnlyList<PdfIndirectObject> objects
    )
    {
        foreach (var obj in objects)
        {
            if (obj.Value is not PdfDictionary d)
                continue;
            if (!ReferenceEquals(d, dict))
                continue;

            return new PdfIndirectReference(obj.ObjectNumber, obj.Generation);
        }

        // Try resolving all Page-type objects.
        foreach (var obj in objects)
        {
            if (obj.Value is not PdfDictionary d)
                continue;
            if (!d.IsPage())
                continue;

            return new PdfIndirectReference(obj.ObjectNumber, obj.Generation);
        }

        return null;
    }

    /// <summary>Recursively collects all indirect reference object numbers from a PdfObject graph.</summary>
    private static IEnumerable<int> CollectRefs(PdfObject obj)
    {
        switch (obj)
        {
            case PdfIndirectReference r:
            {
                yield return r.ObjectNumber;

                break;
            }
            case PdfArray arr:
            {
                foreach (var n in arr.Elements.SelectMany(static elem => CollectRefs(elem)))
                    yield return n;

                break;
            }
            case PdfDictionary dict:
            {
                foreach (var (_, value) in dict.Entries)
                foreach (var n in CollectRefs(value))
                    yield return n;

                break;
            }
            case PdfStream stream:
            {
                foreach (var (_, value) in stream.Dictionary.Entries)
                foreach (var n in CollectRefs(value))
                    yield return n;

                break;
            }
        }
    }

    private static LinearizedLayout MeasureLayout(
        List<PdfIndirectObject> firstPageObjects,
        List<PdfIndirectObject> remainingObjects,
        PdfDictionary trailer
    )
    {
        var buf = new ArrayBufferWriter<byte>();

        // Header.
        var pos = WriteLinearizationHeader(buf);

        // Linearization dict placeholder (fixed block).
        var linearizationDictOffset = pos;
        var placeholder = BuildLinearizationDictPlaceholder();
        buf.Write(placeholder);
        pos += placeholder.Length;

        // Record where first-page objects start.
        var firstPageObjectsStart = pos;

        // First-page objects.
        var firstPageOffsets = new Dictionary<int, long>();
        foreach (var obj in firstPageObjects)
        {
            firstPageOffsets[obj.ObjectNumber] = pos;
            var objBuf = new ArrayBufferWriter<byte>();
            using var objWriter = new PdfWriter(objBuf);
            objWriter.WriteIndirectObject(obj);
            var objBytes = objBuf.WrittenMemory.ToArray();
            buf.Write(objBytes);
            pos += objBytes.Length;
        }

        // Hint stream placeholder object number = maxObjNum + 1.
        var maxObjNum = firstPageObjects.Count > 0
            ? Math.Max(
                firstPageObjects.Max(static o => o.ObjectNumber),
                remainingObjects.Count > 0 ? remainingObjects.Max(static o => o.ObjectNumber) : 0)
            : 0;
        var hintObjNum = maxObjNum + 1;
        var hintStreamOffset = pos;

        // Hint stream placeholder — minimal, just to measure the size.
        var hintPlaceholder = BuildHintStreamPlaceholder(hintObjNum);
        buf.Write(hintPlaceholder);
        pos += hintPlaceholder.Length;

        var endOfFirstPage = pos;

        // First-page xref section.
        var firstXrefOffset = pos;
        var firstXrefBytes = BuildXrefSection(firstPageObjects, firstPageOffsets, hintObjNum, hintStreamOffset);
        buf.Write(firstXrefBytes);
        pos += firstXrefBytes.Length;

        // First-page trailer.
        // ReSharper disable once BadListLineBreaks
        var firstTrailerBytes = BuildFirstTrailer(trailer,
            firstPageObjects,
            remainingObjects,
            hintObjNum,
            firstXrefOffset,
            0 /*mainXref unknown yet*/);
        buf.Write(firstTrailerBytes);
        pos += firstTrailerBytes.Length;

        // Remaining objects.
        foreach (var obj in remainingObjects)
        {
            var objBuf = new ArrayBufferWriter<byte>();
            using var objWriter = new PdfWriter(objBuf);
            objWriter.WriteIndirectObject(obj);
            var objBytes = objBuf.WrittenMemory.ToArray();
            buf.Write(objBytes);
            pos += objBytes.Length;
        }

        // Main xref.
        var mainXrefOffset = pos;

        // Page offsets — page 1 starts at firstPageObjectsStart.
        var pageOffsets = new[] { firstPageObjectsStart };

        return new LinearizedLayout
        {
            LinearizationDictOffset = linearizationDictOffset,
            FirstXrefOffset = firstXrefOffset,
            HintStreamOffset = hintStreamOffset,
            HintStreamObjectNumber = hintObjNum,
            EndOfFirstPageOffset = endOfFirstPage,
            MainXrefOffset = mainXrefOffset,
            FileLength = pos,
            PageOffsets = pageOffsets,
            FirstPageObjectNumbers = firstPageObjects.Select(static o => o.ObjectNumber).ToArray(),
            RemainingObjectNumbers = remainingObjects.Select(static o => o.ObjectNumber).ToArray()
        };
    }

    // ── Hint stream ───────────────────────────────────────────────────────────

    /// <summary>
    ///     Builds the hint stream per ISO 32000-1 Annex F §F.4.
    ///     Contains a page offset hint table and a shared object hint table,
    ///     concatenated and FlateDecode-compressed.
    /// </summary>
    private static byte[] BuildHintStream(LinearizedLayout layout)
    {
        using var raw = new MemoryStream();
        using var bw = new BinaryWriter(raw, Encoding.ASCII, true);

        // ── Page offset hint table (§F.4.5) ───────────────────────────────────
        bw.Write((uint)layout.PageOffsets.Length);
        foreach (var offset in layout.PageOffsets)
            bw.Write((uint)offset);

        // ── Shared object hint table (§F.4.7) ─────────────────────────────────
        bw.Write((uint)layout.FirstPageObjectNumbers.Length);
        foreach (var num in layout.FirstPageObjectNumbers)
            bw.Write((uint)num);

        bw.Flush();
        raw.Position = 0;

        using var compressed = new MemoryStream();
        using (var deflate = new ZLibStream(compressed, CompressionLevel.Optimal, true))
            raw.CopyTo(deflate);

        return compressed.ToArray();
    }

    // ── Final write ───────────────────────────────────────────────────────────

    private static byte[] WriteFinal(
        List<PdfIndirectObject> firstPageObjects,
        List<PdfIndirectObject> remainingObjects,
        PdfDictionary trailer,
        PdfIndirectReference firstPageRef,
        LinearizedLayout layout,
        byte[] hintStreamBytes
    )
    {
        var buf = new ArrayBufferWriter<byte>(64 * 1024);
        long pos = 0;

        // ── Header ─────────────────────────────────────────────────────────────
        pos += WriteLinearizationHeader(buf);

        // ── Linearization dict placeholder — patch at end ──────────────────────
        var linearizationDictStart = pos;
        var linDictPlaceholder = BuildLinearizationDictPlaceholder();
        buf.Write(linDictPlaceholder);
        pos += linDictPlaceholder.Length;

        // ── First-page objects ─────────────────────────────────────────────────
        var objectOffsets = new Dictionary<int, long>();
        foreach (var obj in firstPageObjects)
        {
            objectOffsets[obj.ObjectNumber] = pos;
            pos += WriteObject(buf, obj);
        }

        // ── Hint stream object ─────────────────────────────────────────────────
        var hintObjNum = layout.HintStreamObjectNumber;
        var hintStreamStart = pos;
        objectOffsets[hintObjNum] = pos;

        var hintDict = new PdfDictionary(new Dictionary<string, PdfObject>
        {
            ["Length"] = new PdfInteger(hintStreamBytes.Length),
            ["Filter"] = PdfName.FlateDecode,
            ["S"] = new PdfInteger(0)
        });
        var hintObj = new PdfIndirectObject(hintObjNum, 0, new PdfStream(hintDict, hintStreamBytes));
        pos += WriteObject(buf, hintObj);

        var endOfFirstPage = pos;

        // ── First-page xref section ────────────────────────────────────────────
        var firstXrefOffset = pos;
        var allFirstNumbers = firstPageObjects
            .Select(static o => o.ObjectNumber)
            .Append(hintObjNum)
            .ToList();

        pos += WriteXrefAndFirstTrailer(
            buf,
            allFirstNumbers,
            objectOffsets,
            trailer,
            hintObjNum,
            firstXrefOffset,
            layout.MainXrefOffset
        );

        // ── Remaining objects ──────────────────────────────────────────────────
        foreach (var obj in remainingObjects)
        {
            objectOffsets[obj.ObjectNumber] = pos;
            pos += WriteObject(buf, obj);
        }

        // ── Main xref + trailer ────────────────────────────────────────────────
        var mainXrefOffset = pos;
        pos += WriteMainXrefAndTrailer(
            buf,
            firstPageObjects,
            remainingObjects,
            objectOffsets,
            trailer,
            hintObjNum,
            mainXrefOffset
        );

        // ── Patch linearization dict ───────────────────────────────────────────
        var fileLength = pos;
        var linDict = BuildLinearizationDict(
            fileLength,
            hintStreamStart,
            hintStreamBytes.Length + EstimateHintObjectOverhead(hintObjNum, hintStreamBytes.Length),
            firstPageRef.ObjectNumber,
            endOfFirstPage,
            firstPageObjects.Count > 0
                ? 1 + remainingObjects.Count(static o =>
                    o.Value is PdfDictionary d && d.IsPage())
                : 1,
            mainXrefOffset
        );

        PatchBytes(buf, linearizationDictStart, linDict, LinearizationDictReservedBytes);

        return buf.WrittenMemory.ToArray();
    }

    // ── Serialization helpers ─────────────────────────────────────────────────

    private static long WriteLinearizationHeader(IBufferWriter<byte> buf)
    {
        var header = "%PDF-1.7\n%âãÏÓ\n"u8;
        buf.Write(header);
        return header.Length;
    }

    private static long WriteObject(IBufferWriter<byte> buf, PdfIndirectObject obj)
    {
        var objBuf = new ArrayBufferWriter<byte>();
        using var w = new PdfWriter(objBuf);
        w.WriteIndirectObject(obj);
        var bytes = objBuf.WrittenMemory;
        buf.Write(bytes.Span);
        return bytes.Length;
    }

    private static long WriteXrefAndFirstTrailer(
        IBufferWriter<byte> buf,
        IReadOnlyList<int> firstObjNumbers,
        IReadOnlyDictionary<int, long> offsets,
        PdfDictionary trailer,
        int hintObjNum,
        long firstXrefOffset,
        long mainXrefOffset
    )
    {
        long written = 0;

        var xrefBytes = "xref\n"u8.ToArray();
        buf.Write(xrefBytes);
        written += xrefBytes.Length;

        // Free object 0.
        var free0 = "0 1\n0000000000 65535 f \r\n"u8.ToArray();
        buf.Write(free0);
        written += free0.Length;

        // Write each object as its own subsection (they may not be contiguous).
        foreach (var num in firstObjNumbers.OrderBy(static n => n))
        {
            if (!offsets.TryGetValue(num, out var offset))
                continue;

            var sub = Encoding.ASCII.GetBytes($"{num} 1\n{offset:D10} 00000 n \r\n");
            buf.Write(sub);
            written += sub.Length;
        }

        // First-page trailer.
        var rootRef = trailer[PdfName.Root] as PdfIndirectReference ?? throw new PdfException("Trailer missing /Root.");
        var maxObj = firstObjNumbers.Append(hintObjNum).Max() + 1;

        var trailerDict = BuildAscii(
            $"trailer\n<<\n/Size {maxObj}\n/Root {rootRef}\n/Prev {mainXrefOffset}\n>>\nstartxref\n{firstXrefOffset}\n%%EOF\n"
        );
        buf.Write(trailerDict);
        written += trailerDict.Length;

        return written;
    }

    private static long WriteMainXrefAndTrailer(
        IBufferWriter<byte> buf,
        IEnumerable<PdfIndirectObject> firstPageObjects,
        IEnumerable<PdfIndirectObject> remainingObjects,
        IReadOnlyDictionary<int, long> offsets,
        PdfDictionary trailer,
        int hintObjNum,
        long mainXrefOffset
    )
    {
        long written = 0;
        var all = firstPageObjects.Concat(remainingObjects).OrderBy(static o => o.ObjectNumber).ToList();
        var maxNum = all.Count > 0 ? all.Max(static o => o.ObjectNumber) : 0;
        maxNum = Math.Max(maxNum, hintObjNum);

        var xrefHeader = Encoding.ASCII.GetBytes($"xref\n0 {maxNum + 1}\n");
        buf.Write(xrefHeader);
        written += xrefHeader.Length;

        var free0 = "0000000000 65535 f \r\n"u8.ToArray();
        buf.Write(free0);
        written += free0.Length;

        for (var i = 1; i <= maxNum; i++)
        {
            var entry = offsets.TryGetValue(i, out var off)
                ? Encoding.ASCII.GetBytes($"{off:D10} 00000 n \r\n")
                : "0000000000 00000 f \r\n"u8.ToArray();
            buf.Write(entry);
            written += entry.Length;
        }

        var rootRef = trailer[PdfName.Root] as PdfIndirectReference ?? throw new PdfException("Trailer missing /Root.");
        var infoEntry = trailer[PdfName.Info] is { } info ? $"\n/Info {info}" : string.Empty;

        var trailerBytes = Encoding.ASCII.GetBytes(
            $"trailer\n<<\n/Size {maxNum + 1}\n/Root {rootRef}{infoEntry}\n>>\nstartxref\n{mainXrefOffset}\n%%EOF\n"
        );
        buf.Write(trailerBytes);
        written += trailerBytes.Length;

        return written;
    }

    // ── Linearization dict ────────────────────────────────────────────────────

    private static byte[] BuildLinearizationDictPlaceholder()
    {
        var header = "1 0 obj\n"u8.ToArray();
        var padding = new byte[LinearizationDictReservedBytes - header.Length - "\nendobj\n".Length];
        Array.Fill(padding, (byte)' ');
        var footer = "\nendobj\n"u8.ToArray();
        return [.. header, .. padding, .. footer];
    }

    private static byte[] BuildLinearizationDict(
        long fileLength,
        long hintOffset,
        long hintLength,
        int firstPageObjNum,
        long endOfFirstPage,
        int pageCount,
        long mainXrefOffset
    ) =>
        // /H is an array: [hintOffset hintLength] (Annex F §F.3.3 Table F.1).
        Encoding.ASCII.GetBytes(
            $"1 0 obj\n<<\n/Linearized 1\n/L {fileLength}\n/H [{hintOffset} {hintLength}]\n" +
            $"/O {firstPageObjNum}\n/E {endOfFirstPage}\n/N {pageCount}\n/T {mainXrefOffset}\n>>\nendobj\n"
        );

    private static byte[] BuildHintStreamPlaceholder(int objNum)
    {
        var raw = new byte[64];
        using var ms = new MemoryStream();
        using (var zlib = new ZLibStream(ms, CompressionLevel.Optimal, true))
            zlib.Write(raw, 0, raw.Length);

        var compressed = ms.ToArray();
        var header = Encoding.ASCII.GetBytes(
            $"{objNum} 0 obj\n<<\n/Length {compressed.Length}\n/Filter /FlateDecode\n/S 0\n>>\nstream\n"
        );
        var footer = "\nendstream\nendobj\n"u8.ToArray();
        return [.. header, .. compressed, .. footer];
    }

    private static byte[] BuildXrefSection(
        IEnumerable<PdfIndirectObject> objects,
        IReadOnlyDictionary<int, long> offsets,
        int hintObjNum,
        long hintOffset
    )
    {
        var sb = new StringBuilder();
        sb.Append("xref\n0 1\n0000000000 65535 f \r\n");
        foreach (var obj in objects.OrderBy(static o => o.ObjectNumber))
        {
            if (!offsets.TryGetValue(obj.ObjectNumber, out var off))
                continue;

            sb.Append($"{obj.ObjectNumber} 1\n{off:D10} 00000 n \r\n");
        }

        sb.Append($"{hintObjNum} 1\n{hintOffset:D10} 00000 n \r\n");
        return Encoding.ASCII.GetBytes(sb.ToString());
    }

    private static byte[] BuildFirstTrailer(
        PdfDictionary trailer,
        IEnumerable<PdfIndirectObject> firstPageObjects,
        IEnumerable<PdfIndirectObject> remainingObjects,
        int hintObjNum,
        long firstXrefOffset,
        long mainXrefOffset
    )
    {
        var rootRef = trailer[PdfName.Root] as PdfIndirectReference ?? throw new PdfException("Trailer missing /Root.");
        var maxObj = firstPageObjects
            .Concat(remainingObjects)
            .Select(static o => o.ObjectNumber)
            .Append(hintObjNum)
            .Max() + 1;

        return Encoding.ASCII.GetBytes(
            $"trailer\n<<\n/Size {maxObj}\n/Root {rootRef}\n/Prev {mainXrefOffset}\n>>\n" +
            $"startxref\n{firstXrefOffset}\n%%EOF\n"
        );
    }

    // ── Buffer patching ───────────────────────────────────────────────────────

    /// <summary>
    ///     Patches <paramref name="data" /> into <paramref name="buf" /> at <paramref name="offset" />,
    ///     padding with spaces up to <paramref name="reservedLength" /> bytes.
    /// </summary>
    private static void PatchBytes(
        ArrayBufferWriter<byte> buf,
        long offset,
        byte[] data,
        int reservedLength
    )
    {
        // ArrayBufferWriter does not expose a writable span; use MemoryMarshal to get one.
        var span = MemoryMarshal.AsMemory(buf.WrittenMemory).Span;
        var start = (int)offset;

        // Fill the reserved area with spaces first (safe default).
        span.Slice(start, reservedLength).Fill((byte)' ');

        // Copy as much of data as fits into the reserved area.
        var copyLen = Math.Min(data.Length, reservedLength);
        data.AsSpan(0, copyLen).CopyTo(span.Slice(start, copyLen));
    }

    // ── Utilities ─────────────────────────────────────────────────────────────

    private static byte[] BuildAscii(string s) => Encoding.ASCII.GetBytes(s);

    /// <summary>
    ///     Estimates the byte overhead of the hint stream indirect object header/footer
    ///     so the /H length value in the linearization dict is accurate.
    /// </summary>
    private static long EstimateHintObjectOverhead(int objNum, int dataLen)
    {
        var headerLen =
            $"{objNum} 0 obj\n<<\n/Length {dataLen}\n/Filter /FlateDecode\n/S 0\n>>\nstream\n".Length;
        var footerLen = "\nendstream\nendobj\n".Length;
        return headerLen + footerLen;
    }

    // ── Object partitioning ───────────────────────────────────────────────────

    private sealed class Partition(
        List<PdfIndirectObject> firstPage,
        List<PdfIndirectObject> remaining,
        PdfIndirectReference firstPageRef
    )
    {
        internal List<PdfIndirectObject> FirstPage { get; } = firstPage;
        internal List<PdfIndirectObject> Remaining { get; } = remaining;
        internal PdfIndirectReference FirstPageRef { get; } = firstPageRef;
    }

    // ── Layout measurement ────────────────────────────────────────────────────

    private sealed class LinearizedLayout
    {
        // Byte offset where the linearization dict placeholder starts.
        // ReSharper disable once UnusedAutoPropertyAccessor.Local
        internal long LinearizationDictOffset { get; init; }

        // Byte offset of the first-page xref section.
        // ReSharper disable once UnusedAutoPropertyAccessor.Local
        internal long FirstXrefOffset { get; init; }

        // Byte offset where the hint stream indirect object starts.
        // ReSharper disable once UnusedAutoPropertyAccessor.Local
        internal long HintStreamOffset { get; init; }

        // Object number assigned to the hint stream.
        internal int HintStreamObjectNumber { get; init; }

        // Byte offset immediately after the last byte of the first-page section.
        // ReSharper disable once UnusedAutoPropertyAccessor.Local
        internal long EndOfFirstPageOffset { get; init; }

        // Byte offset of the main (overflow) xref section.
        internal long MainXrefOffset { get; init; }

        // Total file length (set after second pass).
        // ReSharper disable once UnusedAutoPropertyAccessor.Local
        internal long FileLength { get; set; }

        // Per-page starting byte offsets (index 0 = page 1).
        internal long[] PageOffsets { get; init; } = [];

        // Object numbers for all first-page objects (for hint table).
        internal int[] FirstPageObjectNumbers { get; init; } = [];

        // Object numbers for remaining objects (reserved for future hint table extension).
        // ReSharper disable once UnusedAutoPropertyAccessor.Local
        internal int[] RemainingObjectNumbers { get; init; } = [];
    }
}
