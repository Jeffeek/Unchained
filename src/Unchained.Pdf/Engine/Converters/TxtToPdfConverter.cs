using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Unchained.Pdf.Abstractions;
using Unchained.Pdf.Content;
using Unchained.Pdf.Core;
using Unchained.Pdf.Models;

namespace Unchained.Pdf.Engine.Converters;

/// <summary>
///     Converts plain text to a PDF document with automatic word-wrap and pagination.
///     When <see cref="TxtLoadOptions.Tagged" /> is <see langword="true" />, each text line
///     is wrapped in a <c>/P</c> marked-content sequence for accessibility.
/// </summary>
internal static class TxtToPdfConverter
{
    internal static IPdfDocument Convert(string text, TxtLoadOptions options)
    {
        var acc = new PdfPageAccumulator();
        var fontRef = acc.AddFont(options.FontName);
        var fontMap = new Dictionary<string, PdfIndirectReference> { ["F1"] = fontRef };

        var usableWidth = options.PageWidthPt - (2 * options.MarginPt);
        var lineHeight = options.FontSize * options.LineSpacing;
        var usableHeight = options.PageHeightPt - (2 * options.MarginPt);
        var linesPerPage = Math.Max(1, (int)(usableHeight / lineHeight));

        var allLines = LayoutLines(text, options.FontName, options.FontSize, usableWidth);

        // Ensure at least one page even for empty input.
        if (allLines.Count == 0) allLines.Add(string.Empty);

        var pageIndex = 0;
        for (var pageStart = 0; pageStart < allLines.Count; pageStart += linesPerPage)
        {
            var pageLines = allLines.Skip(pageStart).Take(linesPerPage).ToList();

            if (options.Tagged)
            {
                var taggedItems = new List<TaggedContentItem>();
                var content = BuildPageContentTagged(pageLines, options, pageIndex, taggedItems);
                // ReSharper disable once BadListLineBreaks
                acc.AddPage(
                    options.PageWidthPt,
                    options.PageHeightPt,
                    content,
                    fontMap,
                    taggedItems,
                    options.Language
                );
            }
            else
            {
                var content = BuildPageContent(pageLines, options);
                acc.AddPage(options.PageWidthPt, options.PageHeightPt, content, fontMap);
            }

            pageIndex++;
        }

        return acc.Build();
    }

    [SuppressMessage("ReSharper", "BadListLineBreaks")]
    private static List<string> LayoutLines(
        string text,
        string fontName,
        float fontSize,
        float usableWidth
    )
    {
        var result = new List<string>();
        foreach (var rawLine in text.ReplaceLineEndings("\n").Split('\n'))
            result.AddRange(WrapLine(rawLine, fontName, fontSize, usableWidth));

        return result;
    }

    [SuppressMessage("ReSharper", "BadListLineBreaks")]
    private static IEnumerable<string> WrapLine(
        string line,
        string fontName,
        float fontSize,
        float maxWidth
    )
    {
        if (line.Length == 0)
        {
            yield return string.Empty;

            yield break;
        }

        var current = new StringBuilder();
        var spaceWidth = Standard14Widths.Get(fontName, ' ') * fontSize / 1000f;

        foreach (var word in line.Split(' '))
        {
            if (current.Length == 0)
                current.Append(word);
            else
            {
                var projected = MeasureText(current.ToString(), fontName, fontSize) +
                                spaceWidth +
                                MeasureText(word, fontName, fontSize);
                if (projected <= maxWidth)
                {
                    current.Append(' ');
                    current.Append(word);
                }
                else
                {
                    yield return current.ToString();

                    current.Clear().Append(word);
                }
            }
        }

        yield return current.ToString();
    }

    internal static float MeasureText(string text, string fontName, float fontSize) =>
        text.Sum(c => Standard14Widths.Get(fontName, c > 255 ? '?' : c) * fontSize / 1000f);

    private static byte[] BuildPageContent(IReadOnlyCollection<string> lines, TxtLoadOptions options)
    {
        var buf = new ArrayBufferWriter<byte>(256 + (lines.Count * 80));
        var w = new ContentStreamWriter(buf);

        var leading = options.FontSize * options.LineSpacing;
        var startX = options.MarginPt;
        var startY = options.PageHeightPt - options.MarginPt - options.FontSize;

        w.Op("BT"u8);
        w.Name("F1");
        w.Float(options.FontSize);
        w.Op("Tf"u8);
        w.Float(leading);
        w.Op("TL"u8);
        w.Float(startX);
        w.Float(startY);
        w.Op("Td"u8);

        var first = true;
        foreach (var line in lines)
        {
            if (!first) w.Op("T*"u8);
            w.LiteralString(line);
            w.Op("Tj"u8);
            first = false;
        }

        w.Op("ET"u8);

        return buf.WrittenMemory.ToArray();
    }

    /// <summary>
    ///     Builds a page content stream with BDC/EMC wrappers around each line.
    ///     Each non-empty line is tagged as a /P (paragraph) structure element.
    ///     Empty lines are tagged as /Artifact to exclude them from the structure tree.
    /// </summary>
    private static byte[] BuildPageContentTagged(
        IReadOnlyList<string> lines,
        TxtLoadOptions options,
        int pageIndex,
        ICollection<TaggedContentItem> taggedItems
    )
    {
        var buf = new ArrayBufferWriter<byte>(256 + (lines.Count * 120));
        var w = new ContentStreamWriter(buf);

        var leading = options.FontSize * options.LineSpacing;
        var startX = options.MarginPt;
        var startY = options.PageHeightPt - options.MarginPt - options.FontSize;
        var mcid = 0;

        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var y = startY - (i * leading);

            if (string.IsNullOrEmpty(line))
            {
                // Empty lines are artifacts — no structure element, no MCID.
                w.Op("/Artifact BMC"u8);
                w.Op("EMC"u8);
                continue;
            }

            w.MarkedContentBegin("P", mcid);
            taggedItems.Add(new TaggedContentItem("P", mcid, pageIndex));
            mcid++;

            w.Op("BT"u8);
            w.Name("F1");
            w.Float(options.FontSize);
            w.Op("Tf"u8);
            w.Float(startX);
            w.Float(y);
            w.Op("Td"u8);
            w.LiteralString(line);
            w.Op("Tj"u8);
            w.Op("ET"u8);

            w.MarkedContentEnd();
        }

        return buf.WrittenMemory.ToArray();
    }
}
