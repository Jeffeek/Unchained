using System.IO.Compression;
using System.Text;
using Unchained.Pdf.Models;

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
    /// Creates a <see cref="TableData"/> with auto-generated header and cell text.
    /// Headers are <c>Col1…ColN</c>; cells are <c>R{row}C{col}</c>.
    /// </summary>
    public static TableData SimpleTableData(int rows, int cols = 3) => new()
    {
        Headers = Enumerable.Range(1, cols).Select(static i => $"Col{i}").ToList(),
        Rows = Enumerable.Range(0, rows)
            .Select(IReadOnlyList<string> (r) =>
                Enumerable.Range(1, cols).Select(c => $"R{r}C{c}").ToList())
            .ToList()
    };

    /// <summary>
    /// Generates a single-page PDF whose /Font entry includes a /FontFile2 stream
    /// containing <paramref name="fontBytes"/> (a TrueType font program).
    /// </summary>
    public static byte[] WithEmbeddedFont(byte[] fontBytes, string contentStream = "BT /F1 12 Tf 100 700 Td (Hello) Tj ET") =>
        BuildWithEmbeddedFont(fontBytes, contentStream);

    /// <summary>
    /// Generates a single-page PDF with a small <paramref name="width"/>×<paramref name="height"/>
    /// DeviceRGB image XObject and a <c>Do</c> operator that paints it.
    /// </summary>
    public static byte[] WithImageXObject(int width, int height, byte[] rgbData) =>
        BuildWithImageXObject(width, height, rgbData);

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

    /// <summary>
    /// Generates a single-page PDF with multiple AcroForm text fields.
    /// </summary>
    public static byte[] WithMultipleAcroFormFields(IReadOnlyList<(string name, string value)> fields) =>
        BuildWithMultipleAcroFormFields(fields);

    /// <summary>
    /// Generates a single-page PDF with a hierarchical AcroForm:
    /// a non-terminal parent group (<c>/T = "Group"</c>, no <c>/FT</c>) whose
    /// <c>/Kids</c> array contains two child Tx fields (<c>First</c> and <c>Second</c>).
    /// The fully-qualified names are <c>Group.First</c> and <c>Group.Second</c>.
    /// </summary>
    public static byte[] WithHierarchicalAcroForm() =>
        BuildWithHierarchicalAcroForm();

    /// <summary>
    /// Generates a single-page PDF with one Btn (checkbox) AcroForm field.
    /// </summary>
    public static byte[] WithBtnAcroForm(string fieldName = "CheckBox") =>
        BuildWithBtnAcroForm(fieldName);

    /// <summary>
    /// Generates a single-page PDF with a Tx AcroForm field that carries an
    /// inline <c>/AP /N</c> appearance stream and a <c>/P</c> page reference.
    /// Used to exercise the appearance-merging branch of <c>FlattenAsync</c>.
    /// </summary>
    public static byte[] WithAcroFormAndAppearance(string fieldName = "Field", string fieldValue = "") =>
        BuildWithAcroFormAndAppearance(fieldName, fieldValue);

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

    /// <summary>
    /// Generates a single-page PDF containing two separate image XObject stream
    /// objects whose binary pixel data is identical. Used to exercise
    /// <c>OptimizeResourcesAsync</c> duplicate-stream deduplication.
    /// </summary>
    public static byte[] WithDuplicateImageStreams(int width, int height, byte[] rgbData) =>
        BuildWithDuplicateImageStreams(width, height, rgbData);

    /// <summary>
    /// Generates a single-page PDF containing two separate font-program stream
    /// objects with identical binary content. Used to exercise
    /// <c>OptimizeResourcesAsync</c> duplicate-stream deduplication.
    /// </summary>
    public static byte[] WithDuplicateFontStreams(byte[] fontData) =>
        BuildWithDuplicateFontStreams(fontData);

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

    // Builds a PDF with a font entry whose /FontFile2 stream contains fontBytes,
    // encoded as ASCII hex so the file stays in printable ASCII throughout.
    private static byte[] BuildWithEmbeddedFont(byte[] fontBytes, string contentStream)
    {
        // Encode binary font data as hex so it can be stored in a text-based PDF.
        var hexFont = Convert.ToHexString(fontBytes);
        // ASCIIHexDecode expects hex pairs followed by '>'.
        var hexStream = hexFont + ">";
        var hexLen = hexStream.Length;

        var sb = new StringBuilder();

        AppendLineWithLineEnding("%PDF-1.7");
        AppendLineWithLineEnding("%\xE2\xE3\xCF\xD3");

        var o1 = Len();
        AppendLineWithLineEnding("1 0 obj");
        AppendLineWithLineEnding("<< /Type /Catalog /Pages 2 0 R >>");
        AppendLineWithLineEnding("endobj");
        var o2 = Len();
        AppendLineWithLineEnding("2 0 obj");
        AppendLineWithLineEnding("<< /Type /Pages /Kids [7 0 R] /Count 1 >>");
        AppendLineWithLineEnding("endobj");

        // Object 3 — content stream
        var o3 = Len();
        AppendLineWithLineEnding("3 0 obj");
        AppendLineWithLineEnding($"<< /Length {Encoding.Latin1.GetByteCount(contentStream)} >>");
        sb.Append("stream\n").Append(contentStream).Append("\nendstream\n");
        AppendLineWithLineEnding("endobj");

        // Object 4 — FontDescriptor
        var o4 = Len();
        AppendLineWithLineEnding("4 0 obj");
        AppendLineWithLineEnding("<< /Type /FontDescriptor /FontName /TestFont /Flags 32 /FontFile2 5 0 R >>");
        AppendLineWithLineEnding("endobj");

        // Object 5 — embedded font stream (hex-encoded, ASCIIHexDecode)
        var o5 = Len();
        AppendLineWithLineEnding("5 0 obj");
        AppendLineWithLineEnding($"<< /Length {hexLen} /Length1 {fontBytes.Length} /Filter /ASCIIHexDecode >>");
        sb.Append("stream\n").Append(hexStream).Append("\nendstream\n");
        AppendLineWithLineEnding("endobj");

        // Object 6 — Font
        var o6 = Len();
        AppendLineWithLineEnding("6 0 obj");
        AppendLineWithLineEnding("<< /Type /Font /Subtype /TrueType /BaseFont /TestFont /FontDescriptor 4 0 R >>");
        AppendLineWithLineEnding("endobj");

        // Object 7 — Page
        var o7 = Len();
        AppendLineWithLineEnding("7 0 obj");
        AppendLineWithLineEnding("<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Contents 3 0 R");
        AppendLineWithLineEnding("   /Resources << /Font << /F1 6 0 R >> >> >>");
        AppendLineWithLineEnding("endobj");

        var xref = Len();
        AppendLineWithLineEnding("xref");
        AppendLineWithLineEnding("0 8");
        AppendLineWithLineEnding("0000000000 65535 f ");
        foreach (var o in new[] { o1, o2, o3, o4, o5, o6, o7 })
            AppendLineWithLineEnding($"{o:D10} 00000 n ");
        AppendLineWithLineEnding("trailer");
        AppendLineWithLineEnding("<< /Size 8 /Root 1 0 R >>");
        AppendLineWithLineEnding("startxref");
        AppendLineWithLineEnding(xref.ToString());
        sb.Append("%%EOF");

        return Encoding.Latin1.GetBytes(sb.ToString());

        int Len() => Encoding.Latin1.GetByteCount(sb.ToString());

        void AppendLineWithLineEnding(string line) => sb.Append(line).Append('\n');
    }

    // Builds a PDF with a DeviceRGB image XObject and a Do operator that paints it.
    // The image stream is ZLib-compressed; since the compressed bytes are binary,
    // the PDF is assembled as a byte array using a MemoryStream.
    private static byte[] BuildWithImageXObject(int width, int height, byte[] rgbData)
    {
        var compressed = ZlibCompress(rgbData);
        var cs = $"q {width * 10} 0 0 {height * 10} 0 0 cm /Im1 Do Q";
        var csBytes = Encoding.Latin1.GetBytes(cs);

        using var ms = new MemoryStream();
        Line("%PDF-1.7");
        Line("%\xE2\xE3\xCF\xD3");

        var o1 = Pos();
        Line("1 0 obj");
        Line("<< /Type /Catalog /Pages 2 0 R >>");
        Line("endobj");
        var o2 = Pos();
        Line("2 0 obj");
        Line("<< /Type /Pages /Kids [5 0 R] /Count 1 >>");
        Line("endobj");

        // Object 3 — content stream
        var o3 = Pos();
        Line("3 0 obj");
        Line($"<< /Length {csBytes.Length} >>");
        Line("stream");
        Binary(csBytes);
        Line(string.Empty);
        Line("endstream");
        Line("endobj");

        // Object 4 — Image XObject (FlateDecode, binary stream)
        var o4 = Pos();
        Line("4 0 obj");
        Line($"<< /Type /XObject /Subtype /Image /Width {width} /Height {height}");
        Line($"   /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /FlateDecode /Length {compressed.Length} >>");
        Line("stream");
        Binary(compressed);
        Line(string.Empty);
        Line("endstream");
        Line("endobj");

        // Object 5 — Page
        var o5 = Pos();
        Line("5 0 obj");
        Line("<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Contents 3 0 R");
        Line("   /Resources << /XObject << /Im1 4 0 R >> >> >>");
        Line("endobj");

        var xref = Pos();
        Line("xref");
        Line("0 6");
        Line("0000000000 65535 f ");
        foreach (var o in new[] { o1, o2, o3, o4, o5 })
            Line($"{o:D10} 00000 n ");
        Line("trailer");
        Line("<< /Size 6 /Root 1 0 R >>");
        Line("startxref");
        Line(xref.ToString());
        Text("%%EOF");

        return ms.ToArray();

        void Text(string s) => ms.Write(Encoding.Latin1.GetBytes(s));

        void Line(string s)
        {
            Text(s);
            ms.WriteByte((byte)'\n');
        }

        void Binary(byte[] b) => ms.Write(b);

        long Pos() => ms.Position;
    }

    private static byte[] BuildWithMultipleAcroFormFields(IReadOnlyList<(string name, string value)> fields)
    {
        // Objects: 1=Catalog, 2=Pages, 3=Page, 4..(3+N)=field widgets
        var fieldCount = fields.Count;
        var fieldRefs = string.Join(" ", Enumerable.Range(4, fieldCount).Select(static n => $"{n} 0 R"));
        var sb = new StringBuilder();
        var offsets = new List<int>();

        AppendWithLineEnding(sb, "%PDF-1.7");
        AppendWithLineEnding(sb, "%\xE2\xE3\xCF\xD3");

        offsets.Add(ByteLen(sb));
        AppendWithLineEnding(sb, "1 0 obj");
        AppendWithLineEnding(sb, $"<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [{fieldRefs}] >> >>");
        AppendWithLineEnding(sb, "endobj");

        offsets.Add(ByteLen(sb));
        AppendWithLineEnding(sb, "2 0 obj");
        AppendWithLineEnding(sb, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
        AppendWithLineEnding(sb, "endobj");

        offsets.Add(ByteLen(sb));
        AppendWithLineEnding(sb, "3 0 obj");
        AppendWithLineEnding(sb, $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Annots [{fieldRefs}] >>");
        AppendWithLineEnding(sb, "endobj");

        for (var i = 0; i < fieldCount; i++)
        {
            var (name, value) = fields[i];
            offsets.Add(ByteLen(sb));
            AppendWithLineEnding(sb, $"{4 + i} 0 obj");
            AppendWithLineEnding(sb, $"<< /Type /Annot /Subtype /Widget /FT /Tx /T ({EscapeString(name)}) /V ({EscapeString(value)}) /Rect [50 {700 - (i * 30)} 300 {720 - (i * 30)}] /P 3 0 R >>");
            AppendWithLineEnding(sb, "endobj");
        }

        var totalObjects = 4 + fieldCount;
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

    private static byte[] BuildWithHierarchicalAcroForm()
    {
        // Layout:
        //   1 = Catalog  (AcroForm /Fields [5 0 R])
        //   2 = Pages
        //   3 = Page     (/Annots [6 0 R 7 0 R])
        //   4 = Group    (/T (Group) /Kids [6 0 R 7 0 R])  — non-terminal, no /FT
        //   5 = AcroForm Fields array reference wrapper — actually the group is obj 4,
        //       so /Fields [4 0 R] in catalog
        //   6 = Child1   (/FT /Tx /T (First) /V (v1) /P 3 0 R)
        //   7 = Child2   (/FT /Tx /T (Second) /V (v2) /P 3 0 R)
        var sb = new StringBuilder();
        var offsets = new List<int>();

        AppendWithLineEnding(sb, "%PDF-1.7");
        AppendWithLineEnding(sb, "%\xE2\xE3\xCF\xD3");

        // 1 — Catalog
        offsets.Add(ByteLen(sb));
        AppendWithLineEnding(sb, "1 0 obj");
        AppendWithLineEnding(sb, "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [4 0 R] >> >>");
        AppendWithLineEnding(sb, "endobj");

        // 2 — Pages
        offsets.Add(ByteLen(sb));
        AppendWithLineEnding(sb, "2 0 obj");
        AppendWithLineEnding(sb, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
        AppendWithLineEnding(sb, "endobj");

        // 3 — Page
        offsets.Add(ByteLen(sb));
        AppendWithLineEnding(sb, "3 0 obj");
        AppendWithLineEnding(sb, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Annots [5 0 R 6 0 R] >>");
        AppendWithLineEnding(sb, "endobj");

        // 4 — Non-terminal group node (no /FT, has /Kids)
        offsets.Add(ByteLen(sb));
        AppendWithLineEnding(sb, "4 0 obj");
        AppendWithLineEnding(sb, "<< /T (Group) /Kids [5 0 R 6 0 R] >>");
        AppendWithLineEnding(sb, "endobj");

        // 5 — Child field "First"
        offsets.Add(ByteLen(sb));
        AppendWithLineEnding(sb, "5 0 obj");
        AppendWithLineEnding(sb, "<< /Type /Annot /Subtype /Widget /FT /Tx /T (First) /V (v1) /Rect [50 700 300 720] /P 3 0 R >>");
        AppendWithLineEnding(sb, "endobj");

        // 6 — Child field "Second"
        offsets.Add(ByteLen(sb));
        AppendWithLineEnding(sb, "6 0 obj");
        AppendWithLineEnding(sb, "<< /Type /Annot /Subtype /Widget /FT /Tx /T (Second) /V (v2) /Rect [50 660 300 680] /P 3 0 R >>");
        AppendWithLineEnding(sb, "endobj");

        var xrefOffset = ByteLen(sb);
        AppendWithLineEnding(sb, "xref");
        AppendWithLineEnding(sb, "0 7");
        AppendWithLineEnding(sb, "0000000000 65535 f ");
        foreach (var o in offsets) AppendWithLineEnding(sb, $"{o:D10} 00000 n ");
        AppendWithLineEnding(sb, "trailer");
        AppendWithLineEnding(sb, "<< /Size 7 /Root 1 0 R >>");
        AppendWithLineEnding(sb, "startxref");
        AppendWithLineEnding(sb, xrefOffset.ToString());
        sb.Append("%%EOF");

        return Encoding.Latin1.GetBytes(sb.ToString());
    }

    private static byte[] BuildWithBtnAcroForm(string fieldName)
    {
        var sb = new StringBuilder();
        var offsets = new List<int>();

        AppendWithLineEnding(sb, "%PDF-1.7");
        AppendWithLineEnding(sb, "%\xE2\xE3\xCF\xD3");

        offsets.Add(ByteLen(sb));
        AppendWithLineEnding(sb, "1 0 obj");
        AppendWithLineEnding(sb, "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [4 0 R] >> >>");
        AppendWithLineEnding(sb, "endobj");

        offsets.Add(ByteLen(sb));
        AppendWithLineEnding(sb, "2 0 obj");
        AppendWithLineEnding(sb, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
        AppendWithLineEnding(sb, "endobj");

        offsets.Add(ByteLen(sb));
        AppendWithLineEnding(sb, "3 0 obj");
        AppendWithLineEnding(sb, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Annots [4 0 R] >>");
        AppendWithLineEnding(sb, "endobj");

        offsets.Add(ByteLen(sb));
        AppendWithLineEnding(sb, "4 0 obj");
        AppendWithLineEnding(sb, $"<< /Type /Annot /Subtype /Widget /FT /Btn /T ({EscapeString(fieldName)}) /V /Off /Rect [50 700 70 720] /P 3 0 R >>");
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

    private static byte[] BuildWithAcroFormAndAppearance(string fieldName, string fieldValue)
    {
        // The appearance stream content (a simple "BT ... ET" block).
        const string apContent = "BT /Helv 12 Tf 2 2 Td (Hi) Tj ET";
        var apBytes = Encoding.Latin1.GetBytes(apContent);

        // Objects:
        //   1 = Catalog  (/AcroForm /Fields [5 0 R])
        //   2 = Pages
        //   3 = Page     (/Annots [5 0 R])
        //   4 = Appearance stream  (inline /AP /N points here)
        //   5 = Text field widget  (/FT /Tx /AP << /N 4 0 R >> /P 3 0 R)

        var sb = new StringBuilder();
        var offsets = new List<int>();

        AppendWithLineEnding(sb, "%PDF-1.7");
        AppendWithLineEnding(sb, "%\xE2\xE3\xCF\xD3");

        offsets.Add(ByteLen(sb));
        AppendWithLineEnding(sb, "1 0 obj");
        AppendWithLineEnding(sb, "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [5 0 R] >> >>");
        AppendWithLineEnding(sb, "endobj");

        offsets.Add(ByteLen(sb));
        AppendWithLineEnding(sb, "2 0 obj");
        AppendWithLineEnding(sb, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
        AppendWithLineEnding(sb, "endobj");

        offsets.Add(ByteLen(sb));
        AppendWithLineEnding(sb, "3 0 obj");
        AppendWithLineEnding(sb, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Annots [5 0 R] >>");
        AppendWithLineEnding(sb, "endobj");

        // 4 — Normal appearance stream (XObject Form)
        offsets.Add(ByteLen(sb));
        AppendWithLineEnding(sb, "4 0 obj");
        AppendWithLineEnding(sb, $"<< /Type /XObject /Subtype /Form /BBox [0 0 250 20] /Length {apBytes.Length} >>");
        sb.Append("stream\n");
        sb.Append(apContent);
        AppendWithLineEnding(sb, "\nendstream");
        AppendWithLineEnding(sb, "endobj");

        // 5 — Text field widget with /AP referencing obj 4
        offsets.Add(ByteLen(sb));
        AppendWithLineEnding(sb, "5 0 obj");
        AppendWithLineEnding(sb, $"<< /Type /Annot /Subtype /Widget /FT /Tx /T ({EscapeString(fieldName)}) /V ({EscapeString(fieldValue)}) /Rect [50 700 300 720] /P 3 0 R /AP << /N 4 0 R >> >>");
        AppendWithLineEnding(sb, "endobj");

        var xrefOffset = ByteLen(sb);
        AppendWithLineEnding(sb, "xref");
        AppendWithLineEnding(sb, "0 6");
        AppendWithLineEnding(sb, "0000000000 65535 f ");
        foreach (var o in offsets) AppendWithLineEnding(sb, $"{o:D10} 00000 n ");
        AppendWithLineEnding(sb, "trailer");
        AppendWithLineEnding(sb, "<< /Size 6 /Root 1 0 R >>");
        AppendWithLineEnding(sb, "startxref");
        AppendWithLineEnding(sb, xrefOffset.ToString());
        sb.Append("%%EOF");

        return Encoding.Latin1.GetBytes(sb.ToString());
    }

    // ── Duplicate stream fixtures (for OptimizeResourcesAsync tests) ──────────

    // Builds a single-page PDF with two separate image XObject stream objects
    // that hold identical pixel data. The page references Im1 (obj 4); Im2 (obj 6)
    // is declared in the resource dict but not painted, making it a pure duplicate
    // for deduplication purposes.
    private static byte[] BuildWithDuplicateImageStreams(int width, int height, byte[] rgbData)
    {
        // We store the pixel data raw (no filter) so OptimizeResources can see
        // identical byte sequences in both stream objects.
        using var ms = new MemoryStream();

        Line("%PDF-1.7");
        Line("%\xE2\xE3\xCF\xD3");

        // Object 1 — Catalog
        var o1 = Pos();
        Line("1 0 obj");
        Line("<< /Type /Catalog /Pages 2 0 R >>");
        Line("endobj");

        // Object 2 — Pages
        var o2 = Pos();
        Line("2 0 obj");
        Line("<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
        Line("endobj");

        // Object 3 — content stream (paints Im1 only)
        var cs = $"q {width * 10} 0 0 {height * 10} 0 0 cm /Im1 Do Q";
        var csBytes = Encoding.Latin1.GetBytes(cs);
        var o3 = Pos();
        Line("3 0 obj");
        Line($"<< /Length {csBytes.Length} >>");
        Line("stream");
        Binary(csBytes);
        Line(string.Empty);
        Line("endstream");
        Line("endobj");

        // Object 4 — first image XObject (raw, no filter)
        var o4 = Pos();
        Line("4 0 obj");
        Line($"<< /Type /XObject /Subtype /Image /Width {width} /Height {height}");
        Line($"   /ColorSpace /DeviceRGB /BitsPerComponent 8 /Length {rgbData.Length} >>");
        Line("stream");
        Binary(rgbData);
        Line(string.Empty);
        Line("endstream");
        Line("endobj");

        // Object 5 — Page (references both Im1 and Im2 in Resources)
        var o5 = Pos();
        Line("5 0 obj");
        Line("<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Contents 3 0 R");
        Line("   /Resources << /XObject << /Im1 4 0 R /Im2 6 0 R >> >> >>");
        Line("endobj");

        // Object 6 — second image XObject with identical pixel data
        var o6 = Pos();
        Line("6 0 obj");
        Line($"<< /Type /XObject /Subtype /Image /Width {width} /Height {height}");
        Line($"   /ColorSpace /DeviceRGB /BitsPerComponent 8 /Length {rgbData.Length} >>");
        Line("stream");
        Binary(rgbData);
        Line(string.Empty);
        Line("endstream");
        Line("endobj");

        var xref = Pos();
        Line("xref");
        Line("0 7");
        Line("0000000000 65535 f ");
        foreach (var o in new[] { o1, o2, o3, o4, o5, o6 })
            Line($"{o:D10} 00000 n ");
        Line("trailer");
        Line("<< /Size 7 /Root 1 0 R >>");
        Line("startxref");
        Line(xref.ToString());
        Text("%%EOF");

        return ms.ToArray();

        void Text(string s) => ms.Write(Encoding.Latin1.GetBytes(s));

        void Line(string s)
        {
            Text(s);
            ms.WriteByte((byte)'\n');
        }

        void Binary(byte[] b) => ms.Write(b);
        long Pos() => ms.Position;
    }

    // Builds a single-page PDF with two separate font-program stream objects that
    // hold identical binary data. Both are referenced from FontDescriptor dicts.
    private static byte[] BuildWithDuplicateFontStreams(byte[] fontData)
    {
        // Hex-encode so the font bytes can appear in a text-only PDF.
        var hex1 = Convert.ToHexString(fontData) + ">";
        var hex2 = Convert.ToHexString(fontData) + ">"; // identical content

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

        // Object 3 — Page (uses font F1)
        offsets.Add(ByteLen(sb));
        AppendWithLineEnding(sb, "3 0 obj");
        AppendWithLineEnding(sb, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842]");
        AppendWithLineEnding(sb, "   /Resources << /Font << /F1 6 0 R /F2 9 0 R >> >> >>");
        AppendWithLineEnding(sb, "endobj");

        // Object 4 — first font program stream (ASCIIHexDecode)
        offsets.Add(ByteLen(sb));
        AppendWithLineEnding(sb, "4 0 obj");
        AppendWithLineEnding(sb, $"<< /Length {hex1.Length} /Length1 {fontData.Length} /Filter /ASCIIHexDecode >>");
        sb.Append("stream\n").Append(hex1).Append("\nendstream\n");
        AppendWithLineEnding(sb, "endobj");

        // Object 5 — first FontDescriptor
        offsets.Add(ByteLen(sb));
        AppendWithLineEnding(sb, "5 0 obj");
        AppendWithLineEnding(sb, "<< /Type /FontDescriptor /FontName /FontA /Flags 32 /FontFile2 4 0 R >>");
        AppendWithLineEnding(sb, "endobj");

        // Object 6 — first Font
        offsets.Add(ByteLen(sb));
        AppendWithLineEnding(sb, "6 0 obj");
        AppendWithLineEnding(sb, "<< /Type /Font /Subtype /TrueType /BaseFont /FontA /FontDescriptor 5 0 R >>");
        AppendWithLineEnding(sb, "endobj");

        // Object 7 — second font program stream (identical bytes, different object)
        offsets.Add(ByteLen(sb));
        AppendWithLineEnding(sb, "7 0 obj");
        AppendWithLineEnding(sb, $"<< /Length {hex2.Length} /Length1 {fontData.Length} /Filter /ASCIIHexDecode >>");
        sb.Append("stream\n").Append(hex2).Append("\nendstream\n");
        AppendWithLineEnding(sb, "endobj");

        // Object 8 — second FontDescriptor
        offsets.Add(ByteLen(sb));
        AppendWithLineEnding(sb, "8 0 obj");
        AppendWithLineEnding(sb, "<< /Type /FontDescriptor /FontName /FontB /Flags 32 /FontFile2 7 0 R >>");
        AppendWithLineEnding(sb, "endobj");

        // Object 9 — second Font
        offsets.Add(ByteLen(sb));
        AppendWithLineEnding(sb, "9 0 obj");
        AppendWithLineEnding(sb, "<< /Type /Font /Subtype /TrueType /BaseFont /FontB /FontDescriptor 8 0 R >>");
        AppendWithLineEnding(sb, "endobj");

        var xrefOffset = ByteLen(sb);
        AppendWithLineEnding(sb, "xref");
        AppendWithLineEnding(sb, "0 10");
        AppendWithLineEnding(sb, "0000000000 65535 f ");
        foreach (var o in offsets)
            AppendWithLineEnding(sb, $"{o:D10} 00000 n ");
        AppendWithLineEnding(sb, "trailer");
        AppendWithLineEnding(sb, "<< /Size 10 /Root 1 0 R >>");
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
