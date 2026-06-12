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
        // Objects: 0 (free) + 1 (Catalog) + 1 (Pages) + pageCount (Page nodes) = 3 + pageCount
        sb.AppendLine($"0 {3 + pageCount}");
        sb.AppendLine("0000000000 65535 f ");
        foreach (var offset in offsets)
            sb.AppendLine($"{offset:D10} 00000 n ");

        // Trailer
        sb.AppendLine("trailer");
        sb.AppendLine($"<< /Size {3 + pageCount} /Root 1 0 R >>");
        sb.AppendLine("startxref");
        sb.AppendLine(xrefOffset.ToString());
        sb.AppendLine("%%EOF");

        return Encoding.Latin1.GetBytes(sb.ToString());
    }

    /// <summary>
    /// Builds a valid PDF with <paramref name="pageCount"/> pages, each containing
    /// the given <paramref name="text"/> rendered in Helvetica 12pt.
    /// Used for text-extraction benchmarks where all libraries must actually find content.
    /// </summary>
    public static byte[] BuildWithText(int pageCount = 1, string text = "Unchained benchmark text content")
        => BuildWithTextCore(pageCount, text);

    private static byte[] BuildWithTextCore(int pageCount, string text)
    {
        var escaped = text.Replace("\\", @"\\").Replace("(", "\\(").Replace(")", "\\)");

        // Pre-build content stream bytes for each page
        var streams = Enumerable.Range(0, pageCount)
            .Select(i => Encoding.Latin1.GetBytes(
                $"BT /F1 12 Tf 50 {700 - i * 15} Td ({escaped}) Tj ET"))
            .ToArray();

        // Object numbering:
        //  1 = Catalog
        //  2 = Pages
        //  3 = Font
        //  4 + 2*i = Page i dict
        //  5 + 2*i = Content stream i
        var totalObjects = 3 + pageCount * 2;
        const int fontObjNum = 3;

        var sb = new StringBuilder();
        var offsets = new long[totalObjects + 1]; // 1-based

        sb.Append("%PDF-1.7\n%\u00e2\u00e3\u00cf\u00d3\n");

        // Obj 1 — Catalog
        offsets[1] = Encoding.Latin1.GetByteCount(sb.ToString());
        sb.Append("1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");

        // Obj 2 — Pages
        offsets[2] = Encoding.Latin1.GetByteCount(sb.ToString());
        var kids = string.Join(" ", Enumerable.Range(0, pageCount).Select(static i => $"{4 + i * 2} 0 R"));
        sb.Append($"2 0 obj\n<< /Type /Pages /Kids [{kids}] /Count {pageCount} >>\nendobj\n");

        // Obj 3 — Font
        offsets[3] = Encoding.Latin1.GetByteCount(sb.ToString());
        sb.Append($"{fontObjNum} 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>\nendobj\n");

        // Page pairs
        for (var i = 0; i < pageCount; i++)
        {
            var pageNum = 4 + i * 2;
            var contNum = 5 + i * 2;
            var streamLen = streams[i].Length;

            offsets[pageNum] = Encoding.Latin1.GetByteCount(sb.ToString());
            sb.Append($"{pageNum} 0 obj\n");
            sb.Append("<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842]\n");
            sb.Append($"   /Contents {contNum} 0 R\n");
            sb.Append($"   /Resources << /Font << /F1 {fontObjNum} 0 R >> >> >>\n");
            sb.Append("endobj\n");

            offsets[contNum] = Encoding.Latin1.GetByteCount(sb.ToString());
            sb.Append($"{contNum} 0 obj\n<< /Length {streamLen} >>\nstream\n");
            sb.Append(Encoding.Latin1.GetString(streams[i]));
            sb.Append("\nendstream\nendobj\n");
        }

        // Xref
        var xrefPos = Encoding.Latin1.GetByteCount(sb.ToString());
        sb.Append($"xref\n0 {totalObjects + 1}\n");
        sb.Append("0000000000 65535 f \n");
        for (var n = 1; n <= totalObjects; n++)
            sb.Append($"{offsets[n]:D10} 00000 n \n");

        sb.Append($"trailer\n<< /Size {totalObjects + 1} /Root 1 0 R >>\nstartxref\n{xrefPos}\n%%EOF\n");

        return Encoding.Latin1.GetBytes(sb.ToString());
    }
}
