using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Xml.Linq;
using Unchained.Pdf.Abstractions;
using Unchained.Pdf.Core;
using Unchained.Pdf.Models;

namespace Unchained.Pdf.Engine.Converters;

/// <summary>
///     Converts SVG markup to a single-page PDF document.
///     <para>
///         Supported elements: <c>rect</c>, <c>circle</c>, <c>ellipse</c>, <c>line</c>,
///         <c>polyline</c>, <c>polygon</c>, <c>path</c>, <c>text</c>, <c>g</c>.
///         Coordinate system: SVG uses top-left origin (Y↓); converted to PDF bottom-left (Y↑)
///         using a <c>cm</c> transform that flips and scales the Y axis.
///     </para>
///     <para>
///         When <see cref="SvgLoadOptions.Tagged" /> is <see langword="true" />, the entire SVG
///         content is wrapped in a <c>/Figure</c> marked-content sequence with an <c>/Alt</c>
///         entry taken from <see cref="SvgLoadOptions.AltText" />.
///     </para>
/// </summary>
internal static class SvgToPdfConverter
{
    internal static IPdfDocument Convert(string svgXml, SvgLoadOptions options)
    {
        var doc = XDocument.Parse(svgXml);
        var root = doc.Root!;

        var (svgW, svgH) = ParseDimensions(root);

        // Compute the transform from SVG user units to PDF points.
        float scaleX, scaleY, offsetX, offsetY;
        if (options.FitToPage && svgW > 0 && svgH > 0)
        {
            var fitW = options.PageWidthPt - (2 * options.MarginPt);
            var fitH = options.PageHeightPt - (2 * options.MarginPt);
            var scale = Math.Min(fitW / svgW, fitH / svgH);
            scaleX = scale;
            scaleY = scale;
            offsetX = options.MarginPt + ((fitW - (svgW * scale)) / 2f);
            offsetY = options.MarginPt + ((fitH - (svgH * scale)) / 2f);
        }
        else
        {
            scaleX = scaleY = 1f;
            offsetX = options.MarginPt;
            offsetY = options.MarginPt;
        }

        var buf = new ArrayBufferWriter<byte>(4096);
        var w = new ContentStreamWriter(buf);

        List<TaggedContentItem>? taggedItems = null;

        if (options.Tagged)
        {
            taggedItems = [];
            // Wrap entire SVG in a /Figure BDC block (MCID 0, page 0).
            w.MarkedContentBegin("Figure", 0);
            taggedItems.Add(
                new TaggedContentItem("Figure", 0, 0)
                {
                    AltText = string.IsNullOrEmpty(options.AltText) ? null : options.AltText
                }
            );
        }

        // PDF CTM for SVG: flip Y, scale, and translate.
        // cm: [ scaleX 0  0  -scaleY  offsetX  (offsetY + svgH*scaleY) ]
        w.Float(scaleX);
        w.Float(0f);
        w.Float(0f);
        w.Float(-scaleY);
        w.Float(offsetX);
        w.Float(offsetY + (svgH * scaleY));
        w.Op("cm"u8);

        WalkElement(root, w, root.Name.Namespace);

        if (options.Tagged)
            w.MarkedContentEnd();

        var acc = new PdfPageAccumulator();
        var fontRef = acc.AddFont(PdfConstants.FontHelvetica);
        var fontMap = new Dictionary<string, PdfIndirectReference> { ["F1"] = fontRef };

        if (taggedItems is not null)
            // ReSharper disable once BadListLineBreaks
        {
            acc.AddPage(
                options.PageWidthPt,
                options.PageHeightPt,
                buf.WrittenMemory.Span,
                fontMap,
                taggedItems,
                options.Language
            );
        }
        else
            acc.AddPage(options.PageWidthPt, options.PageHeightPt, buf.WrittenMemory.Span, fontMap);

        return acc.Build();
    }

    // ── Element dispatch ──────────────────────────────────────────────────────

    private static void WalkElement(XContainer el, ContentStreamWriter w, XNamespace ns)
    {
        foreach (var child in el.Elements())
        {
            var local = child.Name.LocalName;
            switch (local)
            {
                case "g": EmitGroup(child, w, ns); break;
                case "rect": EmitRect(child, w); break;
                case "circle": EmitCircle(child, w); break;
                case "ellipse": EmitEllipse(child, w); break;
                case "line": EmitLine(child, w); break;
                case "polyline": EmitPolyline(child, w, false); break;
                case "polygon": EmitPolyline(child, w, true); break;
                case "path": EmitPath(child, w); break;
                case "text": EmitText(child, w); break;
            }
        }
    }

