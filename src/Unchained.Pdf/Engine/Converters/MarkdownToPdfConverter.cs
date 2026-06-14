using System.Buffers;
using System.Text;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Unchained.Pdf.Abstractions;
using Unchained.Pdf.Content;
using Unchained.Pdf.Core;
using Unchained.Pdf.Models;

namespace Unchained.Pdf.Engine.Converters;

/// <summary>
///     Converts Markdown text to a PDF document.
///     Supports headings (h1–h6), paragraphs, bold, italic, inline code,
///     fenced code blocks, unordered lists, ordered lists, and thematic breaks.
///     When <see cref="MdLoadOptions.Tagged" /> is <see langword="true" />, every block
///     is wrapped in semantically appropriate BDC/EMC marked-content sequences.
/// </summary>
internal static class MarkdownToPdfConverter
{
    // Font resource keys used in content streams.
    private const string KeyBody = "F1";   // Helvetica / regular
    private const string KeyBold = "F2";   // Helvetica-Bold
    private const string KeyItalic = "F3"; // Helvetica-Oblique
    private const string KeyMono = "F4";   // Courier

    internal static IPdfDocument Convert(string markdown, MdLoadOptions options)
    {
        var pipeline = new MarkdownPipelineBuilder().Build();
        var mdDoc = Markdown.Parse(markdown, pipeline);

        // Collect all runs to lay out: each run is (text, fontKey, fontSize, indent, structTag).
        var runs = new List<TextRun>();
        CollectRuns(mdDoc, options, runs);

        // Paginate runs onto pages.
        var acc = new PdfPageAccumulator();
        var fontMap = BuildFontMap(acc, options);

        var pages = Paginate(runs, options).ToList();
        var pageIndex = 0;
        foreach (var page in pages)
        {
            if (options.Tagged)
            {
                var taggedItems = new List<TaggedContentItem>();
                var content = BuildPageContentTagged(page, options, pageIndex, taggedItems);
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
                var content = BuildPageContent(page, options);
                acc.AddPage(options.PageWidthPt, options.PageHeightPt, content, fontMap);
            }

            pageIndex++;
        }

        return acc.Build();
    }

    // ── Font map ──────────────────────────────────────────────────────────────

    private static Dictionary<string, PdfIndirectReference> BuildFontMap(PdfPageAccumulator acc, MdLoadOptions opts)
    {
        var boldName = opts.BodyFontName.Contains("Times", StringComparison.OrdinalIgnoreCase)
            ? opts.BodyFontName.Replace("Roman", "Bold").Replace("Italic", "BoldItalic")
            : $"{opts.BodyFontName}-Bold";
        var italicName = $"{opts.BodyFontName}-Oblique";

        return new Dictionary<string, PdfIndirectReference>
        {
            [KeyBody] = acc.AddFont(opts.BodyFontName),
            [KeyBold] = acc.AddFont(boldName),
            [KeyItalic] = acc.AddFont(italicName),
            [KeyMono] = acc.AddFont(opts.CodeFontName)
        };
    }

    private static void CollectRuns(MarkdownDocument doc, MdLoadOptions opts, List<TextRun> runs)
    {
        foreach (var block in doc)
            // ReSharper disable once BadListLineBreaks
            CollectBlock(block, opts, runs, 0);
    }

