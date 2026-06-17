using System.Text;

namespace Unchained.Pdf.Tests.Helpers;

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

        Ln(sb, $"%PDF-{version}");
        Ln(sb, "%\xE2\xE3\xCF\xD3");

        for (var i = 0; i < bodies.Count; i++)
        {
            offsets.Add(ByteLength(sb));
            Ln(sb, $"{i + 1} 0 obj");
            Ln(sb, bodies[i]);
            Ln(sb, "endobj");
        }

        var total = bodies.Count + 1;
        var xrefOffset = ByteLength(sb);
        Ln(sb, "xref");
        Ln(sb, $"0 {total}");
        Ln(sb, "0000000000 65535 f ");
        foreach (var o in offsets) Ln(sb, $"{o:D10} 00000 n ");
        Ln(sb, "trailer");
        Ln(sb, $"<< /Size {total} /Root 1 0 R /ID [<AABBCCDD><AABBCCDD>] >>");
        Ln(sb, "startxref");
        Ln(sb, xrefOffset.ToString());
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

        Ln(sb, $"%PDF-{version}");
        Ln(sb, "%\xE2\xE3\xCF\xD3");

        for (var i = 0; i < bodies.Count; i++)
        {
            offsets.Add(ByteLength(sb));
            Ln(sb, $"{i + 1} 0 obj");
            if (i + 1 == streamObjIndex)
            {
                Ln(sb, $"{streamDictNoLength[..^2]} /Length {streamData.Length} >>");
                sb.Append("stream\n");
                foreach (var b in streamData) sb.Append((char)b);
                Ln(sb, "\nendstream");
            }
            else
                Ln(sb, bodies[i]);

            Ln(sb, "endobj");
        }

        var total = bodies.Count + 1;
        var xrefOffset = ByteLength(sb);
        Ln(sb, "xref");
        Ln(sb, $"0 {total}");
        Ln(sb, "0000000000 65535 f ");
        foreach (var o in offsets) Ln(sb, $"{o:D10} 00000 n ");
        Ln(sb, "trailer");
        Ln(sb, $"<< /Size {total} /Root 1 0 R /ID [<AABBCCDD><AABBCCDD>] >>");
        Ln(sb, "startxref");
        Ln(sb, xrefOffset.ToString());
        sb.Append("%%EOF");

        return Encoding.Latin1.GetBytes(sb.ToString());
    }

    private static void Ln(StringBuilder b, string line) => b.Append(line).Append('\n');

    private static int ByteLength(StringBuilder b) => Encoding.Latin1.GetByteCount(b.ToString());
}