    private static void EmitGroup(XElement el, ContentStreamWriter w, XNamespace ns)
    {
        w.Op("q"u8);
        var transform = el.Attribute("transform")?.Value;
        if (transform is not null) EmitTransform(transform, w);
        ApplyStyle(el, w);
        WalkElement(el, w, ns);
        w.Op("Q"u8);
    }

    // ── Shape emitters ────────────────────────────────────────────────────────

    private static void EmitRect(XElement el, ContentStreamWriter w)
    {
        var x = F(el, "x");
        var y = F(el, "y");
        var width = F(el, "width");
        var height = F(el, "height");
        if (width <= 0 || height <= 0)
            return;

        w.Op("q"u8);
        ApplyStyle(el, w);
        w.Float(x);
        w.Float(y);
        w.Float(width);
        w.Float(height);
        w.Op("re"u8);
        EmitPaint(el, w);
        w.Op("Q"u8);
    }

    private static void EmitCircle(XElement el, ContentStreamWriter w)
    {
        var cx = F(el, "cx");
        var cy = F(el, "cy");
        var r = F(el, "r");
        if (r <= 0)
            return;

        w.Op("q"u8);
        ApplyStyle(el, w);
        EmitEllipsePath(cx, cy, r, r, w);
        EmitPaint(el, w);
        w.Op("Q"u8);
    }

    private static void EmitEllipse(XElement el, ContentStreamWriter w)
    {
        var cx = F(el, "cx");
        var cy = F(el, "cy");
        var rx = F(el, "rx");
        var ry = F(el, "ry");
        if (rx <= 0 || ry <= 0)
            return;

        w.Op("q"u8);
        ApplyStyle(el, w);
        EmitEllipsePath(cx, cy, rx, ry, w);
        EmitPaint(el, w);
        w.Op("Q"u8);
    }

    // Approximate ellipse/circle with four cubic Bézier curves (kappa = 0.5523).
    [SuppressMessage("ReSharper", "BadListLineBreaks")]
    private static void EmitEllipsePath(
        float cx,
        float cy,
        float rx,
        float ry,
        ContentStreamWriter w
    )
    {
        const float k = 0.5523f;
        w.Float(cx + rx);
        w.Float(cy);
        w.Op("m"u8);
        w.Float(cx + rx);
        w.Float(cy + (ry * k));
        w.Float(cx + (rx * k));
        w.Float(cy + ry);
        w.Float(cx);
        w.Float(cy + ry);
        w.Op("c"u8);
        w.Float(cx - (rx * k));
        w.Float(cy + ry);
        w.Float(cx - rx);
        w.Float(cy + (ry * k));
        w.Float(cx - rx);
        w.Float(cy);
        w.Op("c"u8);
        w.Float(cx - rx);
        w.Float(cy - (ry * k));
        w.Float(cx - (rx * k));
        w.Float(cy - ry);
        w.Float(cx);
        w.Float(cy - ry);
        w.Op("c"u8);
        w.Float(cx + (rx * k));
        w.Float(cy - ry);
        w.Float(cx + rx);
        w.Float(cy - (ry * k));
        w.Float(cx + rx);
        w.Float(cy);
        w.Op("c"u8);
    }

    private static void EmitLine(XElement el, ContentStreamWriter w)
    {
        w.Op("q"u8);
        ApplyStyle(el, w);
        w.Float(F(el, "x1"));
        w.Float(F(el, "y1"));
        w.Op("m"u8);
        w.Float(F(el, "x2"));
        w.Float(F(el, "y2"));
        w.Op("l"u8);
        w.Op("S"u8);
        w.Op("Q"u8);
    }

    private static void EmitPolyline(XElement el, ContentStreamWriter w, bool close)
    {
        var points = ParsePoints(el.Attribute("points")?.Value);
        if (points.Count < 2)
            return;

        w.Op("q"u8);
        ApplyStyle(el, w);
        w.Float(points[0].x);
        w.Float(points[0].y);
        w.Op("m"u8);

        for (var i = 1; i < points.Count; i++)
        {
            w.Float(points[i].x);
            w.Float(points[i].y);
            w.Op("l"u8);
        }

        if (close)
            EmitPaint(el, w);
        else
            w.Op("S"u8);
        w.Op("Q"u8);
    }

    private static void EmitPath(XElement el, ContentStreamWriter w)
    {
        var d = el.Attribute("d")?.Value;
        if (string.IsNullOrWhiteSpace(d))
            return;

        w.Op("q"u8);
        ApplyStyle(el, w);
        ParseAndEmitPathData(d, w);
        EmitPaint(el, w);
        w.Op("Q"u8);
    }