    private static void CollectBlock(
        IMarkdownObject block,
        MdLoadOptions opts,
        List<TextRun> runs,
        float indent
    )
    {
        switch (block)
        {
            case HeadingBlock h:
            {
                var size = opts.HeadingFontSize(h.Level);
                var text = ExtractInlineText(h.Inline);
                var tag = $"H{h.Level}";
                runs.Add(new TextRun(text, KeyBold, size, indent, structTag: tag));
                // ReSharper disable once BadListLineBreaks
                runs.Add(
                    new TextRun(
                        string.Empty,
                        KeyBody,
                        opts.BodyFontSize * 0.4f,
                        indent,
                        true,
                        structTag: tag
                    )
                );
                break;
            }
            case ParagraphBlock p:
            {
                var segments = ExtractInlineSegments(p.Inline, opts);
                AppendWrappedSegments(segments, runs, opts, indent, structTag: "P");
                // ReSharper disable once BadListLineBreaks
                runs.Add(
                    new TextRun(
                        string.Empty,
                        KeyBody,
                        opts.ParagraphSpacingPt,
                        indent,
                        true,
                        structTag: "P"
                    )
                );
                break;
            }
            case FencedCodeBlock fc:
            {
                runs.AddRange(
                    fc.Lines.Lines
                        .Select(static l => l.Slice.ToString())
                        .Select(line => new TextRun(line, KeyMono, opts.CodeFontSize, indent + 12f, structTag: "Code"))
                );
                // ReSharper disable once BadListLineBreaks
                runs.Add(
                    new TextRun(
                        string.Empty,
                        KeyBody,
                        opts.ParagraphSpacingPt,
                        indent,
                        true,
                        structTag: "Code"
                    )
                );
                break;
            }
            case ListBlock list:
            {
                var idx = 1;
                foreach (var item in list.OfType<ListItemBlock>())
                {
                    var bullet = list.IsOrdered ? $"{idx}. " : "• ";
                    var bulletWidth = TxtToPdfConverter.MeasureText(bullet, opts.BodyFontName, opts.BodyFontSize);
                    var itemIndent = indent + bulletWidth + 4f;

                    var firstBlock = item.FirstOrDefault();
                    if (firstBlock is ParagraphBlock fp)
                    {
                        var segments = ExtractInlineSegments(fp.Inline, opts);
                        if (segments.Count > 0)
                            segments[0] = (bullet + segments[0].Text, segments[0].FontKey, segments[0].FontSize);
                        // ReSharper disable once BadListLineBreaks
                        AppendWrappedSegments(
                            segments,
                            runs,
                            opts,
                            indent,
                            itemIndent,
                            "LBody"
                        );
                    }
                    else
                        runs.Add(new TextRun(bullet, KeyBody, opts.BodyFontSize, indent, structTag: "LI"));

                    foreach (var sub in item.Skip(1))
                        CollectBlock(sub, opts, runs, itemIndent);

                    idx++;
                }

                // ReSharper disable once BadListLineBreaks
                runs.Add(
                    new TextRun(
                        string.Empty,
                        KeyBody,
                        opts.ParagraphSpacingPt * 0.5f,
                        indent,
                        true,
                        structTag: "L"
                    )
                );
                break;
            }
            case ThematicBreakBlock:
            {
                // ReSharper disable BadListLineBreaks
                runs.Add(
                    new TextRun(
                        string.Empty,
                        KeyBody,
                        opts.BodyFontSize,
                        indent,
                        isRule: true,
                        structTag: "P"
                    )
                );
                runs.Add(
                    new TextRun(
                        string.Empty,
                        KeyBody,
                        opts.ParagraphSpacingPt * 0.5f,
                        indent,
                        true,
                        structTag: "P"
                    )
                );
                // ReSharper restore BadListLineBreaks
                break;
            }
            case QuoteBlock qb:
            {
                foreach (var sub in qb)
                    // ReSharper disable once BadListLineBreaks
                    CollectBlock(sub, opts, runs, indent + 24f);
                break;
            }
        }
    }

    // ── Inline text extraction ────────────────────────────────────────────────

    private static string ExtractInlineText(ContainerInline? container)
    {
        if (container is null) return string.Empty;

        var sb = new StringBuilder();
        foreach (var inline in container)
        {
            switch (inline)
            {
                case LiteralInline lit: sb.Append(lit.Content.ToString()); break;
                case EmphasisInline em: sb.Append(ExtractInlineText(em)); break;
                case CodeInline code: sb.Append(code.Content); break;
                case LineBreakInline: sb.Append(' '); break;
            }
        }

        return sb.ToString();
    }

    private static List<(string Text, string FontKey, float FontSize)> ExtractInlineSegments(
        ContainerInline? container,
        MdLoadOptions opts
    )
    {
        var list = new List<(string, string, float)>();
        AppendInlineSegments(container, opts, list, KeyBody, opts.BodyFontSize);
        return list;
    }

    private static void AppendInlineSegments(
        ContainerInline? container,
        MdLoadOptions opts,
        ICollection<(string Text, string FontKey, float FontSize)> list,
        string fontKey,
        float fontSize
    )
    {
        if (container is null) return;

        foreach (var inline in container)
        {
            switch (inline)
            {
                case LiteralInline lit:
                {
                    list.Add((lit.Content.ToString(), fontKey, fontSize));
                    break;
                }
                case EmphasisInline em:
                {
                    var childKey = em.DelimiterCount >= 2 ? KeyBold : KeyItalic;
                    AppendInlineSegments(em, opts, list, childKey, fontSize);
                    break;
                }
                case CodeInline code:
                {
                    list.Add((code.Content, KeyMono, opts.CodeFontSize));
                    break;
                }
                case LineBreakInline:
                {
                    list.Add((" ", fontKey, fontSize));
                    break;
                }
            }
        }
    }

