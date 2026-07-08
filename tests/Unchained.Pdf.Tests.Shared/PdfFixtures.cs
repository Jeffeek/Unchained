using System.IO.Compression;
using System.Text;
using Unchained.Pdf.Models;

namespace Unchained.Pdf.Tests.Shared;

/// <summary>
///     Produces minimal but spec-compliant PDF byte arrays for use as test fixtures.
///     Uses explicit <c>\n</c> line endings for cross-platform byte-offset consistency.
/// </summary>
internal static class PdfFixtures
{
    public static byte[] SinglePage() => Build(1);

    public static byte[] MultiPage(int count) => Build(count);

    /// <summary>
    ///     Creates a <see cref="TableData" /> with auto-generated header and cell text.
    ///     Headers are <c>Col1…ColN</c>; cells are <c>R{row}C{col}</c>.
    /// </summary>
    public static TableData SimpleTableData(int rows, int cols = 3) => new()
    {
        Headers = Enumerable.Range(1, cols).Select(static i => $"Col{i}").ToList(),
        Rows = Enumerable.Range(0, rows)
            .Select(IReadOnlyList<string> (r) =>
                Enumerable.Range(1, cols).Select(c => $"R{r}C{c}").ToList()
            )
            .ToList()
    };

    /// <summary>
    ///     Generates a single-page PDF whose /Font entry includes a /FontFile2 stream
    ///     containing <paramref name="fontBytes" /> (a TrueType font program).
    /// </summary>
    public static byte[] WithEmbeddedFont(byte[] fontBytes, string contentStream = "BT /F1 12 Tf 100 700 Td (Hello) Tj ET") =>
        BuildWithEmbeddedFont(fontBytes, contentStream);

    /// <summary>
    ///     Generates a two-page PDF where each page references its own private image XObject
    ///     (raw DeviceRGB pixel data of the given size). Page 1 → Im1, page 2 → Im2. Deleting a
    ///     page should orphan (and prune) that page's image, meaningfully shrinking the output.
    /// </summary>
    public static byte[] TwoPagesEachWithPrivateImage(int width, int height) =>
        BuildTwoPagesEachWithPrivateImage(width, height);

    /// <summary>
    ///     Generates a single-page PDF with a small <paramref name="width" />×<paramref name="height" />
    ///     DeviceRGB image XObject and a <c>Do</c> operator that paints it.
    /// </summary>
    public static byte[] WithImageXObject(int width, int height, byte[] rgbData) =>
        BuildWithImageXObject(width, height, rgbData);

    /// <summary>
    ///     Generates a single-page PDF whose first page has one /Text annotation.
    /// </summary>
    public static byte[] WithAnnotation(string contents = "Note") =>
        BuildWithAnnotation(contents);

    /// <summary>
    ///     Generates a two-page PDF with a flat /Outlines tree pointing at the pages.
    /// </summary>
    public static byte[] WithOutlines(params (string title, int page)[] bookmarks) =>
        BuildWithOutlines(bookmarks);

    /// <summary>
    ///     Generates a single-page PDF with one AcroForm text field.
    /// </summary>
    public static byte[] WithAcroForm(string fieldName = "TextField", string fieldValue = "") =>
        BuildWithAcroForm(fieldName, fieldValue);

    /// <summary>
    ///     Generates a single-page PDF with multiple AcroForm text fields.
    /// </summary>
    public static byte[] WithMultipleAcroFormFields(IReadOnlyList<(string name, string value)> fields) =>
        BuildWithMultipleAcroFormFields(fields);

    /// <summary>
    ///     Generates a single-page PDF with a hierarchical AcroForm:
    ///     a non-terminal parent group (<c>/T = "Group"</c>, no <c>/FT</c>) whose
    ///     <c>/Kids</c> array contains two child Tx fields (<c>First</c> and <c>Second</c>).
    ///     The fully-qualified names are <c>Group.First</c> and <c>Group.Second</c>.
    /// </summary>
    public static byte[] WithHierarchicalAcroForm() =>
        BuildWithHierarchicalAcroForm();

    /// <summary>
    ///     Generates a single-page PDF with one Btn (checkbox) AcroForm field.
    /// </summary>
    public static byte[] WithBtnAcroForm(string fieldName = "CheckBox") =>
        BuildWithBtnAcroForm(fieldName);

    /// <summary>
    ///     Generates a single-page PDF with a Tx AcroForm field that carries an
    ///     inline <c>/AP /N</c> appearance stream and a <c>/P</c> page reference.
    ///     Used to exercise the appearance-merging branch of <c>FlattenAsync</c>.
    /// </summary>
    public static byte[] WithAcroFormAndAppearance(string fieldName = "Field", string fieldValue = "") =>
        BuildWithAcroFormAndAppearance(fieldName, fieldValue);

