using System.Buffers;
using System.Globalization;
using System.Xml.Linq;
using Unchained.Pdf.Abstractions;
using Unchained.Pdf.Core;
using Unchained.Pdf.Document;
using Unchained.Pdf.Engine.Converters;
using Unchained.Pdf.Engine.PageResources;

namespace Unchained.Pdf.Engine;

/// <summary>
///     Converts between <see cref="IPdfDocument" /> and Unchained's document XML schema.
///     <para>
///         <b>Schema overview</b> — a document is represented as:
///         <code>
/// &lt;Document&gt;
///   &lt;Page width="595" height="842"&gt;
///     &lt;Paragraph font="Helvetica" size="12" x="72" y="770"&gt;text&lt;/Paragraph&gt;
///     &lt;Heading level="1" font="Helvetica-Bold" size="22" x="72" y="750"&gt;text&lt;/Heading&gt;
///     &lt;Table x="72" y="600" columns="3"&gt;
///       &lt;Header&gt;Col1&lt;/Header&gt;
///       &lt;Header&gt;Col2&lt;/Header&gt;
///       &lt;Row&gt;&lt;Cell&gt;A&lt;/Cell&gt;&lt;Cell&gt;B&lt;/Cell&gt;&lt;/Row&gt;
///     &lt;/Table&gt;
///   &lt;/Page&gt;
/// &lt;/Document&gt;
/// </code>
///     </para>
///     <para>
///         <b>SaveXml</b> derives the XML from the parsed text spans and content operators of each page.
///         <b>BindXml / LoadFromXml</b> renders each page element into a PDF content stream.
///     </para>
/// </summary>
internal static class XmlDocumentConverter
{
    // ── SaveXml ───────────────────────────────────────────────────────────────

    /// <summary>
    ///     Serializes the structure of <paramref name="core" /> to the Unchained document XML schema
    ///     and returns the UTF-8 encoded XML string.
    /// </summary>
    internal static string SaveXml(PdfDocumentCore core)
    {
        var docEl = new XElement("Document");

        for (var pageNum = 1; pageNum <= core.PageCount; pageNum++)
        {
            var pageDict = core.GetPage(pageNum);
            var width = GetMediaBoxDimension(pageDict, 2);
            var height = GetMediaBoxDimension(pageDict, 3);

            var pageEl = new XElement("Page",
                new XAttribute("width", width.ToString("G", CultureInfo.InvariantCulture)),
                new XAttribute("height", height.ToString("G", CultureInfo.InvariantCulture)));

            // Emit text spans as Paragraph elements.
            try
            {
                var pd = core.GetPage(pageNum);
                var pageAdapter = new PdfPageAdapter(pd, pageNum, core);
                var spans = pageAdapter.GetTextSpans();

                foreach (var span in spans.Where(static span => !string.IsNullOrWhiteSpace(span.Text)))
                {
                    pageEl.Add(new XElement("Paragraph",
                        // ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
                        new XAttribute("font", span.FontName ?? "Helvetica"),
                        new XAttribute("size",
                            span.FontSize.ToString("G", CultureInfo.InvariantCulture)),
                        new XAttribute("x",
                            span.X.ToString("G", CultureInfo.InvariantCulture)),
                        new XAttribute("y",
                            span.Y.ToString("G", CultureInfo.InvariantCulture)),
                        span.Text));
                }
            }
            catch
            {
                // Skip pages that cannot be parsed — emit empty page element.
            }

            docEl.Add(pageEl);
        }

        return docEl.ToString(SaveOptions.OmitDuplicateNamespaces);
    }

    // ── LoadFromXml (BindXml) ─────────────────────────────────────────────────