    // ── Segment word-wrap ─────────────────────────────────────────────────────

    private static void AppendWrappedSegments(
        List<(string Text, string FontKey, float FontSize)> segments,
        ICollection<TextRun> runs,
        MdLoadOptions opts,
        float indent,
        float continuationIndent = -1f,
        string structTag = "P"
    )
    {
        if (continuationIndent < 0) continuationIndent = indent;
        var lineRuns = new List<(string, string, float)>();
        var lineWidth = 0f;
        var isFirst = true;

        foreach (var (rawText, fk, fs) in segments)
        {
            var fontName = fk == KeyMono ? opts.CodeFontName : opts.BodyFontName;
            var words = rawText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var spaceWidth = Standard14Widths.Get(fontName, ' ') * fs / 1000f;

            foreach (var word in words)
            {
                var ww = TxtToPdfConverter.MeasureText(word, fontName, fs);
                var effectiveIndent = isFirst ? indent : continuationIndent;
                var effective = opts.PageWidthPt - (2 * opts.MarginPt) - effectiveIndent;

                if (lineWidth > 0 && lineWidth + spaceWidth + ww > effective)
                {
                    FlushLine(lineRuns, runs, isFirst ? indent : continuationIndent, structTag);
                    lineRuns.Clear();
                    lineWidth = 0f;
                    isFirst = false;
                }

                if (lineWidth > 0)
                {
                    lineRuns.Add((" ", fk, fs));
                    lineWidth += spaceWidth;
                }

                lineRuns.Add((word, fk, fs));
                lineWidth += ww;
            }
        }

        if (lineRuns.Count > 0)
            FlushLine(lineRuns, runs, isFirst ? indent : continuationIndent, structTag);
    }

    private static void FlushLine(
        List<(string Text, string FontKey, float FontSize)> lineRuns,
        ICollection<TextRun> runs,
        float indent,
        string structTag
    )
    {
        string? lastKey = null;
        var lastSize = 0f;
        var sb = new StringBuilder();
        foreach (var (t, k, s) in lineRuns)
        {
            if (k != lastKey || Math.Abs(s - lastSize) > 0.01f)
            {
                if (sb.Length > 0) runs.Add(new TextRun(sb.ToString(), lastKey!, lastSize, indent, structTag: structTag));
                sb.Clear().Append(t);
                lastKey = k;
                lastSize = s;
            }
            else
                sb.Append(t);
        }

        if (sb.Length > 0 && lastKey is not null)
            runs.Add(new TextRun(sb.ToString(), lastKey, lastSize, indent, structTag: structTag));
    }

    // ── Pagination ────────────────────────────────────────────────────────────

    private static IEnumerable<List<TextRun>> Paginate(List<TextRun> runs, MdLoadOptions opts)
    {
        var pages = new List<List<TextRun>>();
        var current = new List<TextRun>();
        var usedY = 0f;
        var usableHeight = opts.PageHeightPt - (2 * opts.MarginPt);

        foreach (var run in runs)
        {
            var runH = run.IsRule
                ? run.FontSize + 4f
                : run.IsEmptyLine
                    ? run.FontSize
                    : run.FontSize * opts.LineSpacing;

            if (usedY + runH > usableHeight && current.Count > 0)
            {
                pages.Add(current);
                current = [];
                usedY = 0f;
            }

            current.Add(run);
            usedY += runH;
        }

        if (current.Count > 0) pages.Add(current);
        if (pages.Count == 0) pages.Add([]);

        return pages;
    }

    // ── Content stream builders ───────────────────────────────────────────────

