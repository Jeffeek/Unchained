using System.Text;

namespace Unchained.Pdf.Tests.Shared;

/// <summary>
///     Assembles a syntactically valid PDF from a list of object bodies, computing the
///     cross-reference table and trailer automatically. Object number <c>i</c> corresponds to
///     <c>bodies[i-1]</c>; object 1 is conventionally the catalog. Lets tests craft arbitrary
///     object graphs (struct trees, annotations, actions) to exercise specific validator branches.
/// </summary>
internal static class RawPdfBuilder
{
    /// <summary>
    ///     Builds a PDF with the given object bodies (each is the dictionary/stream text that
    ///     appears between <c>N 0 obj</c> and <c>endobj</c>). The trailer references object 1 as
    ///     <c>/Root</c> and includes a fixed <c>/ID</c>. <paramref name="version" /> sets the header.
    /// </summary>
    public static byte[] Build(IReadOnlyList<string> bodies, string version = "1.7")
    {
        var sb = new StringBuilder();
        var offsets = new List<int>();

        PdfFixtures.Ln(sb, $"%PDF-{version}");
        PdfFixtures.Ln(sb, "%\xE2\xE3\xCF\xD3");

        for (var i = 0; i < bodies.Count; i++)
        {
            offsets.Add(PdfFixtures.Len(sb));
            PdfFixtures.Ln(sb, $"{i + 1} 0 obj");
            PdfFixtures.Ln(sb, bodies[i]);
            PdfFixtures.Ln(sb, "endobj");
        }

        var total = bodies.Count + 1;
        var xrefOffset = PdfFixtures.Len(sb);
        PdfFixtures.Ln(sb, "xref");
        PdfFixtures.Ln(sb, $"0 {total}");
        PdfFixtures.Ln(sb, "0000000000 65535 f ");
        foreach (var o in offsets)
            PdfFixtures.Ln(sb, $"{o:D10} 00000 n ");
        PdfFixtures.Ln(sb, "trailer");
        PdfFixtures.Ln(sb, $"<< /Size {total} /Root 1 0 R /ID [<AABBCCDD><AABBCCDD>] >>");
        PdfFixtures.Ln(sb, "startxref");
        PdfFixtures.Ln(sb, xrefOffset.ToString());
        sb.Append("%%EOF");

        return Encoding.Latin1.GetBytes(sb.ToString());
    }

    /// <summary>
    ///     Builds a PDF whose object <paramref name="streamObjIndex" /> (1-based) is a stream whose
    ///     dictionary text is <paramref name="streamDictNoLength" /> (without <c>/Length</c>) and whose
    ///     body is <paramref name="streamData" /> bytes. All other objects are plain dictionaries.
    /// </summary>
    public static byte[] BuildWithStream(
        IReadOnlyList<string> bodies,
        int streamObjIndex,
        string streamDictNoLength,
        byte[] streamData,
        string version = "1.7"
    )
    {
        var sb = new StringBuilder();
        var offsets = new List<int>();

        PdfFixtures.Ln(sb, $"%PDF-{version}");
        PdfFixtures.Ln(sb, "%\xE2\xE3\xCF\xD3");

        for (var i = 0; i < bodies.Count; i++)
        {
            offsets.Add(PdfFixtures.Len(sb));
            PdfFixtures.Ln(sb, $"{i + 1} 0 obj");
            if (i + 1 == streamObjIndex)
            {
                PdfFixtures.Ln(sb, $"{streamDictNoLength[..^2]} /Length {streamData.Length} >>");
                sb.Append("stream\n");
                foreach (var b in streamData) sb.Append((char)b);
                PdfFixtures.Ln(sb, "\nendstream");
            }
            else
                PdfFixtures.Ln(sb, bodies[i]);

            PdfFixtures.Ln(sb, "endobj");
        }

        var total = bodies.Count + 1;
        var xrefOffset = PdfFixtures.Len(sb);
        PdfFixtures.Ln(sb, "xref");
        PdfFixtures.Ln(sb, $"0 {total}");
        PdfFixtures.Ln(sb, "0000000000 65535 f ");
        foreach (var o in offsets)
            PdfFixtures.Ln(sb, $"{o:D10} 00000 n ");
        PdfFixtures.Ln(sb, "trailer");
        PdfFixtures.Ln(sb, $"<< /Size {total} /Root 1 0 R /ID [<AABBCCDD><AABBCCDD>] >>");
        PdfFixtures.Ln(sb, "startxref");
        PdfFixtures.Ln(sb, xrefOffset.ToString());
        sb.Append("%%EOF");

        return Encoding.Latin1.GetBytes(sb.ToString());
    }
}
