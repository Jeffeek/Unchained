using System.IO.Compression;
using System.Text;

namespace Unchained.Pdf.Tests.Helpers;

/// <summary>
/// Produces minimal but spec-compliant PDF byte arrays for use as test fixtures.
/// Uses explicit <c>\n</c> line endings for cross-platform byte-offset consistency.
/// </summary>
internal static class PdfFixtures
{
    public static byte[] SinglePage() => Build(pageCount: 1);

    public static byte[] MultiPage(int count) => Build(pageCount: count);

    /// <summary>
    /// Generates a single-page PDF whose first page has one /Text annotation.
    /// </summary>
    public static byte[] WithAnnotation(string contents = "Note") =>
        BuildWithAnnotation(contents);

    /// <summary>
    /// Generates a two-page PDF with a flat /Outlines tree pointing at the pages.
    /// </summary>
    public static byte[] WithOutlines(params (string title, int page)[] bookmarks) =>
        BuildWithOutlines(bookmarks);

    /// <summary>
    /// Generates a single-page PDF with one AcroForm text field.
    /// </summary>
    public static byte[] WithAcroForm(string fieldName = "TextField", string fieldValue = "") =>
        BuildWithAcroForm(fieldName, fieldValue);

    public static byte[] WithInfo(string title, string author) =>
        Build(pageCount: 1, title: title, author: author);

    /// <summary>
    /// Generates a PDF 1.5-style document that uses a compressed /XRef stream
    /// instead of a traditional xref table. Tests the cross-reference stream
    /// parser (ISO 32000-1 §7.5.8).
    /// W = [1, 4, 2]: 1-byte type, 4-byte offset, 2-byte generation.
    /// </summary>
    public static byte[] WithCompressedXref(int pageCount = 1) =>
        BuildWithXrefStream(pageCount);

    /// <summary>
    /// Generates a single-page PDF whose page content stream contains a simple
    /// text block. Used to test content stream parsing end-to-end.
    /// </summary>
    public static byte[] WithTextContent(string text = "Hello Unchained") =>
        BuildWithContent($"BT /F1 12 Tf 100 700 Td ({EscapeString(text)}) Tj ET");

    private static string EscapeString(string s) =>
        s.Replace("\\", @"\\").Replace("(", "\\(").Replace(")", "\\)");