    /// <summary>
    ///     A single-page PDF with one Ch (choice) AcroForm field, exercising the choice-field
    ///     fill branch in <c>FormFiller</c>.
    /// </summary>
    public static byte[] WithChoiceAcroForm(string fieldName = "Choice")
    {
        var sb = new StringBuilder();
        var offsets = new List<int>();
        Ln(sb, "%PDF-1.7");

        offsets.Add(ByteLength(sb));
        Ln(sb, "1 0 obj");
        Ln(sb, "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [4 0 R] >> >>");
        Ln(sb, "endobj");

        offsets.Add(ByteLength(sb));
        Ln(sb, "2 0 obj");
        Ln(sb, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
        Ln(sb, "endobj");

        offsets.Add(ByteLength(sb));
        Ln(sb, "3 0 obj");
        Ln(sb, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Annots [4 0 R] >>");
        Ln(sb, "endobj");

        offsets.Add(ByteLength(sb));
        Ln(sb, "4 0 obj");
        Ln(
            sb,
            $"<< /Type /Annot /Subtype /Widget /FT /Ch /T ({EscapeString(fieldName)}) /Opt [(One) (Two)] /Rect [50 700 200 720] /P 3 0 R >>"
        );
        Ln(sb, "endobj");

        var xrefOffset = ByteLength(sb);
        Ln(sb, "xref");
        Ln(sb, "0 5");
        Ln(sb, "0000000000 65535 f ");
        foreach (var o in offsets) Ln(sb, $"{o:D10} 00000 n ");
        Ln(sb, "trailer");
        Ln(sb, "<< /Size 5 /Root 1 0 R >>");
        Ln(sb, "startxref");
        Ln(sb, xrefOffset.ToString());
        sb.Append("%%EOF");
        return Encoding.Latin1.GetBytes(sb.ToString());
    }

    /// <summary>
    ///     A single-page PDF with a Btn checkbox whose <c>/AP /N</c> dictionary declares an explicit
    ///     "on" appearance state ("On") alongside "Off". Exercises <c>FormFiller.OnStateName</c>.
    /// </summary>
    public static byte[] WithBtnAcroFormAndOnState(string fieldName = "Check", string onState = "On")
    {
        var sb = new StringBuilder();
        var offsets = new List<int>();
        Ln(sb, "%PDF-1.7");

        offsets.Add(ByteLength(sb));
        Ln(sb, "1 0 obj");
        Ln(sb, "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [4 0 R] >> >>");
        Ln(sb, "endobj");

        offsets.Add(ByteLength(sb));
        Ln(sb, "2 0 obj");
        Ln(sb, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
        Ln(sb, "endobj");

        offsets.Add(ByteLength(sb));
        Ln(sb, "3 0 obj");
        Ln(sb, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Annots [4 0 R] >>");
        Ln(sb, "endobj");

        offsets.Add(ByteLength(sb));
        Ln(sb, "4 0 obj");
        Ln(
            sb,
            $"<< /Type /Annot /Subtype /Widget /FT /Btn /T ({EscapeString(fieldName)}) /V /Off /Rect [50 700 70 720] /P 3 0 R" +
            $" /AP << /N << /{onState} 5 0 R /Off 6 0 R >> >> >>"
        );
        Ln(sb, "endobj");

        offsets.Add(ByteLength(sb));
        Ln(sb, "5 0 obj");
        Ln(sb, "<< /Type /XObject /Subtype /Form /BBox [0 0 20 20] /Length 0 >>");
        sb.Append("stream\n");
        Ln(sb, "\nendstream");
        Ln(sb, "endobj");

        offsets.Add(ByteLength(sb));
        Ln(sb, "6 0 obj");
        Ln(sb, "<< /Type /XObject /Subtype /Form /BBox [0 0 20 20] /Length 0 >>");
        sb.Append("stream\n");
        Ln(sb, "\nendstream");
        Ln(sb, "endobj");

        var xrefOffset = ByteLength(sb);
        Ln(sb, "xref");
        Ln(sb, "0 7");
        Ln(sb, "0000000000 65535 f ");
        foreach (var o in offsets) Ln(sb, $"{o:D10} 00000 n ");
        Ln(sb, "trailer");
        Ln(sb, "<< /Size 7 /Root 1 0 R >>");
        Ln(sb, "startxref");
        Ln(sb, xrefOffset.ToString());
        sb.Append("%%EOF");
        return Encoding.Latin1.GetBytes(sb.ToString());
    }

    /// <summary>
    ///     A Tx AcroForm field with an <c>/AP /N</c> appearance stream on a page that already has a
    ///     <c>/Contents</c> stream. Exercises the append-to-existing-contents branch of
    ///     <c>FormFiller.FlattenAsync</c>.
    /// </summary>
    public static byte[] WithAcroFormAppearanceAndPageContents(string fieldName = "F", string fieldValue = "v")
    {
        const string apContent = "BT /Helv 12 Tf 2 2 Td (Hi) Tj ET";
        var apBytes = Encoding.Latin1.GetBytes(apContent);
        const string pageContent = "0 0 100 100 re f";
        var pageBytes = Encoding.Latin1.GetBytes(pageContent);

        var sb = new StringBuilder();
        var offsets = new List<int>();
        Ln(sb, "%PDF-1.7");

        offsets.Add(ByteLength(sb));
        Ln(sb, "1 0 obj");
        Ln(sb, "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [5 0 R] >> >>");
        Ln(sb, "endobj");

        offsets.Add(ByteLength(sb));
        Ln(sb, "2 0 obj");
        Ln(sb, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
        Ln(sb, "endobj");

        offsets.Add(ByteLength(sb));
        Ln(sb, "3 0 obj");
        Ln(sb, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Annots [5 0 R] /Contents 6 0 R >>");
        Ln(sb, "endobj");

        offsets.Add(ByteLength(sb));
        Ln(sb, "4 0 obj");
        Ln(sb, $"<< /Type /XObject /Subtype /Form /BBox [0 0 250 20] /Length {apBytes.Length} >>");
        sb.Append("stream\n");
        sb.Append(apContent);
        Ln(sb, "\nendstream");
        Ln(sb, "endobj");

        offsets.Add(ByteLength(sb));
        Ln(sb, "5 0 obj");
        Ln(
            sb,
            $"<< /Type /Annot /Subtype /Widget /FT /Tx /T ({EscapeString(fieldName)}) /V ({EscapeString(fieldValue)}) /Rect [50 700 300 720] /P 3 0 R /AP << /N 4 0 R >> >>"
        );
        Ln(sb, "endobj");

        offsets.Add(ByteLength(sb));
        Ln(sb, "6 0 obj");
        Ln(sb, $"<< /Length {pageBytes.Length} >>");
        sb.Append("stream\n");
        sb.Append(pageContent);
        Ln(sb, "\nendstream");
        Ln(sb, "endobj");

        var xrefOffset = ByteLength(sb);
        Ln(sb, "xref");
        Ln(sb, "0 7");
        Ln(sb, "0000000000 65535 f ");
        foreach (var o in offsets) Ln(sb, $"{o:D10} 00000 n ");
        Ln(sb, "trailer");
        Ln(sb, "<< /Size 7 /Root 1 0 R >>");
        Ln(sb, "startxref");
        Ln(sb, xrefOffset.ToString());
        sb.Append("%%EOF");
        return Encoding.Latin1.GetBytes(sb.ToString());
    }

    public static byte[] WithInfo(string title, string author) =>
        Build(1, title, author);

    /// <summary>
    ///     A Btn AcroForm field whose <c>/AP /N</c> on-state appearance lives on a widget kid rather
    ///     than the field itself, and whose <c>/FT</c> is inherited by the kid from the parent.
    ///     Exercises <c>FormFiller.OnStateName</c>'s kid-widget lookup and <c>FieldType</c>'s
    ///     <c>/Parent</c> walk.
    /// </summary>
    public static byte[] WithBtnParentKidAcroForm(string fieldName = "Group", string onState = "On")
    {
        var sb = new StringBuilder();
        var offsets = new List<int>();
        Ln(sb, "%PDF-1.7");

        offsets.Add(ByteLength(sb));
        Ln(sb, "1 0 obj");
        Ln(sb, "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [4 0 R] >> >>");
        Ln(sb, "endobj");

        offsets.Add(ByteLength(sb));
        Ln(sb, "2 0 obj");
        Ln(sb, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
        Ln(sb, "endobj");

        offsets.Add(ByteLength(sb));
        Ln(sb, "3 0 obj");
        Ln(sb, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Annots [5 0 R] >>");
        Ln(sb, "endobj");

        // Parent field: carries /FT /Btn and /Kids; no /AP of its own.
        offsets.Add(ByteLength(sb));
        Ln(sb, "4 0 obj");
        Ln(sb, $"<< /FT /Btn /T ({EscapeString(fieldName)}) /V /Off /Kids [5 0 R] >>");
        Ln(sb, "endobj");

        // Widget kid: inherits /FT from /Parent, carries the /AP /N on-state dictionary.
        offsets.Add(ByteLength(sb));
        Ln(sb, "5 0 obj");
        Ln(
            sb,
            $"<< /Type /Annot /Subtype /Widget /Parent 4 0 R /Rect [50 700 70 720]" +
            $" /AP << /N << /{onState} 6 0 R /Off 7 0 R >> >> >>"
        );
        Ln(sb, "endobj");

        offsets.Add(ByteLength(sb));
        Ln(sb, "6 0 obj");
        Ln(sb, "<< /Type /XObject /Subtype /Form /BBox [0 0 20 20] /Length 0 >>");
        sb.Append("stream\n");
        Ln(sb, "\nendstream");
        Ln(sb, "endobj");

        offsets.Add(ByteLength(sb));
        Ln(sb, "7 0 obj");
        Ln(sb, "<< /Type /XObject /Subtype /Form /BBox [0 0 20 20] /Length 0 >>");
        sb.Append("stream\n");
        Ln(sb, "\nendstream");
        Ln(sb, "endobj");

        var xrefOffset = ByteLength(sb);
        Ln(sb, "xref");
        Ln(sb, "0 8");
        Ln(sb, "0000000000 65535 f ");
        foreach (var o in offsets) Ln(sb, $"{o:D10} 00000 n ");
        Ln(sb, "trailer");
        Ln(sb, "<< /Size 8 /Root 1 0 R >>");
        Ln(sb, "startxref");
        Ln(sb, xrefOffset.ToString());
        sb.Append("%%EOF");
        return Encoding.Latin1.GetBytes(sb.ToString());
    }

    /// <summary>
    ///     A single-page PDF that deliberately violates several PDF/A-1b rules: a non-embedded
    ///     Type1 font (missing /FontDescriptor → §6.3.3), a prohibited /FileAttachment annotation
    ///     without the Print flag (§6.5.3), and a catalog /AA additional-actions dict (§6.6.1).
    ///     Used to exercise the violation-detecting branches of <c>PdfAValidator</c>.
    /// </summary>
    public static byte[] WithPdfAViolations()
    {
        var sb = new StringBuilder();
        var offsets = new List<int>();
        Ln(sb, "%PDF-1.7");

        offsets.Add(ByteLength(sb));
        Ln(sb, "1 0 obj");
        Ln(sb, "<< /Type /Catalog /Pages 2 0 R /AA << /WC << /S /JavaScript >> >> >>");
        Ln(sb, "endobj");

        offsets.Add(ByteLength(sb));
        Ln(sb, "2 0 obj");
        Ln(sb, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
        Ln(sb, "endobj");

        offsets.Add(ByteLength(sb));
        Ln(sb, "3 0 obj");
        Ln(sb, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100]");
        Ln(sb, "   /Resources << /Font << /F1 4 0 R /F2 6 0 R >> >> /Annots [5 0 R 7 0 R] >>");
        Ln(sb, "endobj");

        // Non-embedded Type1 font: no /FontDescriptor → 6.3.3 violation.
        offsets.Add(ByteLength(sb));
        Ln(sb, "4 0 obj");
        Ln(sb, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");
        Ln(sb, "endobj");

        // FileAttachment annotation, no Print flag → 6.5.3 violations.
        offsets.Add(ByteLength(sb));
        Ln(sb, "5 0 obj");
        Ln(sb, "<< /Type /Annot /Subtype /FileAttachment /Rect [10 10 30 30] >>");
        Ln(sb, "endobj");

        // Font with a /FontDescriptor that has no embedded /FontFile* → 6.3.3 violation (no-file path).
        offsets.Add(ByteLength(sb));
        Ln(sb, "6 0 obj");
        Ln(sb, "<< /Type /Font /Subtype /TrueType /BaseFont /Arial /FontDescriptor 8 0 R >>");
        Ln(sb, "endobj");

        // Widget annotation with no /AP → 6.5.4 violation.
        offsets.Add(ByteLength(sb));
        Ln(sb, "7 0 obj");
        Ln(sb, "<< /Type /Annot /Subtype /Widget /Rect [40 40 60 60] /F 4 >>");
        Ln(sb, "endobj");

        offsets.Add(ByteLength(sb));
        Ln(sb, "8 0 obj");
        Ln(sb, "<< /Type /FontDescriptor /FontName /Arial /Flags 32 >>");
        Ln(sb, "endobj");

        var xrefOffset = ByteLength(sb);
        Ln(sb, "xref");
        Ln(sb, "0 9");
        Ln(sb, "0000000000 65535 f ");
        foreach (var o in offsets) Ln(sb, $"{o:D10} 00000 n ");
        Ln(sb, "trailer");
        Ln(sb, "<< /Size 9 /Root 1 0 R >>");
        Ln(sb, "startxref");
        Ln(sb, xrefOffset.ToString());
        sb.Append("%%EOF");
        return Encoding.Latin1.GetBytes(sb.ToString());
    }

    /// <summary>
    ///     A two-page PDF with a legacy <c>/Dests</c> dictionary in the catalog (PDF 1.0 style),
    ///     mapping the name <c>intro</c> to a destination array targeting page 2. Exercises the
    ///     legacy named-destination path in <c>PdfDocumentAdapter.GetNamedDestinations</c>.
    /// </summary>
    public static byte[] WithLegacyDests()
    {
        var sb = new StringBuilder();
        var offsets = new List<int>();
        Ln(sb, "%PDF-1.7");

        offsets.Add(ByteLength(sb));
        Ln(sb, "1 0 obj");
        Ln(sb, "<< /Type /Catalog /Pages 2 0 R /Dests << /intro [5 0 R /Fit] >> >>");
        Ln(sb, "endobj");

        offsets.Add(ByteLength(sb));
        Ln(sb, "2 0 obj");
        Ln(sb, "<< /Type /Pages /Kids [4 0 R 5 0 R] /Count 2 >>");
        Ln(sb, "endobj");

        // obj 3 intentionally unused to keep object numbering simple; mark free below.
        offsets.Add(ByteLength(sb));
        Ln(sb, "3 0 obj");
        Ln(sb, "<< /Type /Pages /Kids [4 0 R 5 0 R] /Count 2 >>");
        Ln(sb, "endobj");

        offsets.Add(ByteLength(sb));
        Ln(sb, "4 0 obj");
        Ln(sb, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] >>");
        Ln(sb, "endobj");

        offsets.Add(ByteLength(sb));
        Ln(sb, "5 0 obj");
        Ln(sb, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] >>");
        Ln(sb, "endobj");

        var xrefOffset = ByteLength(sb);
        Ln(sb, "xref");
        Ln(sb, "0 6");
        Ln(sb, "0000000000 65535 f ");
        foreach (var o in offsets) Ln(sb, $"{o:D10} 00000 n ");
        Ln(sb, "trailer");
        Ln(sb, "<< /Size 6 /Root 1 0 R >>");
        Ln(sb, "startxref");
        Ln(sb, xrefOffset.ToString());
        sb.Append("%%EOF");
        return Encoding.Latin1.GetBytes(sb.ToString());
    }

    /// <summary>
    ///     Generates a PDF 1.5-style document that uses a compressed /XRef stream
    ///     instead of a traditional xref table. Tests the cross-reference stream
    ///     parser (ISO 32000-1 §7.5.8).
    ///     W = [1, 4, 2]: 1-byte type, 4-byte offset, 2-byte generation.
    /// </summary>
    public static byte[] WithCompressedXref(int pageCount = 1) =>
        BuildWithXrefStream(pageCount);

    /// <summary>
    ///     Generates a single-page PDF whose page content stream contains a simple
    ///     text block. Used to test content stream parsing end-to-end.
    /// </summary>
    public static byte[] WithTextContent(string text = "Hello Unchained") =>
        BuildWithContent($"BT /F1 12 Tf 100 700 Td ({EscapeString(text)}) Tj ET");

    /// <summary>
    ///     Generates a single-page PDF whose page content stream is exactly
    ///     <paramref name="contentStream" />. Lets tests inject arbitrary operators
    ///     (e.g. <c>cm</c> transforms) to exercise the content/text state machine.
    /// </summary>
    public static byte[] WithRawContent(string contentStream) =>
        BuildWithContent(contentStream);

    /// <summary>
    ///     Generates a single-page PDF containing two separate image XObject stream
    ///     objects whose binary pixel data is identical. Used to exercise
    ///     <c>OptimizeResourcesAsync</c> duplicate-stream deduplication.
    /// </summary>
    public static byte[] WithDuplicateImageStreams(int width, int height, byte[] rgbData) =>
        BuildWithDuplicateImageStreams(width, height, rgbData);

    /// <summary>
    ///     Generates a single-page PDF containing two separate font-program stream
    ///     objects with identical binary content. Used to exercise
    ///     <c>OptimizeResourcesAsync</c> duplicate-stream deduplication.
    /// </summary>
    public static byte[] WithDuplicateFontStreams(byte[] fontData) =>
        BuildWithDuplicateFontStreams(fontData);

    private static string EscapeString(string s) =>
        s
            .Replace("\\", @"\\")
            .Replace("(", "\\(")
            .Replace(")", "\\)")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t")
            .Replace("\0", "\\0");

    /// <summary>
    ///     Generates a single-page PDF whose page paints a vertical black→white axial shading
    ///     (ShadingType 2) across the whole page via the <c>sh</c> operator. Used to exercise
    ///     gradient rendering. Domain 0..1; Coords map the gradient from the page bottom (black)
    ///     to the top (white).
    /// </summary>
    public static byte[] WithAxialShading()
    {
        var sb = new StringBuilder();
        var offsets = new List<int>();
        Ln(sb, "%PDF-1.7");
        Ln(sb, "%\xE2\xE3\xCF\xD3");

        offsets.Add(ByteLength(sb));
        Ln(sb, "1 0 obj");
        Ln(sb, "<< /Type /Catalog /Pages 2 0 R >>");
        Ln(sb, "endobj");

        offsets.Add(ByteLength(sb));
        Ln(sb, "2 0 obj");
        Ln(sb, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
        Ln(sb, "endobj");

        // Page with a /Shading resource named Sh1.
        offsets.Add(ByteLength(sb));
        Ln(sb, "3 0 obj");
        Ln(sb, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] /Contents 4 0 R");
        Ln(sb, "   /Resources << /Shading << /Sh1 5 0 R >> >> >>");
        Ln(sb, "endobj");

        // Content stream: paint the shading over the page.
        const string content = "q 0 0 100 100 re W n /Sh1 sh Q";
        var cb = Encoding.Latin1.GetBytes(content);
        offsets.Add(ByteLength(sb));
        Ln(sb, "4 0 obj");
        Ln(sb, $"<< /Length {cb.Length} >>");
        sb.Append("stream\n");
        sb.Append(content);
        Ln(sb, "\nendstream");
        Ln(sb, "endobj");

        // Axial shading: black at y=0 → white at y=100, FunctionType 2 (N=1).
        offsets.Add(ByteLength(sb));
        Ln(sb, "5 0 obj");
        Ln(sb, "<< /ShadingType 2 /ColorSpace /DeviceRGB /Coords [0 0 0 100] /Domain [0 1]");
        Ln(sb, "   /Function << /FunctionType 2 /Domain [0 1] /C0 [0 0 0] /C1 [1 1 1] /N 1 >>");
        Ln(sb, "   /Extend [true true] >>");
        Ln(sb, "endobj");

        var xrefOffset = ByteLength(sb);
        Ln(sb, "xref");
        Ln(sb, "0 6");
        Ln(sb, "0000000000 65535 f ");
        foreach (var o in offsets) Ln(sb, $"{o:D10} 00000 n ");
        Ln(sb, "trailer");
        Ln(sb, "<< /Size 6 /Root 1 0 R >>");
        Ln(sb, "startxref");
        Ln(sb, xrefOffset.ToString());
        sb.Append("%%EOF");
        return Encoding.Latin1.GetBytes(sb.ToString());
    }

    /// <summary>
    ///     Like <see cref="WithAxialShading" /> but activates a constant fill alpha (<c>/GS1 gs</c>
    ///     with <c>/ca 0.5</c>) before painting the shading, exercising the alpha-blend branch of the
    ///     gradient rasteriser.
    /// </summary>
    public static byte[] WithAxialShadingAlpha()
    {
        var sb = new StringBuilder();
        var offsets = new List<int>();
        Ln(sb, "%PDF-1.7");

        offsets.Add(ByteLength(sb));
        Ln(sb, "1 0 obj");
        Ln(sb, "<< /Type /Catalog /Pages 2 0 R >>");
        Ln(sb, "endobj");

        offsets.Add(ByteLength(sb));
        Ln(sb, "2 0 obj");
        Ln(sb, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
        Ln(sb, "endobj");

        offsets.Add(ByteLength(sb));
        Ln(sb, "3 0 obj");
        Ln(sb, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] /Contents 4 0 R");
        Ln(sb, "   /Resources << /Shading << /Sh1 5 0 R >> /ExtGState << /GS1 6 0 R >> >> >>");
        Ln(sb, "endobj");

        const string content = "q /GS1 gs 0 0 100 100 re W n /Sh1 sh Q";
        var cb = Encoding.Latin1.GetBytes(content);
        offsets.Add(ByteLength(sb));
        Ln(sb, "4 0 obj");
        Ln(sb, $"<< /Length {cb.Length} >>");
        sb.Append("stream\n");
        sb.Append(content);
        Ln(sb, "\nendstream");
        Ln(sb, "endobj");

        offsets.Add(ByteLength(sb));
        Ln(sb, "5 0 obj");
        Ln(sb, "<< /ShadingType 2 /ColorSpace /DeviceRGB /Coords [0 0 0 100] /Domain [0 1]");
        Ln(sb, "   /Function << /FunctionType 2 /Domain [0 1] /C0 [0 0 0] /C1 [1 1 1] /N 1 >>");
        Ln(sb, "   /Extend [true true] >>");
        Ln(sb, "endobj");

        offsets.Add(ByteLength(sb));
        Ln(sb, "6 0 obj");
        Ln(sb, "<< /Type /ExtGState /ca 0.5 >>");
        Ln(sb, "endobj");

        var xrefOffset2 = ByteLength(sb);
        Ln(sb, "xref");
        Ln(sb, "0 7");
        Ln(sb, "0000000000 65535 f ");
        foreach (var o in offsets) Ln(sb, $"{o:D10} 00000 n ");
        Ln(sb, "trailer");
        Ln(sb, "<< /Size 7 /Root 1 0 R >>");
        Ln(sb, "startxref");
        Ln(sb, xrefOffset2.ToString());
        sb.Append("%%EOF");
        return Encoding.Latin1.GetBytes(sb.ToString());
    }

    /// <summary>
    ///     Generates a single-page PDF that fills the page with a coloured tiling pattern
    ///     (PatternType 1, PaintType 1): each 10×10 cell paints a solid red square. Used to
    ///     exercise tiling-pattern rendering.
    /// </summary>
    public static byte[] WithTilingPattern()
    {
        var sb = new StringBuilder();
        var offsets = new List<int>();
        Ln(sb, "%PDF-1.7");
        Ln(sb, "%\xE2\xE3\xCF\xD3");

        offsets.Add(ByteLength(sb));
        Ln(sb, "1 0 obj");
        Ln(sb, "<< /Type /Catalog /Pages 2 0 R >>");
        Ln(sb, "endobj");
        offsets.Add(ByteLength(sb));
        Ln(sb, "2 0 obj");
        Ln(sb, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
        Ln(sb, "endobj");

        // Page with a /Pattern resource and a /Pattern colour space.
        offsets.Add(ByteLength(sb));
        Ln(sb, "3 0 obj");
        Ln(sb, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] /Contents 4 0 R");
        Ln(sb, "   /Resources << /Pattern << /P1 5 0 R >> >> >>");
        Ln(sb, "endobj");

        // Content: set the pattern fill colour space, select /P1, fill the page rect.
        const string content = "/Pattern cs /P1 scn 0 0 100 100 re f";
        var cb = Encoding.Latin1.GetBytes(content);
        offsets.Add(ByteLength(sb));
        Ln(sb, "4 0 obj");
        Ln(sb, $"<< /Length {cb.Length} >>");
        sb.Append("stream\n");
        sb.Append(content);
        Ln(sb, "\nendstream");
        Ln(sb, "endobj");

        // Tiling pattern cell: a 10×10 solid red square.
        const string cell = "1 0 0 rg 0 0 10 10 re f";
        var cellBytes = Encoding.Latin1.GetBytes(cell);
        offsets.Add(ByteLength(sb));
        Ln(sb, "5 0 obj");
        Ln(sb, "<< /Type /Pattern /PatternType 1 /PaintType 1 /TilingType 1");
        Ln(sb, "   /BBox [0 0 10 10] /XStep 10 /YStep 10 /Resources << >>");
        Ln(sb, $"   /Length {cellBytes.Length} >>");
        sb.Append("stream\n");
        sb.Append(cell);
        Ln(sb, "\nendstream");
        Ln(sb, "endobj");

        var xrefOffset = ByteLength(sb);
        Ln(sb, "xref");
        Ln(sb, "0 6");
        Ln(sb, "0000000000 65535 f ");
        foreach (var o in offsets) Ln(sb, $"{o:D10} 00000 n ");
        Ln(sb, "trailer");
        Ln(sb, "<< /Size 6 /Root 1 0 R >>");
        Ln(sb, "startxref");
        Ln(sb, xrefOffset.ToString());
        sb.Append("%%EOF");
        return Encoding.Latin1.GetBytes(sb.ToString());
    }

    /// <summary>
    ///     A one-page document with two optional content groups (layers, ISO 32000-1 §8.11):
    ///     "Layer One" (obj 5, ON by default) and "Layer Two" (obj 6, OFF by default — listed in
    ///     the default configuration's <c>/OFF</c> array). <c>/OCProperties</c> lives in the catalog.
    /// </summary>
    public static byte[] WithOptionalContentGroups()
    {
        var sb = new StringBuilder();
        var offsets = new List<int>();
        Ln(sb, "%PDF-1.7");
        Ln(sb, "%\xE2\xE3\xCF\xD3");

        // Catalog carries /OCProperties inline: both OCGs listed, Layer Two OFF by default.
        offsets.Add(ByteLength(sb));
        Ln(sb, "1 0 obj");
        Ln(sb, "<< /Type /Catalog /Pages 2 0 R");
        Ln(sb, "   /OCProperties << /OCGs [5 0 R 6 0 R] /D << /ON [5 0 R] /OFF [6 0 R] >> >> >>");
        Ln(sb, "endobj");

        offsets.Add(ByteLength(sb));
        Ln(sb, "2 0 obj");
        Ln(sb, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
        Ln(sb, "endobj");

        offsets.Add(ByteLength(sb));
        Ln(sb, "3 0 obj");
        Ln(sb, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] /Contents 4 0 R");
        Ln(sb, "   /Resources << /Properties << /MC0 5 0 R /MC1 6 0 R >> >> >>");
        Ln(sb, "endobj");

        const string content = "/OC /MC0 BDC 0 0 50 50 re f EMC /OC /MC1 BDC 50 50 50 50 re f EMC";
        var cb = Encoding.Latin1.GetBytes(content);
        offsets.Add(ByteLength(sb));
        Ln(sb, "4 0 obj");
        Ln(sb, $"<< /Length {cb.Length} >>");
        sb.Append("stream\n");
        sb.Append(content);
        Ln(sb, "\nendstream");
        Ln(sb, "endobj");

        offsets.Add(ByteLength(sb));
        Ln(sb, "5 0 obj");
        Ln(sb, "<< /Type /OCG /Name (Layer One) >>");
        Ln(sb, "endobj");
        offsets.Add(ByteLength(sb));
        Ln(sb, "6 0 obj");
        Ln(sb, "<< /Type /OCG /Name (Layer Two) >>");
        Ln(sb, "endobj");

        var xrefOffset = ByteLength(sb);
        Ln(sb, "xref");
        Ln(sb, "0 7");
        Ln(sb, "0000000000 65535 f ");
        foreach (var o in offsets) Ln(sb, $"{o:D10} 00000 n ");
        Ln(sb, "trailer");
        Ln(sb, "<< /Size 7 /Root 1 0 R >>");
        Ln(sb, "startxref");
        Ln(sb, xrefOffset.ToString());
        sb.Append("%%EOF");
        return Encoding.Latin1.GetBytes(sb.ToString());
    }

    /// <summary>
    ///     Like the inline OCG fixture but with <c>/OCProperties</c> held in its own indirect object
    ///     (obj 7) referenced from the catalog. Exercises the indirect-reference rebuild path.
    /// </summary>
    public static byte[] WithIndirectOptionalContentProperties()
    {
        var sb = new StringBuilder();
        var offsets = new List<int>();
        Ln(sb, "%PDF-1.7");

        offsets.Add(ByteLength(sb));
        Ln(sb, "1 0 obj");
        Ln(sb, "<< /Type /Catalog /Pages 2 0 R /OCProperties 7 0 R >>");
        Ln(sb, "endobj");

        offsets.Add(ByteLength(sb));
        Ln(sb, "2 0 obj");
        Ln(sb, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
        Ln(sb, "endobj");

        offsets.Add(ByteLength(sb));
        Ln(sb, "3 0 obj");
        Ln(sb, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] /Contents 4 0 R");
        Ln(sb, "   /Resources << /Properties << /MC0 5 0 R /MC1 6 0 R >> >> >>");
        Ln(sb, "endobj");

        const string content = "/OC /MC0 BDC 0 0 50 50 re f EMC";
        var cb = Encoding.Latin1.GetBytes(content);
        offsets.Add(ByteLength(sb));
        Ln(sb, "4 0 obj");
        Ln(sb, $"<< /Length {cb.Length} >>");
        sb.Append("stream\n");
        sb.Append(content);
        Ln(sb, "\nendstream");
        Ln(sb, "endobj");

        offsets.Add(ByteLength(sb));
        Ln(sb, "5 0 obj");
        Ln(sb, "<< /Type /OCG /Name (Layer One) >>");
        Ln(sb, "endobj");
        offsets.Add(ByteLength(sb));
        Ln(sb, "6 0 obj");
        Ln(sb, "<< /Type /OCG /Name (Layer Two) >>");
        Ln(sb, "endobj");

        offsets.Add(ByteLength(sb));
        Ln(sb, "7 0 obj");
        Ln(sb, "<< /OCGs [5 0 R 6 0 R] /D << /ON [5 0 R] /OFF [6 0 R] >> >>");
        Ln(sb, "endobj");

        var xrefOffset = ByteLength(sb);
        Ln(sb, "xref");
        Ln(sb, "0 8");
        Ln(sb, "0000000000 65535 f ");
        foreach (var o in offsets) Ln(sb, $"{o:D10} 00000 n ");
        Ln(sb, "trailer");
        Ln(sb, "<< /Size 8 /Root 1 0 R >>");
        Ln(sb, "startxref");
        Ln(sb, xrefOffset.ToString());
        sb.Append("%%EOF");
        return Encoding.Latin1.GetBytes(sb.ToString());
    }

    /// <summary>
    ///     A document whose <c>/OCProperties</c> has OCGs but no default configuration <c>/D</c>.
    ///     Toggling layer visibility must throw because there is no config to rewrite.
    /// </summary>
    public static byte[] WithOptionalContentNoDefaultConfig()
    {
        var sb = new StringBuilder();
        var offsets = new List<int>();
        Ln(sb, "%PDF-1.7");

        offsets.Add(ByteLength(sb));
        Ln(sb, "1 0 obj");
        Ln(sb, "<< /Type /Catalog /Pages 2 0 R /OCProperties << /OCGs [5 0 R] >> >>");
        Ln(sb, "endobj");

        offsets.Add(ByteLength(sb));
        Ln(sb, "2 0 obj");
        Ln(sb, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
        Ln(sb, "endobj");

        offsets.Add(ByteLength(sb));
        Ln(sb, "3 0 obj");
        Ln(sb, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] >>");
        Ln(sb, "endobj");

        offsets.Add(ByteLength(sb));
        Ln(sb, "5 0 obj");
        Ln(sb, "<< /Type /OCG /Name (Layer One) >>");
        Ln(sb, "endobj");

        var xrefOffset = ByteLength(sb);
        Ln(sb, "xref");
        Ln(sb, "0 6");
        Ln(sb, "0000000000 65535 f ");
        Ln(sb, $"{offsets[0]:D10} 00000 n ");
        Ln(sb, $"{offsets[1]:D10} 00000 n ");
        Ln(sb, $"{offsets[2]:D10} 00000 n ");
        Ln(sb, "0000000000 65535 f ");
        Ln(sb, $"{offsets[3]:D10} 00000 n ");
        Ln(sb, "trailer");
        Ln(sb, "<< /Size 6 /Root 1 0 R >>");
        Ln(sb, "startxref");
        Ln(sb, xrefOffset.ToString());
        sb.Append("%%EOF");
        return Encoding.Latin1.GetBytes(sb.ToString());
    }

    /// <summary>
    ///     A one-page document whose content invokes a Form XObject via a <c>Do</c> operator. The
    ///     form (obj 5) carries a <c>/Matrix</c> and its own content. Exercises the form-XObject
    ///     expansion path in <c>PageContentReader</c> (q/cm/&lt;form&gt;/Q inlining).
    /// </summary>
    public static byte[] WithFormXObjectDo()
    {
        var sb = new StringBuilder();
        var offsets = new List<int>();
        Ln(sb, "%PDF-1.7");

        offsets.Add(ByteLength(sb));
        Ln(sb, "1 0 obj");
        Ln(sb, "<< /Type /Catalog /Pages 2 0 R >>");
        Ln(sb, "endobj");

        offsets.Add(ByteLength(sb));
        Ln(sb, "2 0 obj");
        Ln(sb, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
        Ln(sb, "endobj");

        offsets.Add(ByteLength(sb));
        Ln(sb, "3 0 obj");
        Ln(sb, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] /Contents 4 0 R");
        Ln(sb, "   /Resources << /XObject << /Fm0 5 0 R >> >> >>");
        Ln(sb, "endobj");

        const string content = "q 1 0 0 1 10 10 cm /Fm0 Do Q";
        var cb = Encoding.Latin1.GetBytes(content);
        offsets.Add(ByteLength(sb));
        Ln(sb, "4 0 obj");
        Ln(sb, $"<< /Length {cb.Length} >>");
        sb.Append("stream\n");
        sb.Append(content);
        Ln(sb, "\nendstream");
        Ln(sb, "endobj");

        const string formContent = "0 0 50 50 re f";
        var fc = Encoding.Latin1.GetBytes(formContent);
        offsets.Add(ByteLength(sb));
        Ln(sb, "5 0 obj");
        Ln(sb, $"<< /Type /XObject /Subtype /Form /BBox [0 0 50 50] /Matrix [1 0 0 1 0 0] /Length {fc.Length} >>");
        sb.Append("stream\n");
        sb.Append(formContent);
        Ln(sb, "\nendstream");
        Ln(sb, "endobj");

        var xrefOffset = ByteLength(sb);
        Ln(sb, "xref");
        Ln(sb, "0 6");
        Ln(sb, "0000000000 65535 f ");
        foreach (var o in offsets) Ln(sb, $"{o:D10} 00000 n ");
        Ln(sb, "trailer");
        Ln(sb, "<< /Size 6 /Root 1 0 R >>");
        Ln(sb, "startxref");
        Ln(sb, xrefOffset.ToString());
        sb.Append("%%EOF");
        return Encoding.Latin1.GetBytes(sb.ToString());
    }

    /// <summary>
    ///     A one-page document whose <c>/Contents</c> is an array of two stream objects (§7.8.1).
    ///     Exercises the multi-stream concatenation path in <c>PageContentReader</c>.
    /// </summary>
    public static byte[] WithContentStreamArray()
    {
        var sb = new StringBuilder();
        var offsets = new List<int>();
        Ln(sb, "%PDF-1.7");

        offsets.Add(ByteLength(sb));
        Ln(sb, "1 0 obj");
        Ln(sb, "<< /Type /Catalog /Pages 2 0 R >>");
        Ln(sb, "endobj");

        offsets.Add(ByteLength(sb));
        Ln(sb, "2 0 obj");
        Ln(sb, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
        Ln(sb, "endobj");

        offsets.Add(ByteLength(sb));
        Ln(sb, "3 0 obj");
        Ln(sb, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] /Contents [4 0 R 5 0 R] >>");
        Ln(sb, "endobj");

        const string c1 = "0 0 50 50 re f";
        var c1B = Encoding.Latin1.GetBytes(c1);
        offsets.Add(ByteLength(sb));
        Ln(sb, "4 0 obj");
        Ln(sb, $"<< /Length {c1B.Length} >>");
        sb.Append("stream\n");
        sb.Append(c1);
        Ln(sb, "\nendstream");
        Ln(sb, "endobj");

        const string c2 = "50 50 50 50 re f";
        var c2B = Encoding.Latin1.GetBytes(c2);
        offsets.Add(ByteLength(sb));
        Ln(sb, "5 0 obj");
        Ln(sb, $"<< /Length {c2B.Length} >>");
        sb.Append("stream\n");
        sb.Append(c2);
        Ln(sb, "\nendstream");
        Ln(sb, "endobj");

        var xrefOffset = ByteLength(sb);
        Ln(sb, "xref");
        Ln(sb, "0 6");
        Ln(sb, "0000000000 65535 f ");
        foreach (var o in offsets) Ln(sb, $"{o:D10} 00000 n ");
        Ln(sb, "trailer");
        Ln(sb, "<< /Size 6 /Root 1 0 R >>");
        Ln(sb, "startxref");
        Ln(sb, xrefOffset.ToString());
        sb.Append("%%EOF");
        return Encoding.Latin1.GetBytes(sb.ToString());
    }

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
    }

    private static byte[] Build(int pageCount, string? title = null, string? author = null)
    {
        var sb = new StringBuilder();
        var offsets = new List<int>();

        Ln(sb, "%PDF-1.7");
        Ln(sb, "%\xE2\xE3\xCF\xD3");

        // Object 1 — Catalog
        offsets.Add(ByteLength(sb));
        Ln(sb, "1 0 obj");
        Ln(sb, "<< /Type /Catalog /Pages 2 0 R >>");
        Ln(sb, "endobj");

        // Object 2 — Pages
        offsets.Add(ByteLength(sb));
        var kids = string.Join(" ", Enumerable.Range(3, pageCount).Select(static n => $"{n} 0 R"));
        Ln(sb, "2 0 obj");
        Ln(sb, $"<< /Type /Pages /Kids [{kids}] /Count {pageCount} >>");
        Ln(sb, "endobj");

        // Objects 3..N — Page nodes
        for (var i = 0; i < pageCount; i++)
        {
            offsets.Add(ByteLength(sb));
            Ln(sb, $"{3 + i} 0 obj");
            Ln(sb, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] >>");
            Ln(sb, "endobj");
        }

        // Optional Info dictionary
        var infoRef = string.Empty;
        if (title is not null || author is not null)
        {
            var infoObjNum = 3 + pageCount;
            offsets.Add(ByteLength(sb));
            Ln(sb, $"{infoObjNum} 0 obj");
            var titleEntry = title is not null ? $" /Title ({title})" : string.Empty;
            var authorEntry = author is not null ? $" /Author ({author})" : string.Empty;
            Ln(sb, $"<<{titleEntry}{authorEntry} >>");
            Ln(sb, "endobj");
            infoRef = $" /Info {infoObjNum} 0 R";
        }

        var totalObjects = 3 + pageCount + (infoRef.Length > 0 ? 1 : 0);

        // xref
        var xrefOffset = ByteLength(sb);
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
    }

    private static int ByteLength(StringBuilder sb) => Encoding.Latin1.GetByteCount(sb.ToString());

    // ── PDF with compressed /XRef stream ─────────────────────────────────────

    private static byte[] BuildWithXrefStream(int pageCount)
    {
        // Phase 1: write the body objects and record their offsets.
        var sb = new StringBuilder();
        var offsets = new List<int>(); // offsets[i] = byte offset of object (i+1)

        Ln(sb, "%PDF-1.5");
        Ln(sb, "%\xE2\xE3\xCF\xD3");

        // Object 1 — Catalog
        offsets.Add(Len(sb));
        Ln(sb, "1 0 obj");
        Ln(sb, "<< /Type /Catalog /Pages 2 0 R >>");
        Ln(sb, "endobj");

        // Object 2 — Pages
        offsets.Add(Len(sb));
        var kids = string.Join(" ", Enumerable.Range(3, pageCount).Select(static n => $"{n} 0 R"));
        Ln(sb, "2 0 obj");
        Ln(sb, $"<< /Type /Pages /Kids [{kids}] /Count {pageCount} >>");
        Ln(sb, "endobj");

        // Objects 3..N — Page nodes
        for (var i = 0; i < pageCount; i++)
        {
            offsets.Add(Len(sb));
            Ln(sb, $"{3 + i} 0 obj");
            Ln(sb, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] >>");
            Ln(sb, "endobj");
        }

        // Phase 2: build the binary xref stream data.
        // ReSharper disable once GrammarMistakeInComment
        // Object numbering: 0=free, 1..N=body objects, N+1=the xref stream itself.
        var totalObjects = 3 + pageCount + 1; // +1 for the xref stream object itself
        var xrefStreamObjNum = 2 + pageCount + 1;
        var xrefStreamOffset = Len(sb); // offset where the xref stream object will start

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
        Ln(sb, $"{xrefStreamObjNum} 0 obj");
        Ln(sb, $"<< /Type /XRef /Size {totalObjects} /W [{w0} {w1} {w2}] /Filter /FlateDecode /Length {compressed.Length} /Root 1 0 R >>");
        sb.Append("stream\n");
        var bodyBytes = Encoding.Latin1.GetBytes(sb.ToString());
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
    }

    private static byte[] BuildWithAnnotation(string contents)
    {
        var sb = new StringBuilder();
        var offsets = new List<int>();

        Ln(sb, "%PDF-1.7");
        Ln(sb, "%\xE2\xE3\xCF\xD3");

        // Object 1 — Catalog
        offsets.Add(ByteLength(sb));
        Ln(sb, "1 0 obj");
        Ln(sb, "<< /Type /Catalog /Pages 2 0 R >>");
        Ln(sb, "endobj");

        // Object 2 — Pages
        offsets.Add(ByteLength(sb));
        Ln(sb, "2 0 obj");
        Ln(sb, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
        Ln(sb, "endobj");

        // Object 3 — Page (with /Annots)
        offsets.Add(ByteLength(sb));
        Ln(sb, "3 0 obj");
        Ln(sb, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Annots [4 0 R] >>");
        Ln(sb, "endobj");

        // Object 4 — Annotation
        var escaped = EscapeString(contents);
        offsets.Add(ByteLength(sb));
        Ln(sb, "4 0 obj");
        Ln(sb, $"<< /Type /Annot /Subtype /Text /Rect [50 700 100 750] /Contents ({escaped}) >>");
        Ln(sb, "endobj");

        var xrefOffset = ByteLength(sb);
        Ln(sb, "xref");
        Ln(sb, "0 5");
        Ln(sb, "0000000000 65535 f ");
        foreach (var o in offsets) Ln(sb, $"{o:D10} 00000 n ");
        Ln(sb, "trailer");
        Ln(sb, "<< /Size 5 /Root 1 0 R >>");
        Ln(sb, "startxref");
        Ln(sb, xrefOffset.ToString());
        sb.Append("%%EOF");

        return Encoding.Latin1.GetBytes(sb.ToString());
    }

    private static byte[] BuildWithOutlines(IEnumerable<(string title, int page)> bookmarks) =>
        BuildWithOutlinesFinal(bookmarks.ToList());

    private static byte[] BuildWithOutlinesFinal(IReadOnlyList<(string title, int page)> bms)
    {
        var sb = new StringBuilder();
        var offsets = new List<int>();

        // Fixed layout: catalog=1, pages=2, outlinesRoot=3, items=4..(3+N), page1=(4+N), page2=(5+N)
        var page1Obj = 4 + bms.Count;
        var page2Obj = 5 + bms.Count;
        var totalObjects = page2Obj + 1;

        Ln(sb, "%PDF-1.7");
        Ln(sb, "%\xE2\xE3\xCF\xD3");

        offsets.Add(ByteLength(sb));
        Ln(sb, "1 0 obj");
        Ln(sb, "<< /Type /Catalog /Pages 2 0 R /Outlines 3 0 R >>");
        Ln(sb, "endobj");

        offsets.Add(ByteLength(sb));
        Ln(sb, "2 0 obj");
        Ln(sb, $"<< /Type /Pages /Kids [{page1Obj} 0 R {page2Obj} 0 R] /Count 2 >>");
        Ln(sb, "endobj");

        var first = bms.Count > 0 ? "4 0 R" : "null";
        var last = bms.Count > 0 ? $"{3 + bms.Count} 0 R" : "null";
        offsets.Add(ByteLength(sb));
        Ln(sb, "3 0 obj");
        Ln(sb, $"<< /Type /Outlines /Count {bms.Count} /First {first} /Last {last} >>");
        Ln(sb, "endobj");

        for (var i = 0; i < bms.Count; i++)
        {
            var num = 4 + i;
            var (title, pageNum) = bms[i];
            var pageObjNum = pageNum == 1 ? page1Obj : page2Obj;
            var prev = i > 0 ? $" /Prev {num - 1} 0 R" : string.Empty;
            var next = i < bms.Count - 1 ? $" /Next {num + 1} 0 R" : string.Empty;
            offsets.Add(ByteLength(sb));
            Ln(sb, $"{num} 0 obj");
            Ln(sb, $"<< /Title ({EscapeString(title)}) /Parent 3 0 R /Dest [{pageObjNum} 0 R /Fit]{prev}{next} >>");
            Ln(sb, "endobj");
        }

        offsets.Add(ByteLength(sb));
        Ln(sb, $"{page1Obj} 0 obj");
        Ln(sb, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] >>");
        Ln(sb, "endobj");

        offsets.Add(ByteLength(sb));
        Ln(sb, $"{page2Obj} 0 obj");
        Ln(sb, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] >>");
        Ln(sb, "endobj");

        var xrefOffset = ByteLength(sb);
        Ln(sb, "xref");
        Ln(sb, $"0 {totalObjects}");
        Ln(sb, "0000000000 65535 f ");
        foreach (var o in offsets) Ln(sb, $"{o:D10} 00000 n ");
        Ln(sb, "trailer");
        Ln(sb, $"<< /Size {totalObjects} /Root 1 0 R >>");
        Ln(sb, "startxref");
        Ln(sb, xrefOffset.ToString());
        sb.Append("%%EOF");

        return Encoding.Latin1.GetBytes(sb.ToString());
    }

    private static byte[] BuildWithAcroForm(string fieldName, string fieldValue)
    {
        var sb = new StringBuilder();
        var offsets = new List<int>();

        Ln(sb, "%PDF-1.7");
        Ln(sb, "%\xE2\xE3\xCF\xD3");

        // Object 1 — Catalog (with /AcroForm)
        offsets.Add(ByteLength(sb));
        Ln(sb, "1 0 obj");
        Ln(sb, "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [4 0 R] >> >>");
        Ln(sb, "endobj");

        // Object 2 — Pages
        offsets.Add(ByteLength(sb));
        Ln(sb, "2 0 obj");
        Ln(sb, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
        Ln(sb, "endobj");

        // Object 3 — Page
        offsets.Add(ByteLength(sb));
        Ln(sb, "3 0 obj");
        Ln(sb, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Annots [4 0 R] >>");
        Ln(sb, "endobj");

        // Object 4 — Text field (also the widget annotation for it)
        var escapedName = EscapeString(fieldName);
        var escapedValue = EscapeString(fieldValue);
        offsets.Add(ByteLength(sb));
        Ln(sb, "4 0 obj");
        Ln(
            sb,
            $"<< /Type /Annot /Subtype /Widget /FT /Tx /T ({escapedName}) /V ({escapedValue}) /Rect [50 700 300 720] /P 3 0 R >>"
        );
        Ln(sb, "endobj");

        var xrefOffset = ByteLength(sb);
        Ln(sb, "xref");
        Ln(sb, "0 5");
        Ln(sb, "0000000000 65535 f ");
        foreach (var o in offsets) Ln(sb, $"{o:D10} 00000 n ");
        Ln(sb, "trailer");
        Ln(sb, "<< /Size 5 /Root 1 0 R >>");
        Ln(sb, "startxref");
        Ln(sb, xrefOffset.ToString());
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

        Ln(sb, "%PDF-1.7");
        Ln(sb, "%\xE2\xE3\xCF\xD3");

        var o1 = Len(sb);
        Ln(sb, "1 0 obj");
        Ln(sb, "<< /Type /Catalog /Pages 2 0 R >>");
        Ln(sb, "endobj");
        var o2 = Len(sb);
        Ln(sb, "2 0 obj");
        Ln(sb, "<< /Type /Pages /Kids [7 0 R] /Count 1 >>");
        Ln(sb, "endobj");

        // Object 3 — content stream
        var o3 = Len(sb);
        Ln(sb, "3 0 obj");
        Ln(sb, $"<< /Length {Encoding.Latin1.GetByteCount(contentStream)} >>");
        sb.Append("stream\n").Append(contentStream).Append("\nendstream\n");
        Ln(sb, "endobj");

        // Object 4 — FontDescriptor
        var o4 = Len(sb);
        Ln(sb, "4 0 obj");
        Ln(sb, "<< /Type /FontDescriptor /FontName /TestFont /Flags 32 /FontFile2 5 0 R >>");
        Ln(sb, "endobj");

        // Object 5 — embedded font stream (hex-encoded, ASCIIHexDecode)
        var o5 = Len(sb);
        Ln(sb, "5 0 obj");
        Ln(sb, $"<< /Length {hexLen} /Length1 {fontBytes.Length} /Filter /ASCIIHexDecode >>");
        sb.Append("stream\n").Append(hexStream).Append("\nendstream\n");
        Ln(sb, "endobj");

        // Object 6 — Font
        var o6 = Len(sb);
        Ln(sb, "6 0 obj");
        Ln(sb, "<< /Type /Font /Subtype /TrueType /BaseFont /TestFont /FontDescriptor 4 0 R >>");
        Ln(sb, "endobj");

        // Object 7 — Page
        var o7 = Len(sb);
        Ln(sb, "7 0 obj");
        Ln(sb, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Contents 3 0 R");
        Ln(sb, "   /Resources << /Font << /F1 6 0 R >> >> >>");
        Ln(sb, "endobj");

        var xref = Len(sb);
        Ln(sb, "xref");
        Ln(sb, "0 8");
        Ln(sb, "0000000000 65535 f ");
        foreach (var o in new[] { o1, o2, o3, o4, o5, o6, o7 })
            Ln(sb, $"{o:D10} 00000 n ");
        Ln(sb, "trailer");
        Ln(sb, "<< /Size 8 /Root 1 0 R >>");
        Ln(sb, "startxref");
        Ln(sb, xref.ToString());
        sb.Append("%%EOF");

        return Encoding.Latin1.GetBytes(sb.ToString());
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
        Line(ms, "%PDF-1.7");
        Line(ms, "%\xE2\xE3\xCF\xD3");

        var o1 = Pos(ms);
        Line(ms, "1 0 obj");
        Line(ms, "<< /Type /Catalog /Pages 2 0 R >>");
        Line(ms, "endobj");
        var o2 = Pos(ms);
        Line(ms, "2 0 obj");
        Line(ms, "<< /Type /Pages /Kids [5 0 R] /Count 1 >>");
        Line(ms, "endobj");

        // Object 3 — content stream
        var o3 = Pos(ms);
        Line(ms, "3 0 obj");
        Line(ms, $"<< /Length {csBytes.Length} >>");
        Line(ms, "stream");
        Binary(ms, csBytes);
        Line(ms, string.Empty);
        Line(ms, "endstream");
        Line(ms, "endobj");

        // Object 4 — Image XObject (FlateDecode, binary stream)
        var o4 = Pos(ms);
        Line(ms, "4 0 obj");
        Line(ms, $"<< /Type /XObject /Subtype /Image /Width {width} /Height {height}");
        Line(ms, $"   /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /FlateDecode /Length {compressed.Length} >>");
        Line(ms, "stream");
        Binary(ms, compressed);
        Line(ms, string.Empty);
        Line(ms, "endstream");
        Line(ms, "endobj");

        // Object 5 — Page
        var o5 = Pos(ms);
        Line(ms, "5 0 obj");
        Line(ms, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Contents 3 0 R");
        Line(ms, "   /Resources << /XObject << /Im1 4 0 R >> >> >>");
        Line(ms, "endobj");

        var xref = Pos(ms);
        Line(ms, "xref");
        Line(ms, "0 6");
        Line(ms, "0000000000 65535 f ");
        foreach (var o in new[] { o1, o2, o3, o4, o5 })
            Line(ms, $"{o:D10} 00000 n ");
        Line(ms, "trailer");
        Line(ms, "<< /Size 6 /Root 1 0 R >>");
        Line(ms, "startxref");
        Line(ms, xref.ToString());
        Text(ms, "%%EOF");

        return ms.ToArray();
    }

    private static byte[] BuildWithMultipleAcroFormFields(IReadOnlyList<(string name, string value)> fields)
    {
        // Objects: 1=Catalog, 2=Pages, 3=Page, 4..(3+N)=field widgets
        var fieldCount = fields.Count;
        var fieldRefs = string.Join(" ", Enumerable.Range(4, fieldCount).Select(static n => $"{n} 0 R"));
        var sb = new StringBuilder();
        var offsets = new List<int>();

        Ln(sb, "%PDF-1.7");
        Ln(sb, "%\xE2\xE3\xCF\xD3");

        offsets.Add(ByteLength(sb));
        Ln(sb, "1 0 obj");
        Ln(sb, $"<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [{fieldRefs}] >> >>");
        Ln(sb, "endobj");

        offsets.Add(ByteLength(sb));
        Ln(sb, "2 0 obj");
        Ln(sb, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
        Ln(sb, "endobj");

        offsets.Add(ByteLength(sb));
        Ln(sb, "3 0 obj");
        Ln(sb, $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Annots [{fieldRefs}] >>");
        Ln(sb, "endobj");

        for (var i = 0; i < fieldCount; i++)
        {
            var (name, value) = fields[i];
            offsets.Add(ByteLength(sb));
            Ln(sb, $"{4 + i} 0 obj");
            Ln(
                sb,
                $"<< /Type /Annot /Subtype /Widget /FT /Tx /T ({EscapeString(name)}) /V ({EscapeString(value)}) /Rect [50 {700 - (i * 30)} 300 {720 - (i * 30)}] /P 3 0 R >>"
            );
            Ln(sb, "endobj");
        }

        var totalObjects = 4 + fieldCount;
        var xrefOffset = ByteLength(sb);
        Ln(sb, "xref");
        Ln(sb, $"0 {totalObjects}");
        Ln(sb, "0000000000 65535 f ");
        foreach (var o in offsets) Ln(sb, $"{o:D10} 00000 n ");
        Ln(sb, "trailer");
        Ln(sb, $"<< /Size {totalObjects} /Root 1 0 R >>");
        Ln(sb, "startxref");
        Ln(sb, xrefOffset.ToString());
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

        Ln(sb, "%PDF-1.7");
        Ln(sb, "%\xE2\xE3\xCF\xD3");

        // 1 — Catalog
        offsets.Add(ByteLength(sb));
        Ln(sb, "1 0 obj");
        Ln(sb, "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [4 0 R] >> >>");
        Ln(sb, "endobj");

        // 2 — Pages
        offsets.Add(ByteLength(sb));
        Ln(sb, "2 0 obj");
        Ln(sb, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
        Ln(sb, "endobj");

        // 3 — Page
        offsets.Add(ByteLength(sb));
        Ln(sb, "3 0 obj");
        Ln(sb, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Annots [5 0 R 6 0 R] >>");
        Ln(sb, "endobj");

        // 4 — Non-terminal group node (no /FT, has /Kids)
        offsets.Add(ByteLength(sb));
        Ln(sb, "4 0 obj");
        Ln(sb, "<< /T (Group) /Kids [5 0 R 6 0 R] >>");
        Ln(sb, "endobj");

        // 5 — Child field "First"
        offsets.Add(ByteLength(sb));
        Ln(sb, "5 0 obj");
        Ln(sb, "<< /Type /Annot /Subtype /Widget /FT /Tx /T (First) /V (v1) /Rect [50 700 300 720] /P 3 0 R >>");
        Ln(sb, "endobj");

        // 6 — Child field "Second"
        offsets.Add(ByteLength(sb));
        Ln(sb, "6 0 obj");
        Ln(sb, "<< /Type /Annot /Subtype /Widget /FT /Tx /T (Second) /V (v2) /Rect [50 660 300 680] /P 3 0 R >>");
        Ln(sb, "endobj");

        var xrefOffset = ByteLength(sb);
        Ln(sb, "xref");
        Ln(sb, "0 7");
        Ln(sb, "0000000000 65535 f ");
        foreach (var o in offsets) Ln(sb, $"{o:D10} 00000 n ");
        Ln(sb, "trailer");
        Ln(sb, "<< /Size 7 /Root 1 0 R >>");
        Ln(sb, "startxref");
        Ln(sb, xrefOffset.ToString());
        sb.Append("%%EOF");

        return Encoding.Latin1.GetBytes(sb.ToString());
    }

    private static byte[] BuildWithBtnAcroForm(string fieldName)
    {
        var sb = new StringBuilder();
        var offsets = new List<int>();

        Ln(sb, "%PDF-1.7");
        Ln(sb, "%\xE2\xE3\xCF\xD3");

        offsets.Add(ByteLength(sb));
        Ln(sb, "1 0 obj");
        Ln(sb, "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [4 0 R] >> >>");
        Ln(sb, "endobj");

        offsets.Add(ByteLength(sb));
        Ln(sb, "2 0 obj");
        Ln(sb, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
        Ln(sb, "endobj");

        offsets.Add(ByteLength(sb));
        Ln(sb, "3 0 obj");
        Ln(sb, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Annots [4 0 R] >>");
        Ln(sb, "endobj");

        offsets.Add(ByteLength(sb));
        Ln(sb, "4 0 obj");
        Ln(
            sb,
            $"<< /Type /Annot /Subtype /Widget /FT /Btn /T ({EscapeString(fieldName)}) /V /Off /Rect [50 700 70 720] /P 3 0 R >>"
        );
        Ln(sb, "endobj");

        var xrefOffset = ByteLength(sb);
        Ln(sb, "xref");
        Ln(sb, "0 5");
        Ln(sb, "0000000000 65535 f ");
        foreach (var o in offsets) Ln(sb, $"{o:D10} 00000 n ");
        Ln(sb, "trailer");
        Ln(sb, "<< /Size 5 /Root 1 0 R >>");
        Ln(sb, "startxref");
        Ln(sb, xrefOffset.ToString());
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

        Ln(sb, "%PDF-1.7");
        Ln(sb, "%\xE2\xE3\xCF\xD3");

        offsets.Add(ByteLength(sb));
        Ln(sb, "1 0 obj");
        Ln(sb, "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [5 0 R] >> >>");
        Ln(sb, "endobj");

        offsets.Add(ByteLength(sb));
        Ln(sb, "2 0 obj");
        Ln(sb, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
        Ln(sb, "endobj");

        offsets.Add(ByteLength(sb));
        Ln(sb, "3 0 obj");
        Ln(sb, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Annots [5 0 R] >>");
        Ln(sb, "endobj");

        // 4 — Normal appearance stream (XObject Form)
        offsets.Add(ByteLength(sb));
        Ln(sb, "4 0 obj");
        Ln(sb, $"<< /Type /XObject /Subtype /Form /BBox [0 0 250 20] /Length {apBytes.Length} >>");
        sb.Append("stream\n");
        sb.Append(apContent);
        Ln(sb, "\nendstream");
        Ln(sb, "endobj");

        // 5 — Text field widget with /AP referencing obj 4
        offsets.Add(ByteLength(sb));
        Ln(sb, "5 0 obj");
        Ln(
            sb,
            $"<< /Type /Annot /Subtype /Widget /FT /Tx /T ({EscapeString(fieldName)}) /V ({EscapeString(fieldValue)}) /Rect [50 700 300 720] /P 3 0 R /AP << /N 4 0 R >> >>"
        );
        Ln(sb, "endobj");

        var xrefOffset = ByteLength(sb);
        Ln(sb, "xref");
        Ln(sb, "0 6");
        Ln(sb, "0000000000 65535 f ");
        foreach (var o in offsets) Ln(sb, $"{o:D10} 00000 n ");
        Ln(sb, "trailer");
        Ln(sb, "<< /Size 6 /Root 1 0 R >>");
        Ln(sb, "startxref");
        Ln(sb, xrefOffset.ToString());
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

        Line(ms, "%PDF-1.7");
        Line(ms, "%\xE2\xE3\xCF\xD3");

        // Object 1 — Catalog
        var o1 = Pos(ms);
        Line(ms, "1 0 obj");
        Line(ms, "<< /Type /Catalog /Pages 2 0 R >>");
        Line(ms, "endobj");

        // Object 2 — Pages
        var o2 = Pos(ms);
        Line(ms, "2 0 obj");
        Line(ms, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
        Line(ms, "endobj");

        // Object 3 — content stream (paints Im1 only)
        var cs = $"q {width * 10} 0 0 {height * 10} 0 0 cm /Im1 Do Q";
        var csBytes = Encoding.Latin1.GetBytes(cs);
        var o3 = Pos(ms);
        Line(ms, "3 0 obj");
        Line(ms, $"<< /Length {csBytes.Length} >>");
        Line(ms, "stream");
        Binary(ms, csBytes);
        Line(ms, string.Empty);
        Line(ms, "endstream");
        Line(ms, "endobj");

        // Object 4 — first image XObject (raw, no filter)
        var o4 = Pos(ms);
        Line(ms, "4 0 obj");
        Line(ms, $"<< /Type /XObject /Subtype /Image /Width {width} /Height {height}");
        Line(ms, $"   /ColorSpace /DeviceRGB /BitsPerComponent 8 /Length {rgbData.Length} >>");
        Line(ms, "stream");
        Binary(ms, rgbData);
        Line(ms, string.Empty);
        Line(ms, "endstream");
        Line(ms, "endobj");

        // Object 5 — Page (references both Im1 and Im2 in Resources)
        var o5 = Pos(ms);
        Line(ms, "5 0 obj");
        Line(ms, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Contents 3 0 R");
        Line(ms, "   /Resources << /XObject << /Im1 4 0 R /Im2 6 0 R >> >> >>");
        Line(ms, "endobj");

        // Object 6 — second image XObject with identical pixel data
        var o6 = Pos(ms);
        Line(ms, "6 0 obj");
        Line(ms, $"<< /Type /XObject /Subtype /Image /Width {width} /Height {height}");
        Line(ms, $"   /ColorSpace /DeviceRGB /BitsPerComponent 8 /Length {rgbData.Length} >>");
        Line(ms, "stream");
        Binary(ms, rgbData);
        Line(ms, string.Empty);
        Line(ms, "endstream");
        Line(ms, "endobj");

        var xref = Pos(ms);
        Line(ms, "xref");
        Line(ms, "0 7");
        Line(ms, "0000000000 65535 f ");
        foreach (var o in new[] { o1, o2, o3, o4, o5, o6 })
            Line(ms, $"{o:D10} 00000 n ");
        Line(ms, "trailer");
        Line(ms, "<< /Size 7 /Root 1 0 R >>");
        Line(ms, "startxref");
        Line(ms, xref.ToString());
        Text(ms, "%%EOF");

        return ms.ToArray();
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

        Ln(sb, "%PDF-1.7");
        Ln(sb, "%\xE2\xE3\xCF\xD3");

        // Object 1 — Catalog
        offsets.Add(ByteLength(sb));
        Ln(sb, "1 0 obj");
        Ln(sb, "<< /Type /Catalog /Pages 2 0 R >>");
        Ln(sb, "endobj");

        // Object 2 — Pages
        offsets.Add(ByteLength(sb));
        Ln(sb, "2 0 obj");
        Ln(sb, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
        Ln(sb, "endobj");

        // Object 3 — Page (uses font F1)
        offsets.Add(ByteLength(sb));
        Ln(sb, "3 0 obj");
        Ln(sb, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842]");
        Ln(sb, "   /Resources << /Font << /F1 6 0 R /F2 9 0 R >> >> >>");
        Ln(sb, "endobj");

        // Object 4 — first font program stream (ASCIIHexDecode)
        offsets.Add(ByteLength(sb));
        Ln(sb, "4 0 obj");
        Ln(sb, $"<< /Length {hex1.Length} /Length1 {fontData.Length} /Filter /ASCIIHexDecode >>");
        sb.Append("stream\n").Append(hex1).Append("\nendstream\n");
        Ln(sb, "endobj");

        // Object 5 — first FontDescriptor
        offsets.Add(ByteLength(sb));
        Ln(sb, "5 0 obj");
        Ln(sb, "<< /Type /FontDescriptor /FontName /FontA /Flags 32 /FontFile2 4 0 R >>");
        Ln(sb, "endobj");

        // Object 6 — first Font
        offsets.Add(ByteLength(sb));
        Ln(sb, "6 0 obj");
        Ln(sb, "<< /Type /Font /Subtype /TrueType /BaseFont /FontA /FontDescriptor 5 0 R >>");
        Ln(sb, "endobj");

        // Object 7 — second font program stream (identical bytes, different object)
        offsets.Add(ByteLength(sb));
        Ln(sb, "7 0 obj");
        Ln(sb, $"<< /Length {hex2.Length} /Length1 {fontData.Length} /Filter /ASCIIHexDecode >>");
        sb.Append("stream\n").Append(hex2).Append("\nendstream\n");
        Ln(sb, "endobj");

        // Object 8 — second FontDescriptor
        offsets.Add(ByteLength(sb));
        Ln(sb, "8 0 obj");
        Ln(sb, "<< /Type /FontDescriptor /FontName /FontB /Flags 32 /FontFile2 7 0 R >>");
        Ln(sb, "endobj");

        // Object 9 — second Font
        offsets.Add(ByteLength(sb));
        Ln(sb, "9 0 obj");
        Ln(sb, "<< /Type /Font /Subtype /TrueType /BaseFont /FontB /FontDescriptor 8 0 R >>");
        Ln(sb, "endobj");

        var xrefOffset = ByteLength(sb);
        Ln(sb, "xref");
        Ln(sb, "0 10");
        Ln(sb, "0000000000 65535 f ");
        foreach (var o in offsets)
            Ln(sb, $"{o:D10} 00000 n ");
        Ln(sb, "trailer");
        Ln(sb, "<< /Size 10 /Root 1 0 R >>");
        Ln(sb, "startxref");
        Ln(sb, xrefOffset.ToString());
        sb.Append("%%EOF");

        return Encoding.Latin1.GetBytes(sb.ToString());
    }

    /// <summary>
    ///     Generates a single-page PDF (100×100 pt) that clips a black-filled rectangle
    ///     using a right-triangle W path (vertices: bottom-left, bottom-right, top-right).
    ///     The triangle occupies the lower-right half of the page. The top-left corner of
    ///     the page bbox is OUTSIDE the triangle, so exact polygon clipping must leave it
    ///     white. An axis-aligned bbox approximation would have painted it black.
    /// </summary>
    public static byte[] WithTriangularClip()
    {
        var sb = new StringBuilder();
        var offsets = new List<int>();
        Ln(sb, "%PDF-1.7");
        Ln(sb, "%\xE2\xE3\xCF\xD3");

        offsets.Add(ByteLength(sb));
        Ln(sb, "1 0 obj");
        Ln(sb, "<< /Type /Catalog /Pages 2 0 R >>");
        Ln(sb, "endobj");

        offsets.Add(ByteLength(sb));
        Ln(sb, "2 0 obj");
        Ln(sb, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
        Ln(sb, "endobj");

        offsets.Add(ByteLength(sb));
        Ln(sb, "3 0 obj");
        Ln(sb, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] /Contents 4 0 R /Resources <<>> >>");
        Ln(sb, "endobj");

        // Content stream:
        //   1. Set fill colour to black (0 g)
        //   2. Define a triangle: (0,0) → (100,0) → (100,100) then close
        //   3. W n  — set clip to triangle (nonzero winding), then end path without painting
        //   4. Fill the whole page with black — only pixels inside the triangle are painted
        // In PDF user space y=0 is bottom; the page flip puts y=0 at pixel row (height-1).
        // Triangle vertices in user space: bottom-left(0,0), bottom-right(100,0), top-right(100,100).
        // In device space (y flipped) this is: bottom-left pixel corner, bottom-right, top-right.
        // The top-left device pixel corner corresponds to user-space (0,100) — OUTSIDE the triangle.
        const string content =
            "0 g\n" +
            "0 0 m 100 0 l 100 100 l h\n" +
            "W n\n" +
            "0 0 100 100 re f";
        var cb = Encoding.Latin1.GetBytes(content);
        offsets.Add(ByteLength(sb));
        Ln(sb, "4 0 obj");
        Ln(sb, $"<< /Length {cb.Length} >>");
        sb.Append("stream\n");
        sb.Append(content);
        Ln(sb, "\nendstream");
        Ln(sb, "endobj");

        var xrefOffset = ByteLength(sb);
        Ln(sb, "xref");
        Ln(sb, "0 5");
        Ln(sb, "0000000000 65535 f ");
        foreach (var o in offsets) Ln(sb, $"{o:D10} 00000 n ");
        Ln(sb, "trailer");
        Ln(sb, "<< /Size 5 /Root 1 0 R >>");
        Ln(sb, "startxref");
        Ln(sb, xrefOffset.ToString());
        sb.Append("%%EOF");
        return Encoding.Latin1.GetBytes(sb.ToString());
    }

    /// <summary>
    ///     Generates a single-page PDF that tests a blend mode. The page is pre-filled white,
    ///     then a grey rectangle (128,128,128) is painted over it using the given ExtGState
    ///     blend mode. Returns the PDF bytes for rendering verification.
    ///     <para>
    ///         For <c>Multiply</c>: grey × white = grey → result darker than white.
    ///         For <c>Screen</c>:   white + grey - grey×white = white (near-white result).
    ///         For <c>Difference</c>: |white - grey| = grey → same grey as source.
    ///     </para>
    /// </summary>
    public static byte[] WithBlendMode(string blendMode)
    {
        var sb = new StringBuilder();
        var offsets = new List<int>();
        Ln(sb, "%PDF-1.7");
        Ln(sb, "%\xE2\xE3\xCF\xD3");

        offsets.Add(ByteLength(sb));
        Ln(sb, "1 0 obj");
        Ln(sb, "<< /Type /Catalog /Pages 2 0 R >>");
        Ln(sb, "endobj");

        offsets.Add(ByteLength(sb));
        Ln(sb, "2 0 obj");
        Ln(sb, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
        Ln(sb, "endobj");

        offsets.Add(ByteLength(sb));
        Ln(sb, "3 0 obj");
        Ln(sb, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] /Contents 4 0 R");
        Ln(sb, "   /Resources << /ExtGState << /GS1 5 0 R >> >> >>");
        Ln(sb, "endobj");

        // Content: mid-grey page background (0.4 ≈ 102), then lighter grey rect (0.5 ≈ 127) with blend mode.
        // Using 0.4 backdrop and 0.5 source keeps all blend modes in a range where the result
        // is visually distinct from both pure white and pure black, even for edge cases like
        // ColorDodge (0.4/0.5 = 0.8 ≈ 204, not white) and ColorBurn (1-0.6/0.5 = -0.2 → 0 = black).
        const string content = "0.4 g 0 0 100 100 re f\n" + // backdrop: mid-grey ~102
                               "/GS1 gs\n" +                // apply blend mode ExtGState
                               "0.5 g 0 0 100 100 re f";    // source: mid-grey ~127
        var cb = Encoding.Latin1.GetBytes(content);
        offsets.Add(ByteLength(sb));
        Ln(sb, "4 0 obj");
        Ln(sb, $"<< /Length {cb.Length} >>");
        sb.Append("stream\n");
        sb.Append(content);
        Ln(sb, "\nendstream");
        Ln(sb, "endobj");

        // ExtGState with the given blend mode.
        offsets.Add(ByteLength(sb));
        Ln(sb, "5 0 obj");
        Ln(sb, $"<< /Type /ExtGState /BM /{blendMode} >>");
        Ln(sb, "endobj");

        var xrefOffset = ByteLength(sb);
        Ln(sb, "xref");
        Ln(sb, "0 6");
        Ln(sb, "0000000000 65535 f ");
        foreach (var o in offsets) Ln(sb, $"{o:D10} 00000 n ");
        Ln(sb, "trailer");
        Ln(sb, "<< /Size 6 /Root 1 0 R >>");
        Ln(sb, "startxref");
        Ln(sb, xrefOffset.ToString());
        sb.Append("%%EOF");
        return Encoding.Latin1.GetBytes(sb.ToString());
    }

    /// <summary>
    ///     Generates a single-page PDF that uses an ExtGState /SMask (soft mask) to apply a
    ///     circular alpha gradient over a black rectangle. The mask Form XObject fills a white
    ///     circle in the centre of the page. Pixels inside the circle should be rendered as
    ///     black (mask = opaque); the corners outside the circle should remain white (mask = transparent).
    /// </summary>
    public static byte[] WithSoftMask()
    {
        var sb = new StringBuilder();
        var offsets = new List<int>();
        Ln(sb, "%PDF-1.7");
        Ln(sb, "%\xE2\xE3\xCF\xD3");

        offsets.Add(ByteLength(sb));
        Ln(sb, "1 0 obj");
        Ln(sb, "<< /Type /Catalog /Pages 2 0 R >>");
        Ln(sb, "endobj");

        offsets.Add(ByteLength(sb));
        Ln(sb, "2 0 obj");
        Ln(sb, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
        Ln(sb, "endobj");

        // Page with ExtGState resource GS1 that has /SMask referencing form object 6.
        offsets.Add(ByteLength(sb));
        Ln(sb, "3 0 obj");
        Ln(sb, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] /Contents 4 0 R");
        Ln(sb, "   /Resources << /ExtGState << /GS1 5 0 R >> >> >>");
        Ln(sb, "endobj");

        // Content: white background, then black rect with soft mask applied.
        const string content =
            "1 g 0 0 100 100 re f\n" + // white background
            "/GS1 gs\n" +              // activate soft mask
            "0 g 0 0 100 100 re f";    // black fill (masked by ellipse)
        var cb = Encoding.Latin1.GetBytes(content);
        offsets.Add(ByteLength(sb));
        Ln(sb, "4 0 obj");
        Ln(sb, $"<< /Length {cb.Length} >>");
        sb.Append("stream\n");
        sb.Append(content);
        Ln(sb, "\nendstream");
        Ln(sb, "endobj");

        // ExtGState with /SMask: type Alpha, mask form = object 6.
        offsets.Add(ByteLength(sb));
        Ln(sb, "5 0 obj");
        Ln(sb, "<< /Type /ExtGState /SMask << /Type /Mask /S /Alpha /G 6 0 R >> >>");
        Ln(sb, "endobj");

        // Mask Form XObject: fills a centred rectangle with white (opaque mask region).
        // Using a rectangle for simplicity — pixels inside the rect get mask=255, outside=0.
        const string maskContent = "1 g 25 25 50 50 re f";
        var mc = Encoding.Latin1.GetBytes(maskContent);
        offsets.Add(ByteLength(sb));
        Ln(sb, "6 0 obj");
        Ln(sb, $"<< /Type /XObject /Subtype /Form /BBox [0 0 100 100] /Length {mc.Length} >>");
        sb.Append("stream\n");
        sb.Append(maskContent);
        Ln(sb, "\nendstream");
        Ln(sb, "endobj");

        var xrefOffset = ByteLength(sb);
        Ln(sb, "xref");
        Ln(sb, "0 7");
        Ln(sb, "0000000000 65535 f ");
        foreach (var o in offsets) Ln(sb, $"{o:D10} 00000 n ");
        Ln(sb, "trailer");
        Ln(sb, "<< /Size 7 /Root 1 0 R >>");
        Ln(sb, "startxref");
        Ln(sb, xrefOffset.ToString());
        sb.Append("%%EOF");
        return Encoding.Latin1.GetBytes(sb.ToString());
    }

    /// <summary>
    ///     Generates a single-page PDF that uses a Separation color space (/MyCyan)
    ///     with a CMYK tint transform. A full-tint (1.0) fill covers the page.
    ///     The tint transform maps tint → (1,0,0,0) in DeviceCMYK (pure cyan).
    ///     Correct rendering should produce a cyan-ish colour rather than grey fallback.
    /// </summary>
    public static byte[] WithSeparationColorSpace()
    {
        var sb = new StringBuilder();
        var offsets = new List<int>();
        Ln(sb, "%PDF-1.7");
        Ln(sb, "%\xE2\xE3\xCF\xD3");

        offsets.Add(ByteLength(sb));
        Ln(sb, "1 0 obj");
        Ln(sb, "<< /Type /Catalog /Pages 2 0 R >>");
        Ln(sb, "endobj");

        offsets.Add(ByteLength(sb));
        Ln(sb, "2 0 obj");
        Ln(sb, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
        Ln(sb, "endobj");

        // Page with a /ColorSpace resource named /MyCyan (Separation).
        offsets.Add(ByteLength(sb));
        Ln(sb, "3 0 obj");
        Ln(sb, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] /Contents 4 0 R");
        Ln(sb, "   /Resources << /ColorSpace << /MyCyan 5 0 R >> >> >>");
        Ln(sb, "endobj");

        // Content: set fill to MyCyan separation, full tint, fill page.
        const string content =
            "1 g 0 0 100 100 re f\n" + // white background
            "/MyCyan cs\n" +           // set fill colour space to Separation
            "1 sc\n" +                 // full tint (1.0)
            "0 0 100 100 re f";        // fill page
        var cb = Encoding.Latin1.GetBytes(content);
        offsets.Add(ByteLength(sb));
        Ln(sb, "4 0 obj");
        Ln(sb, $"<< /Length {cb.Length} >>");
        sb.Append("stream\n");
        sb.Append(content);
        Ln(sb, "\nendstream");
        Ln(sb, "endobj");

        // Separation colour space: [/Separation /MyCyan /DeviceCMYK tintFn]
        // Tint function: Type 2, C0=[0 0 0 0], C1=[1 0 0 0] → pure cyan at full tint.
        offsets.Add(ByteLength(sb));
        Ln(sb, "5 0 obj");
        Ln(sb, "[/Separation /MyCyan /DeviceCMYK");
        Ln(sb, " << /FunctionType 2 /Domain [0 1] /C0 [0 0 0 0] /C1 [1 0 0 0] /N 1 >>]");
        Ln(sb, "endobj");

        var xrefOffset = ByteLength(sb);
        Ln(sb, "xref");
        Ln(sb, "0 6");
        Ln(sb, "0000000000 65535 f ");
        foreach (var o in offsets) Ln(sb, $"{o:D10} 00000 n ");
        Ln(sb, "trailer");
        Ln(sb, "<< /Size 6 /Root 1 0 R >>");
        Ln(sb, "startxref");
        Ln(sb, xrefOffset.ToString());
        sb.Append("%%EOF");
        return Encoding.Latin1.GetBytes(sb.ToString());
    }

    // Two pages, each with a private (page-specific) image XObject of raw pixel data.
    //   1 = Catalog, 2 = Pages, 3 = Page1, 4 = Im1, 5 = Page2, 6 = Im2
    private static byte[] BuildTwoPagesEachWithPrivateImage(int width, int height)
    {
        // Distinct pixel data per image so neither can be deduplicated; sized large
        // enough that pruning one is clearly visible in the output length.
        var img1 = new byte[width * height * 3];
        var img2 = new byte[width * height * 3];
        for (var i = 0; i < img1.Length; i++)
        {
            img1[i] = (byte)(i % 251);
            img2[i] = (byte)(i * 7 % 251);
        }

        using var ms = new MemoryStream();
        Line(ms, "%PDF-1.7");
        Line(ms, "%\xE2\xE3\xCF\xD3");

        var o1 = Pos(ms);
        Line(ms, "1 0 obj");
        Line(ms, "<< /Type /Catalog /Pages 2 0 R >>");
        Line(ms, "endobj");

        var o2 = Pos(ms);
        Line(ms, "2 0 obj");
        Line(ms, "<< /Type /Pages /Kids [3 0 R 5 0 R] /Count 2 >>");
        Line(ms, "endobj");

        // Page 1 references Im1 (obj 4).
        var o3 = Pos(ms);
        Line(ms, "3 0 obj");
        Line(ms, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842]");
        Line(ms, "   /Resources << /XObject << /Im1 4 0 R >> >> >>");
        Line(ms, "endobj");

        var o4 = Pos(ms);
        Line(ms, "4 0 obj");
        Line(ms, $"<< /Type /XObject /Subtype /Image /Width {width} /Height {height}");
        Line(ms, $"   /ColorSpace /DeviceRGB /BitsPerComponent 8 /Length {img1.Length} >>");
        Line(ms, "stream");
        Binary(ms, img1);
        Line(ms, string.Empty);
        Line(ms, "endstream");
        Line(ms, "endobj");

        // Page 2 references Im2 (obj 6).
        var o5 = Pos(ms);
        Line(ms, "5 0 obj");
        Line(ms, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842]");
        Line(ms, "   /Resources << /XObject << /Im2 6 0 R >> >> >>");
        Line(ms, "endobj");

        var o6 = Pos(ms);
        Line(ms, "6 0 obj");
        Line(ms, $"<< /Type /XObject /Subtype /Image /Width {width} /Height {height}");
        Line(ms, $"   /ColorSpace /DeviceRGB /BitsPerComponent 8 /Length {img2.Length} >>");
        Line(ms, "stream");
        Binary(ms, img2);
        Line(ms, string.Empty);
        Line(ms, "endstream");
        Line(ms, "endobj");

        var xref = Pos(ms);
        Line(ms, "xref");
        Line(ms, "0 7");
        Line(ms, "0000000000 65535 f ");
        foreach (var o in new[] { o1, o2, o3, o4, o5, o6 })
            Line(ms, $"{o:D10} 00000 n ");
        Line(ms, "trailer");
        Line(ms, "<< /Size 7 /Root 1 0 R >>");
        Line(ms, "startxref");
        Line(ms, xref.ToString());
        Text(ms, "%%EOF");

        return ms.ToArray();
    }

    private static byte[] ZlibCompress(byte[] data)
    {
        using var ms = new MemoryStream();
        using (var zlib = new ZLibStream(ms, CompressionMode.Compress, true))
            zlib.Write(data);
        return ms.ToArray();
    }

    internal static void Ln(StringBuilder b, string line) => b.Append(line).Append('\n');

    internal static int Len(StringBuilder b) => Encoding.Latin1.GetByteCount(b.ToString());

    internal static void Text(MemoryStream ms, string s) => ms.Write(Encoding.Latin1.GetBytes(s));

    internal static void Line(MemoryStream ms, string s)
    {
        Text(ms, s);
        ms.WriteByte((byte)'\n');
    }

    internal static void Binary(MemoryStream ms, byte[] b) => ms.Write(b);

    internal static long Pos(MemoryStream ms) => ms.Position;
}