    private static void EmitText(XElement el, ContentStreamWriter w)
    {
        var text = el.Value;
        if (string.IsNullOrWhiteSpace(text)) return;

        var x = F(el, "x");
        var y = F(el, "y");
        var fontSize = ParseFontSize(el.Attribute("font-size")?.Value ?? "12");

        w.Op("q"u8);
        ApplyFill(el, w);
        w.Op("BT"u8);
        w.Name("F1");
        w.Float(fontSize);
        w.Op("Tf"u8);
        w.Float(x);
        w.Float(y);
        w.Op("Td"u8);
        w.LiteralString(text);
        w.Op("Tj"u8);
        w.Op("ET"u8);
        w.Op("Q"u8);
    }

    // ── Style helpers ─────────────────────────────────────────────────────────

    private static void ApplyStyle(XElement el, ContentStreamWriter w)
    {
        ApplyFill(el, w);
        var sw = el.Attribute("stroke-width")?.Value;
        if (sw is null || !float.TryParse(sw, NumberStyles.Float, CultureInfo.InvariantCulture, out var swf))
            return;

        w.Float(swf);
        w.Op("w"u8);
    }

    private static void ApplyFill(XElement el, ContentStreamWriter w)
    {
        var fill = el.Attribute("fill")?.Value ?? "black";
        if (fill != "none")
            EmitColor(fill, w, false);

        var stroke = el.Attribute("stroke")?.Value;
        if (stroke is not null && stroke != "none")
            EmitColor(stroke, w, true);
    }

    private static void EmitColor(string color, ContentStreamWriter w, bool stroke)
    {
        var (r, g, b) = ParseColor(color);
        w.Float(r);
        w.Float(g);
        w.Float(b);
        w.Op(stroke ? "RG"u8 : "rg"u8);
    }

    private static void EmitPaint(XElement el, ContentStreamWriter w)
    {
        var fill = el.Attribute("fill")?.Value ?? "black";
        var stroke = el.Attribute("stroke")?.Value;
        var hasFill = fill != "none";
        var hasStroke = stroke is not null && stroke != "none";

        switch (hasFill)
        {
            case true when hasStroke: w.Op("B"u8); break;
            case true: w.Op("f"u8); break;
            default:
            {
                w.Op(hasStroke ? "S"u8 : "n"u8);
                break;
            }
        }
    }

    // ── Transform ─────────────────────────────────────────────────────────────