    private static byte[] BuildWithContent(string contentStream)
    {
        var sb = new StringBuilder();
        var offsets = new List<int>();

        Ln(sb, "%PDF-1.7");
        Ln(sb, "%\xE2\xE3\xCF\xD3");

        // Object 1 — Catalog
        offsets.Add(Len(sb));
        Ln(sb, "1 0 obj");
        Ln(sb, "<< /Type /Catalog /Pages 2 0 R >>");
        Ln(sb, "endobj");

        // Object 2 — Pages
        offsets.Add(Len(sb));
        Ln(sb, "2 0 obj");
        Ln(sb, "<< /Type /Pages /Kids [4 0 R] /Count 1 >>");
        Ln(sb, "endobj");

        // Object 3 — content stream
        var streamBytes = Encoding.Latin1.GetBytes(contentStream);
        offsets.Add(Len(sb));
        Ln(sb, "3 0 obj");
        Ln(sb, $"<< /Length {streamBytes.Length} >>");
        sb.Append("stream\n");
        sb.Append(contentStream);
        Ln(sb, "\nendstream");
        Ln(sb, "endobj");

        // Object 4 — Page
        offsets.Add(Len(sb));
        Ln(sb, "4 0 obj");
        Ln(sb, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Contents 3 0 R >>");
        Ln(sb, "endobj");

        // Update Pages object to reference correct page (already done above)
        // xref
        var xrefOffset = Len(sb);
        Ln(sb, "xref");
        Ln(sb, "0 5");
        Ln(sb, "0000000000 65535 f ");
        foreach (var o in offsets)
            Ln(sb, $"{o:D10} 00000 n ");

        Ln(sb, "trailer");
        Ln(sb, "<< /Size 5 /Root 1 0 R >>");
        Ln(sb, "startxref");
        Ln(sb, xrefOffset.ToString());
        sb.Append("%%EOF");

        return Encoding.Latin1.GetBytes(sb.ToString());

        static int Len(StringBuilder b) => Encoding.Latin1.GetByteCount(b.ToString());

        static void Ln(StringBuilder b, string line) => b.Append(line).Append('\n');
    }

    private static byte[] Build(int pageCount, string? title = null, string? author = null)
    {
        var sb = new StringBuilder();
        var offsets = new List<int>();

        Ln(sb, "%PDF-1.7");
        Ln(sb, "%\xE2\xE3\xCF\xD3");

        // Object 1 — Catalog
        offsets.Add(ByteLen(sb));
        Ln(sb, "1 0 obj");
        Ln(sb, "<< /Type /Catalog /Pages 2 0 R >>");
        Ln(sb, "endobj");

        // Object 2 — Pages
        offsets.Add(ByteLen(sb));
        var kids = string.Join(" ", Enumerable.Range(3, pageCount).Select(static n => $"{n} 0 R"));
        Ln(sb, "2 0 obj");
        Ln(sb, $"<< /Type /Pages /Kids [{kids}] /Count {pageCount} >>");
        Ln(sb, "endobj");

        // Objects 3..N — Page nodes
        for (var i = 0; i < pageCount; i++)
        {
            offsets.Add(ByteLen(sb));
            Ln(sb, $"{3 + i} 0 obj");
            Ln(sb, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] >>");
            Ln(sb, "endobj");
        }

        // Optional Info dictionary
        var infoRef = string.Empty;
        if (title is not null || author is not null)
        {
            var infoObjNum = 3 + pageCount;
            offsets.Add(ByteLen(sb));
            Ln(sb, $"{infoObjNum} 0 obj");
            var titleEntry = title is not null ? $" /Title ({title})" : string.Empty;
            var authorEntry = author is not null ? $" /Author ({author})" : string.Empty;
            Ln(sb, $"<<{titleEntry}{authorEntry} >>");
            Ln(sb, "endobj");
            infoRef = $" /Info {infoObjNum} 0 R";
        }

        var totalObjects = 3 + pageCount + (infoRef.Length > 0 ? 1 : 0);

        // xref
        var xrefOffset = ByteLen(sb);
        Ln(sb, "xref");
        Ln(sb, $"0 {totalObjects}");
        Ln(sb, "0000000000 65535 f ");
        foreach (var offset in offsets)
            Ln(sb, $"{offset:D10} 00000 n ");

        // Trailer
        Ln(sb, "trailer");
        Ln(sb, $"<< /Size {totalObjects} /Root 1 0 R{infoRef} >>");
        Ln(sb, "startxref");
        Ln(sb, xrefOffset.ToString());
        sb.Append("%%EOF");

        return Encoding.Latin1.GetBytes(sb.ToString());

        static void Ln(StringBuilder b, string line) => b.Append(line).Append('\n');
    }

    private static int ByteLen(StringBuilder sb) => Encoding.Latin1.GetByteCount(sb.ToString());

    private static void AppendWithLineEnding(StringBuilder b, string line) => b.Append(line).Append('\n');

    // ── PDF with compressed /XRef stream ─────────────────────────────────────

    private static byte[] BuildWithXrefStream(int pageCount)
    {
        // Phase 1: write the body objects and record their offsets.
        var body = new StringBuilder();
        var offsets = new List<int>(); // offsets[i] = byte offset of object (i+1)

        Ln(body, "%PDF-1.5");
        Ln(body, "%\xE2\xE3\xCF\xD3");

        // Object 1 — Catalog
        offsets.Add(Len(body));
        Ln(body, "1 0 obj");
        Ln(body, "<< /Type /Catalog /Pages 2 0 R >>");
        Ln(body, "endobj");

        // Object 2 — Pages
        offsets.Add(Len(body));
        var kids = string.Join(" ", Enumerable.Range(3, pageCount).Select(static n => $"{n} 0 R"));
        Ln(body, "2 0 obj");
        Ln(body, $"<< /Type /Pages /Kids [{kids}] /Count {pageCount} >>");
        Ln(body, "endobj");

        // Objects 3..N — Page nodes
        for (var i = 0; i < pageCount; i++)
        {
            offsets.Add(Len(body));
            Ln(body, $"{3 + i} 0 obj");
            Ln(body, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] >>");
            Ln(body, "endobj");
        }

        // Phase 2: build the binary xref stream data.
        // ReSharper disable once GrammarMistakeInComment
        // Object numbering: 0=free, 1..N=body objects, N+1=the xref stream itself.
        var totalObjects = 3 + pageCount + 1; // +1 for the xref stream object itself
        var xrefStreamObjNum = 2 + pageCount + 1;
        var xrefStreamOffset = Len(body); // offset where the xref stream object will start

        // W = [1, 4, 2]: type(1), offset(4), generation(2)
        const int w0 = 1, w1 = 4, w2 = 2;
        const int rowSize = w0 + w1 + w2;
        var rawXref = new byte[totalObjects * rowSize];

        // Object 0: free
        WriteRow(0, 0, 0, 65535);
        // ReSharper disable once GrammarMistakeInComment
        // Objects 1..N: in-use body objects
        for (var i = 0; i < offsets.Count; i++)
            WriteRow(i + 1, 1, offsets[i], 0);
        // Xref stream object itself (object number = xrefStreamObjNum, row index = same)
        WriteRow(xrefStreamObjNum, 1, xrefStreamOffset, 0);

        // Compress the binary xref data
        var compressed = ZlibCompress(rawXref);

        // Phase 3: write the xref stream object.
        Ln(body, $"{xrefStreamObjNum} 0 obj");
        Ln(body, $"<< /Type /XRef /Size {totalObjects} /W [{w0} {w1} {w2}] /Filter /FlateDecode /Length {compressed.Length} /Root 1 0 R >>");
        body.Append("stream\n");
        var bodyBytes = Encoding.Latin1.GetBytes(body.ToString());
        var result = new byte[bodyBytes.Length + compressed.Length + "\nendstream\nendobj\nstartxref\n".Length + 20 + "%%EOF".Length];

        var pos2 = 0;
        bodyBytes.CopyTo(result, pos2);
        pos2 += bodyBytes.Length;
        compressed.CopyTo(result, pos2);
        pos2 += compressed.Length;

        var tail = $"\nendstream\nendobj\nstartxref\n{xrefStreamOffset}\n%%EOF";
        var tailBytes = Encoding.Latin1.GetBytes(tail);
        tailBytes.CopyTo(result, pos2);
        pos2 += tailBytes.Length;

        return result[..pos2];

        void WriteRow(
            int rowIndex,
            byte type,
            long offset,
            int gen
        )
        {
            var pos = rowIndex * rowSize;
            rawXref[pos] = type;
            rawXref[pos + 1] = (byte)(offset >> 24);
            rawXref[pos + 2] = (byte)(offset >> 16);
            rawXref[pos + 3] = (byte)(offset >> 8);
            rawXref[pos + 4] = (byte)offset;
            rawXref[pos + 5] = (byte)(gen >> 8);
            rawXref[pos + 6] = (byte)gen;
        }

        static int Len(StringBuilder b) => Encoding.Latin1.GetByteCount(b.ToString());

        static void Ln(StringBuilder b, string line) => b.Append(line).Append('\n');
    }

    private static byte[] BuildWithAnnotation(string contents)
    {
        var sb = new StringBuilder();
        var offsets = new List<int>();

        AppendWithLineEnding(sb, "%PDF-1.7");
        AppendWithLineEnding(sb, "%\xE2\xE3\xCF\xD3");

        // Object 1 — Catalog
        offsets.Add(ByteLen(sb));
        AppendWithLineEnding(sb, "1 0 obj");
        AppendWithLineEnding(sb, "<< /Type /Catalog /Pages 2 0 R >>");
        AppendWithLineEnding(sb, "endobj");

        // Object 2 — Pages
        offsets.Add(ByteLen(sb));
        AppendWithLineEnding(sb, "2 0 obj");
        AppendWithLineEnding(sb, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
        AppendWithLineEnding(sb, "endobj");

        // Object 3 — Page (with /Annots)
        offsets.Add(ByteLen(sb));
        AppendWithLineEnding(sb, "3 0 obj");
        AppendWithLineEnding(sb, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Annots [4 0 R] >>");
        AppendWithLineEnding(sb, "endobj");

        // Object 4 — Annotation
        var escaped = EscapeString(contents);
        offsets.Add(ByteLen(sb));
        AppendWithLineEnding(sb, "4 0 obj");
        AppendWithLineEnding(sb, $"<< /Type /Annot /Subtype /Text /Rect [50 700 100 750] /Contents ({escaped}) >>");
        AppendWithLineEnding(sb, "endobj");

        var xrefOffset = ByteLen(sb);
        AppendWithLineEnding(sb, "xref");
        AppendWithLineEnding(sb, "0 5");
        AppendWithLineEnding(sb, "0000000000 65535 f ");
        foreach (var o in offsets) AppendWithLineEnding(sb, $"{o:D10} 00000 n ");
        AppendWithLineEnding(sb, "trailer");
        AppendWithLineEnding(sb, "<< /Size 5 /Root 1 0 R >>");
        AppendWithLineEnding(sb, "startxref");
        AppendWithLineEnding(sb, xrefOffset.ToString());
        sb.Append("%%EOF");

        return Encoding.Latin1.GetBytes(sb.ToString());
    }

    private static byte[] BuildWithOutlines(IEnumerable<(string title, int page)> bookmarks)
    {
        var bms = bookmarks.ToList();
        var sb = new StringBuilder();
        const int pageCount = 2;

        AppendWithLineEnding(sb, "%PDF-1.7");
        AppendWithLineEnding(sb, "%\xE2\xE3\xCF\xD3");

        // Object 1 — Catalog (with /Outlines)
        AppendWithLineEnding(sb, "1 0 obj");
        AppendWithLineEnding(sb, "<< /Type /Catalog /Pages 2 0 R /Outlines 3 0 R >>");
        AppendWithLineEnding(sb, "endobj");

        // Object 2 — Pages
        AppendWithLineEnding(sb, "2 0 obj");
        AppendWithLineEnding(sb, "<< /Type /Pages /Kids [5 0 R 6 0 R] /Count 2 >>");
        AppendWithLineEnding(sb, "endobj");

        // Object 3 — Outlines root
        // Items start at object 4
        var itemCount = bms.Count;
        var firstRef = itemCount > 0 ? "4 0 R" : "null";
        var lastRef = itemCount > 0 ? $"{3 + itemCount} 0 R" : "null";
        AppendWithLineEnding(sb, "3 0 obj");
        AppendWithLineEnding(sb, $"<< /Type /Outlines /Count {itemCount} /First {firstRef} /Last {lastRef} >>");
        AppendWithLineEnding(sb, "endobj");

        // ReSharper disable once GrammarMistakeInComment
        // Objects 4..(3+N) — outline items
        for (var i = 0; i < bms.Count; i++)
        {
            var num = 4 + i;
            var (title, pageNum) = bms[i];
            var pageObjNum = pageNum == 1 ? 5 : 6;
            var prev = i > 0 ? $" /Prev {num - 1} 0 R" : string.Empty;
            var next = i < bms.Count - 1 ? $" /Next {num + 1} 0 R" : string.Empty;
            AppendWithLineEnding(sb, $"{num} 0 obj");
            AppendWithLineEnding(sb, $"<< /Title ({EscapeString(title)}) /Parent 3 0 R /Dest [{pageObjNum} 0 R /Fit]{prev}{next} >>");
            AppendWithLineEnding(sb, "endobj");
        }

        // Page objects
        var page1ObjNum = 4 + bms.Count;
        AppendWithLineEnding(sb, $"{page1ObjNum} 0 obj");
        AppendWithLineEnding(sb, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] >>");
        AppendWithLineEnding(sb, "endobj");

        var page2ObjNum = page1ObjNum + 1;
        AppendWithLineEnding(sb, $"{page2ObjNum} 0 obj");
        AppendWithLineEnding(sb, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] >>");
        AppendWithLineEnding(sb, "endobj");

        // Fix Pages /Kids to use correct page object numbers
        // We need to re-write object 2 at the actual page object numbers.
        // Since we built it above with [5 0 R 6 0 R] but actual numbers depend on bm count,
        // rebuild the whole thing with correct numbers.
        _ = pageCount; // avoid unused warning

        return BuildWithOutlinesFinal(bms);
    }

    private static byte[] BuildWithOutlinesFinal(IReadOnlyList<(string title, int page)> bms)
    {
        var sb = new StringBuilder();
        var offsets = new List<int>();

        // Fixed layout: catalog=1, pages=2, outlinesRoot=3, items=4..(3+N), page1=(4+N), page2=(5+N)
        var page1Obj = 4 + bms.Count;
        var page2Obj = 5 + bms.Count;
        var totalObjects = page2Obj + 1;

        AppendWithLineEnding(sb, "%PDF-1.7");
        AppendWithLineEnding(sb, "%\xE2\xE3\xCF\xD3");

        offsets.Add(ByteLen(sb));
        AppendWithLineEnding(sb, "1 0 obj");
        AppendWithLineEnding(sb, "<< /Type /Catalog /Pages 2 0 R /Outlines 3 0 R >>");
        AppendWithLineEnding(sb, "endobj");

        offsets.Add(ByteLen(sb));
        AppendWithLineEnding(sb, "2 0 obj");
        AppendWithLineEnding(sb, $"<< /Type /Pages /Kids [{page1Obj} 0 R {page2Obj} 0 R] /Count 2 >>");
        AppendWithLineEnding(sb, "endobj");

        var first = bms.Count > 0 ? "4 0 R" : "null";
        var last = bms.Count > 0 ? $"{3 + bms.Count} 0 R" : "null";
        offsets.Add(ByteLen(sb));
        AppendWithLineEnding(sb, "3 0 obj");
        AppendWithLineEnding(sb, $"<< /Type /Outlines /Count {bms.Count} /First {first} /Last {last} >>");
        AppendWithLineEnding(sb, "endobj");

        for (var i = 0; i < bms.Count; i++)
        {
            var num = 4 + i;
            var (title, pageNum) = bms[i];
            var pageObjNum = pageNum == 1 ? page1Obj : page2Obj;
            var prev = i > 0 ? $" /Prev {num - 1} 0 R" : string.Empty;
            var next = i < bms.Count - 1 ? $" /Next {num + 1} 0 R" : string.Empty;
            offsets.Add(ByteLen(sb));
            AppendWithLineEnding(sb, $"{num} 0 obj");
            AppendWithLineEnding(sb, $"<< /Title ({EscapeString(title)}) /Parent 3 0 R /Dest [{pageObjNum} 0 R /Fit]{prev}{next} >>");
            AppendWithLineEnding(sb, "endobj");
        }

        offsets.Add(ByteLen(sb));
        AppendWithLineEnding(sb, $"{page1Obj} 0 obj");
        AppendWithLineEnding(sb, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] >>");
        AppendWithLineEnding(sb, "endobj");

        offsets.Add(ByteLen(sb));
        AppendWithLineEnding(sb, $"{page2Obj} 0 obj");
        AppendWithLineEnding(sb, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] >>");
        AppendWithLineEnding(sb, "endobj");

        var xrefOffset = ByteLen(sb);
        AppendWithLineEnding(sb, "xref");
        AppendWithLineEnding(sb, $"0 {totalObjects}");
        AppendWithLineEnding(sb, "0000000000 65535 f ");
        foreach (var o in offsets) AppendWithLineEnding(sb, $"{o:D10} 00000 n ");
        AppendWithLineEnding(sb, "trailer");
        AppendWithLineEnding(sb, $"<< /Size {totalObjects} /Root 1 0 R >>");
        AppendWithLineEnding(sb, "startxref");
        AppendWithLineEnding(sb, xrefOffset.ToString());
        sb.Append("%%EOF");

        return Encoding.Latin1.GetBytes(sb.ToString());
    }

    private static byte[] BuildWithAcroForm(string fieldName, string fieldValue)
    {
        var sb = new StringBuilder();
        var offsets = new List<int>();

        AppendWithLineEnding(sb, "%PDF-1.7");
        AppendWithLineEnding(sb, "%\xE2\xE3\xCF\xD3");

        // Object 1 — Catalog (with /AcroForm)
        offsets.Add(ByteLen(sb));
        AppendWithLineEnding(sb, "1 0 obj");
        AppendWithLineEnding(sb, "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [4 0 R] >> >>");
        AppendWithLineEnding(sb, "endobj");

        // Object 2 — Pages
        offsets.Add(ByteLen(sb));
        AppendWithLineEnding(sb, "2 0 obj");
        AppendWithLineEnding(sb, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
        AppendWithLineEnding(sb, "endobj");

        // Object 3 — Page
        offsets.Add(ByteLen(sb));
        AppendWithLineEnding(sb, "3 0 obj");
        AppendWithLineEnding(sb, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Annots [4 0 R] >>");
        AppendWithLineEnding(sb, "endobj");

        // Object 4 — Text field (also the widget annotation for it)
        var escapedName = EscapeString(fieldName);
        var escapedValue = EscapeString(fieldValue);
        offsets.Add(ByteLen(sb));
        AppendWithLineEnding(sb, "4 0 obj");
        AppendWithLineEnding(sb, $"<< /Type /Annot /Subtype /Widget /FT /Tx /T ({escapedName}) /V ({escapedValue}) /Rect [50 700 300 720] /P 3 0 R >>");
        AppendWithLineEnding(sb, "endobj");

        var xrefOffset = ByteLen(sb);
        AppendWithLineEnding(sb, "xref");
        AppendWithLineEnding(sb, "0 5");
        AppendWithLineEnding(sb, "0000000000 65535 f ");
        foreach (var o in offsets) AppendWithLineEnding(sb, $"{o:D10} 00000 n ");
        AppendWithLineEnding(sb, "trailer");
        AppendWithLineEnding(sb, "<< /Size 5 /Root 1 0 R >>");
        AppendWithLineEnding(sb, "startxref");
        AppendWithLineEnding(sb, xrefOffset.ToString());
        sb.Append("%%EOF");

        return Encoding.Latin1.GetBytes(sb.ToString());
    }

    private static byte[] ZlibCompress(byte[] data)
    {
        using var ms = new MemoryStream();
        using (var zlib = new ZLibStream(ms, CompressionMode.Compress, leaveOpen: true))
            zlib.Write(data);
        return ms.ToArray();
    }
}
