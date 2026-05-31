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

    public static byte[] WithInfo(string title, string author) =>
        Build(pageCount: 1, title: title, author: author);

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
        var infoRef = "";
        if (title is not null || author is not null)
        {
            var infoObjNum = 3 + pageCount;
            offsets.Add(ByteLen(sb));
            Ln(sb, $"{infoObjNum} 0 obj");
            var titleEntry = title is not null ? $" /Title ({title})" : "";
            var authorEntry = author is not null ? $" /Author ({author})" : "";
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
}