    private static void EmitTransform(string transform, ContentStreamWriter w)
    {
        transform = transform.Trim();
        if (transform.StartsWith("translate(", StringComparison.Ordinal))
        {
            var inner = transform[10..^1];
            var parts = inner.Split([',', ' '], StringSplitOptions.RemoveEmptyEntries);
            var tx = parts.Length > 0 && float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var v0) ? v0 : 0f;
            var ty = parts.Length > 1 && float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var v1) ? v1 : 0f;
            w.Float(1f);
            w.Float(0f);
            w.Float(0f);
            w.Float(1f);
            w.Float(tx);
            w.Float(ty);
            w.Op("cm"u8);
        }
        else if (transform.StartsWith("matrix(", StringComparison.Ordinal))
        {
            var parts = transform[7..^1].Split([',', ' '], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 6)
                return;

            var values = parts
                .Select(static p => (ok: float.TryParse(p, NumberStyles.Float, CultureInfo.InvariantCulture, out var v), v))
                .Where(static t => t.ok)
                .Select(static t => t.v);
            foreach (var v in values)
                w.Float(v);

            w.Op("cm"u8);
        }
        // scale, rotate: not yet implemented — skipped gracefully.
    }

    // ── SVG path data parser ──────────────────────────────────────────────────

    private static void ParseAndEmitPathData(string d, ContentStreamWriter w)
    {
        var tokens = TokenizePath(d);
        var i = 0;
        var cx = 0f;
        var cy = 0f;
        var prevCmd = ' ';

        while (i < tokens.Count)
        {
            var t = tokens[i];
            if (t.Length == 1 && char.IsLetter(t[0]))
            {
                prevCmd = t[0];
                i++;
                continue;
            }

            var abs = char.IsUpper(prevCmd);
            switch (char.ToUpperInvariant(prevCmd))
            {
                case 'M':
                {
                    // ReSharper disable BadListLineBreaks
                    EmitXY(w, tokens, ref i, abs, ref cx, ref cy, "m"u8);
                    // ReSharper restore BadListLineBreaks
                    prevCmd = abs ? 'L' : 'l';
                    break;
                }
                case 'L':
                {
                    // ReSharper disable BadListLineBreaks
                    EmitXY(w, tokens, ref i, abs, ref cx, ref cy, "l"u8);
                    // ReSharper restore BadListLineBreaks
                    break;
                }
                case 'H':
                {
                    var x = N(tokens, i++);
                    cx = abs ? x : cx + x;
                    w.Float(cx);
                    w.Float(cy);
                    w.Op("l"u8);
                    break;
                }
                case 'V':
                {
                    var y = N(tokens, i++);
                    cy = abs ? y : cy + y;
                    w.Float(cx);
                    w.Float(cy);
                    w.Op("l"u8);
                    break;
                }
                case 'C':
                {
                    var x1 = N(tokens, i);
                    var y1 = N(tokens, i + 1);
                    var x2 = N(tokens, i + 2);
                    var y2 = N(tokens, i + 3);
                    var x = N(tokens, i + 4);
                    var y = N(tokens, i + 5);
                    i += 6;
                    if (!abs)
                    {
                        x1 += cx;
                        y1 += cy;
                        x2 += cx;
                        y2 += cy;
                        x += cx;
                        y += cy;
                    }

                    w.Float(x1);
                    w.Float(y1);
                    w.Float(x2);
                    w.Float(y2);
                    w.Float(x);
                    w.Float(y);
                    w.Op("c"u8);
                    cx = x;
                    cy = y;

                    break;
                }
                case 'Z':
                {
                    w.Op("h"u8);
                    break;
                }
                default:
                {
                    i++;
                    break;
                }
            }
        }
    }

    private static List<string> TokenizePath(string d)
    {
        var result = new List<string>();
        var i = 0;
        while (i < d.Length)
        {
            if (char.IsWhiteSpace(d[i]) || d[i] == ',')
            {
                i++;
                continue;
            }

            if (char.IsLetter(d[i]))
            {
                result.Add(d[i].ToString());
                i++;
                continue;
            }

            var j = i;
            if (d[j] == '-' || d[j] == '+')
                j++;

            while (j < d.Length && IsNumberChar(d[j], j > i))
                j++;
            result.Add(d[i..j]);
            i = j;
        }

        return result;

        static bool IsNumberChar(char ch, bool afterStart) =>
            char.IsDigit(ch) || ch == '.' || ch == 'e' || ch == 'E' || (afterStart && ch is '-' or '+');
    }

    // ── Utility ───────────────────────────────────────────────────────────────

    private static (float w, float h) ParseDimensions(XElement root)
    {
        var vb = root.Attribute("viewBox")?.Value;

        // ReSharper disable once InvertIf
        if (vb is not null)
        {
            var parts = vb.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 4 &&
                float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var vw) &&
                float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var vh))
                return (vw, vh);
        }

        return (ParseDimAttr(root, "width", 595f), ParseDimAttr(root, "height", 842f));
    }

    private static float ParseDimAttr(XElement el, string name, float fallback)
    {
        var v = el.Attribute(name)?.Value.TrimEnd('p', 't', 'x', 'm', ' ');
        return v is not null && float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var r) ? r : fallback;
    }

    private static float F(XElement el, string attr)
    {
        var v = el.Attribute(attr)?.Value;
        return v is not null && float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var r) ? r : 0f;
    }

    private static float N(IReadOnlyList<string> tokens, int i)
        => i < tokens.Count && float.TryParse(tokens[i], NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0f;

    private static float ParseFontSize(string s)
        => float.TryParse(s.TrimEnd('p', 't', 'x'), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 12f;

    private static (float r, float g, float b) ParseColor(string color)
    {
        if (!color.StartsWith('#') || color.Length < 7)
        {
            return color switch
            {
                "black" or "currentColor" => (0f, 0f, 0f),
                "white" => (1f, 1f, 1f),
                "red" => (1f, 0f, 0f),
                "green" => (0f, 0.502f, 0f),
                "blue" => (0f, 0f, 1f),
                "gray" or "grey" => (0.502f, 0.502f, 0.502f),
                _ => (0f, 0f, 0f)
            };
        }

        var r = System.Convert.ToInt32(color[1..3], 16) / 255f;
        var g = System.Convert.ToInt32(color[3..5], 16) / 255f;
        var b = System.Convert.ToInt32(color[5..7], 16) / 255f;
        return (r, g, b);
    }

    private static List<(float x, float y)> ParsePoints(string? points)
    {
        var result = new List<(float, float)>();
        if (points is null) return result;

        var nums = points.Split([',', ' '], StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i + 1 < nums.Length; i += 2)
        {
            if (float.TryParse(nums[i], NumberStyles.Float, CultureInfo.InvariantCulture, out var x) &&
                float.TryParse(nums[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
                result.Add((x, y));
        }

        return result;
    }

    // ReSharper disable once InconsistentNaming
    private static void EmitXY(
        ContentStreamWriter w,
        IReadOnlyList<string> tokens,
        ref int i,
        bool abs,
        ref float cx,
        ref float cy,
        ReadOnlySpan<byte> op
    )
    {
        var x = N(tokens, i);
        var y = N(tokens, i + 1);
        i += 2;
        cx = abs ? x : cx + x;
        cy = abs ? y : cy + y;
        w.Float(cx);
        w.Float(cy);
        w.Op(op);
    }
}