    private static byte[] BuildPageContent(List<TextRun> runs, MdLoadOptions opts)
    {
        var buf = new ArrayBufferWriter<byte>(1024);
        var w = new ContentStreamWriter(buf);

        var x = opts.MarginPt;
        var y = opts.PageHeightPt - opts.MarginPt;
        var curFont = string.Empty;
        var curSize = 0f;

        w.Op("BT"u8);

        foreach (var run in runs)
        {
            if (run.IsRule)
            {
                w.Op("ET"u8);
                y -= run.FontSize * 0.5f;
                w.Float(opts.MarginPt);
                w.Float(y);
                w.Op("m"u8);
                w.Float(opts.PageWidthPt - opts.MarginPt);
                w.Float(y);
                w.Op("l"u8);
                w.Float(0.5f);
                w.Op("w"u8);
                w.Op("S"u8);
                y -= (run.FontSize * 0.5f) + 2f;
                w.Op("BT"u8);
                curFont = string.Empty;
                continue;
            }

            if (run.IsEmptyLine)
            {
                y -= run.FontSize;
                continue;
            }

            var lineHeight = run.FontSize * opts.LineSpacing;
            y -= lineHeight;

            if (run.FontKey != curFont || Math.Abs(run.FontSize - curSize) > 0.01f)
            {
                w.Name(run.FontKey);
                w.Float(run.FontSize);
                w.Op("Tf"u8);
                curFont = run.FontKey;
                curSize = run.FontSize;
            }

            w.Float(x + run.IndentPt);
            w.Float(y);
            w.Op("Td"u8);
            w.LiteralString(run.Text);
            w.Op("Tj"u8);
        }

        w.Op("ET"u8);
        return buf.WrittenMemory.ToArray();
    }

    private static byte[] BuildPageContentTagged(
        List<TextRun> runs,
        MdLoadOptions opts,
        int pageIndex,
        ICollection<TaggedContentItem> taggedItems
    )
    {
        var buf = new ArrayBufferWriter<byte>(1024);
        var w = new ContentStreamWriter(buf);

        var x = opts.MarginPt;
        var y = opts.PageHeightPt - opts.MarginPt;
        var curFont = string.Empty;
        var curSize = 0f;
        var mcid = 0;

        w.Op("BT"u8);

        foreach (var run in runs)
        {
            if (run.IsRule)
            {
                w.Op("ET"u8);
                // Rules are artifacts — not part of the logical structure.
                w.Op("/Artifact BMC"u8);
                y -= run.FontSize * 0.5f;
                w.Float(opts.MarginPt);
                w.Float(y);
                w.Op("m"u8);
                w.Float(opts.PageWidthPt - opts.MarginPt);
                w.Float(y);
                w.Op("l"u8);
                w.Float(0.5f);
                w.Op("w"u8);
                w.Op("S"u8);
                y -= (run.FontSize * 0.5f) + 2f;
                w.Op("EMC"u8);
                w.Op("BT"u8);
                curFont = string.Empty;
                continue;
            }

            if (run.IsEmptyLine)
            {
                y -= run.FontSize;
                continue;
            }

            var lineHeight = run.FontSize * opts.LineSpacing;
            y -= lineHeight;

            w.Op("ET"u8);
            w.MarkedContentBegin(run.StructTag, mcid);
            taggedItems.Add(new TaggedContentItem(run.StructTag, mcid, pageIndex));
            mcid++;
            w.Op("BT"u8);

            if (run.FontKey != curFont || Math.Abs(run.FontSize - curSize) > 0.01f)
            {
                w.Name(run.FontKey);
                w.Float(run.FontSize);
                w.Op("Tf"u8);
                curFont = run.FontKey;
                curSize = run.FontSize;
            }

            w.Float(x + run.IndentPt);
            w.Float(y);
            w.Op("Td"u8);
            w.LiteralString(run.Text);
            w.Op("Tj"u8);

            w.Op("ET"u8);
            w.MarkedContentEnd();
            w.Op("BT"u8);
        }

        w.Op("ET"u8);
        return buf.WrittenMemory.ToArray();
    }

    // ── Run collection ────────────────────────────────────────────────────────

    private sealed class TextRun(
        string text,
        string fontKey,
        float fontSize,
        float indentPt,
        bool isBreak = false,
        bool isRule = false,
        string structTag = "P"
    )
    {
        internal string Text { get; } = text;
        internal string FontKey { get; } = fontKey;
        internal float FontSize { get; } = fontSize;
        internal float IndentPt { get; } = indentPt;

        // ReSharper disable once MemberCanBePrivate.Local
        internal bool IsBreak { get; } = isBreak;
        internal bool IsRule { get; } = isRule;
        internal bool IsEmptyLine => IsBreak && !IsRule;

        /// <summary>
        ///     PDF structure type for this run when tagged output is enabled.
        ///     Standard values: "P", "H1"–"H6", "Code", "L", "LI", "LBody".
        /// </summary>
        internal string StructTag { get; } = structTag;
    }
}
