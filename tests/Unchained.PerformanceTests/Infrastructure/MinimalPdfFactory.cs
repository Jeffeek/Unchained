using System.Text;

namespace Unchained.PerformanceTests.Infrastructure;

/// <summary>
/// Generates minimal but spec-compliant PDF byte arrays without any external dependency.
/// Used as benchmark fixtures so the benchmarks have no dependency on a working
/// Unchained write path (the write path is what we're building).
/// </summary>
internal static class MinimalPdfFactory
{
    /// <summary>
    /// Builds a valid single-page PDF with <paramref name="pageCount"/> blank A4 pages.
    /// The structure is the smallest PDF that conforms to ISO 32000-1 §7.5.
    /// </summary>
    public static byte[] Build(int pageCount = 1, string pageText = "Unchained benchmark")
    {
        // We construct the PDF as ASCII text using a StringBuilder then convert at the end.
        // Byte offsets are calculated manually as we go.
        var sb = new StringBuilder();
        var offsets = new List<int>();

        sb.AppendLine("%PDF-1.7");
        sb.AppendLine("%\xE2\xE3\xCF\xD3"); // binary marker

        // Object 1 — Catalog
        offsets.Add(Encoding.Latin1.GetByteCount(sb.ToString()));
        sb.AppendLine("1 0 obj");
        sb.AppendLine("<< /Type /Catalog /Pages 2 0 R >>");
        sb.AppendLine("endobj");

        // Object 2 — Pages root
        offsets.Add(Encoding.Latin1.GetByteCount(sb.ToString()));
        var kids = string.Join(" ", Enumerable.Range(3, pageCount).Select(static n => $"{n} 0 R"));
        sb.AppendLine("2 0 obj");
        sb.AppendLine($"<< /Type /Pages /Kids [{kids}] /Count {pageCount} >>");
        sb.AppendLine("endobj");

        // Objects 3..N — one Page per page
        for (var i = 0; i < pageCount; i++)
        {
            var objNum = 3 + i;
            offsets.Add(Encoding.Latin1.GetByteCount(sb.ToString()));
            sb.AppendLine($"{objNum} 0 obj");
            sb.AppendLine("<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] >>");
            sb.AppendLine("endobj");
        }

        // Cross-reference table
        var xrefOffset = Encoding.Latin1.GetByteCount(sb.ToString());
        sb.AppendLine("xref");
        sb.AppendLine($"0 {2 + pageCount}");
        sb.AppendLine("0000000000 65535 f ");
        foreach (var offset in offsets)
            sb.AppendLine($"{offset:D10} 00000 n ");

        // Trailer
        sb.AppendLine("trailer");
        sb.AppendLine($"<< /Size {2 + pageCount} /Root 1 0 R >>");
        sb.AppendLine("startxref");
        sb.AppendLine(xrefOffset.ToString());
        sb.AppendLine("%%EOF");

        return Encoding.Latin1.GetBytes(sb.ToString());
    }
}