    /// <summary>
    ///     Parses an Unchained document XML string and produces an <see cref="IPdfDocument" />.
    ///     Supports: <c>Page</c>, <c>Paragraph</c>, <c>Heading</c>, <c>Table</c>.
    /// </summary>
    internal static IPdfDocument LoadFromXml(string xmlContent)
    {
        var doc = XDocument.Parse(xmlContent);
        var root = doc.Root ?? throw new InvalidOperationException("XML document has no root element.");

        if (!root.Name.LocalName.Equals("Document", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Root element must be <Document>, got <{root.Name.LocalName}>.");

        var acc = new PdfPageAccumulator();

        foreach (var pageEl in root.Elements().Where(static e => e.Name.LocalName.Equals("Page", StringComparison.OrdinalIgnoreCase)))
        {
            var width = FloatAttr(pageEl, "width", 595f);
            var height = FloatAttr(pageEl, "height", 842f);

            var fontRefs = new Dictionary<string, PdfIndirectReference>();
            var usedFonts = new HashSet<string>(StringComparer.Ordinal);

            // Pre-scan for fonts used on this page.
            foreach (var font in pageEl.Elements().Select(static el => el.Attribute("font")?.Value ?? "Helvetica"))
                usedFonts.Add(font);

            foreach (var font in usedFonts)
                fontRefs[font] = acc.AddFont(font);

            var buf = new ArrayBufferWriter<byte>(4096);
            var csw = new ContentStreamWriter(buf);

            foreach (var el in pageEl.Elements())
            {
                var localName = el.Name.LocalName.ToLowerInvariant();
                switch (localName)
                {
                    case "paragraph":
                    {
                        EmitText(csw, el, height, fontRefs, false);
                        break;
                    }
                    case "heading":
                    {
                        EmitText(csw, el, height, fontRefs, true);
                        break;
                    }
                    case "table":
                    {
                        EmitTable(csw, el, height, fontRefs);
                        break;
                    }
                    case "line":
                    {
                        EmitLine(csw, el);
                        break;
                    }
                }
            }

            // Convert fontRefs to string→ref map for PdfPageAccumulator.
            var fontMap = fontRefs.ToDictionary(
                static kv => kv.Key,
                static kv => kv.Value);

            acc.AddPage(width, height, buf.WrittenMemory.Span, fontMap);
        }

        return acc.Build();
    }

    // ── Content emitters ──────────────────────────────────────────────────────

    private static void EmitText(
        ContentStreamWriter csw,
        XElement el,
        float pageHeight,
        IReadOnlyDictionary<string, PdfIndirectReference> fontRefs,
        bool isBold
    )
    {
        var text = el.Value;
        if (string.IsNullOrEmpty(text)) return;

        var font = el.Attribute("font")?.Value ?? (isBold ? "Helvetica-Bold" : "Helvetica");
        var size = FloatAttr(el, "size", 12f);
        var x = FloatAttr(el, "x", 72f);
        // XML Y is top-down; PDF Y is bottom-up — flip if the value looks like top-down.
        var rawY = FloatAttr(el, "y", pageHeight - 72f - size);

        // Use the font key that exists in fontRefs (may differ from requested).
        var fontKey = fontRefs.ContainsKey(font)
            ? font
            : fontRefs.Keys.FirstOrDefault() ?? "Helvetica";

        csw.Op("BT"u8);
        csw.Name(fontKey);
        csw.Float(size);
        csw.Op("Tf"u8);
        csw.Float(1f);
        csw.Float(0f);
        csw.Float(0f);
        csw.Float(1f);
        csw.Float(x);
        csw.Float(rawY);
        csw.Op("Tm"u8);
        csw.LiteralString(text);
        csw.Op("Tj"u8);
        csw.Op("ET"u8);
    }

    private static void EmitTable(
        ContentStreamWriter csw,
        XElement el,
        float pageHeight,
        IReadOnlyDictionary<string, PdfIndirectReference> fontRefs
    )
    {
        var x = FloatAttr(el, "x", 72f);
        var startY = FloatAttr(el, "y", pageHeight / 2f);
        const float rowHeight = 20f;
        const float colWidth = 120f;
        const float fontSize = 10f;

        var headers = el.Elements()
            .Where(static e => e.Name.LocalName.Equals("Header", StringComparison.OrdinalIgnoreCase))
            .Select(static e => e.Value)
            .ToList();

        var rows = el.Elements()
            .Where(static e => e.Name.LocalName.Equals("Row", StringComparison.OrdinalIgnoreCase))
            .Select(static e =>
                e.Elements()
                    .Where(static c => c.Name.LocalName.Equals("Cell", StringComparison.OrdinalIgnoreCase))
                    .Select(static c => c.Value)
                    .ToList())
            .ToList();

        var fontKey = fontRefs.Keys.FirstOrDefault() ?? "Helvetica";
        var boldKey = fontRefs.Keys.FirstOrDefault(static k =>
            k.Contains("Bold", StringComparison.OrdinalIgnoreCase)) ?? fontKey;

        var curY = startY;

        // Headers.
        if (headers.Count > 0)
        {
            csw.Op("BT"u8);
            csw.Name(boldKey);
            csw.Float(fontSize);
            csw.Op("Tf"u8);
            for (var c = 0; c < headers.Count; c++)
            {
                csw.Float(1f);
                csw.Float(0f);
                csw.Float(0f);
                csw.Float(1f);
                csw.Float(x + (c * colWidth));
                csw.Float(curY);
                csw.Op("Tm"u8);
                csw.LiteralString(headers[c]);
                csw.Op("Tj"u8);
            }

            csw.Op("ET"u8);
            curY -= rowHeight;
        }

        // Data rows.
        foreach (var row in rows)
        {
            csw.Op("BT"u8);
            csw.Name(fontKey);
            csw.Float(fontSize);
            csw.Op("Tf"u8);
            for (var c = 0; c < row.Count; c++)
            {
                csw.Float(1f);
                csw.Float(0f);
                csw.Float(0f);
                csw.Float(1f);
                csw.Float(x + (c * colWidth));
                csw.Float(curY);
                csw.Op("Tm"u8);
                csw.LiteralString(row[c]);
                csw.Op("Tj"u8);
            }

            csw.Op("ET"u8);
            curY -= rowHeight;
        }
    }

    private static void EmitLine(ContentStreamWriter csw, XElement el)
    {
        var x1 = FloatAttr(el, "x1", 72f);
        var y1 = FloatAttr(el, "y1", 400f);
        var x2 = FloatAttr(el, "x2", 523f);
        var y2 = FloatAttr(el, "y2", 400f);
        var width = FloatAttr(el, "width", 0.5f);

        csw.Float(width);
        csw.Op("w"u8);
        csw.Float(x1);
        csw.Float(y1);
        csw.Op("m"u8);
        csw.Float(x2);
        csw.Float(y2);
        csw.Op("l"u8);
        csw.Op("S"u8);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static float FloatAttr(XElement el, string name, float fallback)
    {
        var val = el.Attribute(name)?.Value;
        return val is not null && float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)
            ? v
            : fallback;
    }

    private static float GetMediaBoxDimension(PdfDictionary pageDict, int index)
    {
        var mb = pageDict.Get<PdfArray>(PdfName.MediaBox);
        return mb is null || mb.Count <= index ? index == 2 ? 595f : 842f : (float)mb[index].ReadIntOrReal(index == 2 ? 595f : 842f);
    }
}
