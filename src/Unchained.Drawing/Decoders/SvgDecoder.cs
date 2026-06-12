using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Unchained.Drawing.Extensions;

namespace Unchained.Drawing.Decoders;

/// <summary>
///     A minimal SVG rasterizer that decodes the most common SVG elements
///     (rect, circle, ellipse, line, polygon, polyline, path) to an RGB pixel array.
///     Handles viewBox scaling. Does not support CSS stylesheets, filters, or gradients.
/// </summary>
internal static class SvgDecoder
{
    /// <summary>
    ///     Decodes SVG bytes to a flat RGB array (3 bytes per pixel, row-major).
    ///     Returns null if the SVG cannot be parsed.
    /// </summary>
    public static byte[]? TryDecodeToRgb(
        ReadOnlySpan<byte> svgBytes,
        int targetWidth,
        int targetHeight,
        out int width,
        out int height
    )
    {
        width = targetWidth;
        height = targetHeight;
        try
        {
            var xml = svgBytes.FromUtf8Span();
            var doc = XDocument.Parse(xml);
            var root = doc.Root;
            if (root is null) return null;

            // Parse viewBox and SVG dimensions.
            var (vx, vy, vw, vh) = ParseViewBox(root);
            var svgW = ParseLength(root.Attribute("width")?.Value) ?? vw;
            var svgH = ParseLength(root.Attribute("height")?.Value) ?? vh;
            if (svgW <= 0)
                svgW = targetWidth;
            if (svgH <= 0)
                svgH = targetHeight;
            if (vw <= 0)
            {
                vw = svgW;
                vx = 0;
            }

            if (vh <= 0)
            {
                vh = svgH;
                vy = 0;
            }

            // Scale factor: viewBox → target pixels.
            var scaleX = targetWidth / vw;
            var scaleY = targetHeight / vh;
            var offsetX = -vx * scaleX;
            var offsetY = -vy * scaleY;

            // White background.
            var pixels = new byte[targetWidth * targetHeight * 3];
            for (var i = 0; i < pixels.Length; i++) pixels[i] = 255;

            var ctx = new RenderContext(
                pixels,
                targetWidth,
                targetHeight,
                scaleX,
                scaleY,
                offsetX,
                offsetY
            );

            // Render all child elements.
            RenderElements(root.Elements(), ctx, "none", "#000000", 1.0f);

            return pixels;
        }
        catch
        {
            return null;
        }
    }

    // ── Element rendering ─────────────────────────────────────────────────────

    private static void RenderElements(
        IEnumerable<XElement> elements,
        RenderContext ctx,
        string inheritFill,
        string inheritStroke,
        float inheritOpacity
    )
    {
        foreach (var el in elements)
        {
            var localName = el.Name.LocalName;
            var fill = el.Attribute("fill")?.Value ?? inheritFill;
            var stroke = el.Attribute("stroke")?.Value ?? inheritStroke;
            var opacity = ParseFloat(el.Attribute("opacity")?.Value) ?? inheritOpacity;
            var style = el.Attribute("style")?.Value;
            if (style is not null) ParseStyle(style, ref fill, ref stroke);

            switch (localName)
            {
                case "g":
                {
                    RenderElements(el.Elements(), ctx, fill, stroke, opacity);
                    break;
                }
                case "rect":
                {
                    RenderRect(el, ctx, fill, stroke, opacity);
                    break;
                }
                case "circle":
                {
                    RenderCircle(el, ctx, fill, stroke, opacity);
                    break;
                }
                case "ellipse":
                {
                    RenderEllipse(el, ctx, fill, opacity);
                    break;
                }
                case "line":
                {
                    RenderLine(el, ctx, stroke, opacity);
                    break;
                }
                case "polygon":
                case "polyline":
                {
                    // ReSharper disable once BadListLineBreaks
                    RenderPoly(el,
                        ctx,
                        fill,
                        stroke,
                        opacity,
                        localName == "polygon");
                    break;
                }
                case "path":
                {
                    RenderPath(el, ctx, fill, stroke, opacity);
                    break;
                }
            }
        }
    }

    private static void RenderRect(
        XElement el,
        RenderContext ctx,
        string fill,
        string stroke,
        float opacity
    )
    {
        var x = (float)(ParseLength(el.Attribute("x")?.Value) ?? 0);
        var y = (float)(ParseLength(el.Attribute("y")?.Value) ?? 0);
        var w = (float)(ParseLength(el.Attribute("width")?.Value) ?? 0);
        var h = (float)(ParseLength(el.Attribute("height")?.Value) ?? 0);
        if (w <= 0 || h <= 0) return;

        if (fill != "none")
        {
            var (r, g, b) = ParseColor(fill);
            // ReSharper disable BadListLineBreaks
            FillRect(ctx,
                x,
                y,
                w,
                h,
                r,
                g,
                b,
                opacity);
            // ReSharper restore BadListLineBreaks
        }

        // ReSharper disable once InvertIf
        if (stroke != "none" && stroke != "")
        {
            var (r, g, b) = ParseColor(stroke);
            // ReSharper disable BadListLineBreaks
            StrokeRect(ctx,
                x,
                y,
                w,
                h,
                r,
                g,
                b,
                opacity);
            // ReSharper restore BadListLineBreaks
        }
    }

    private static void RenderCircle(
        XElement el,
        RenderContext ctx,
        string fill,
        string stroke,
        float opacity
    )
    {
        var cx = (float)(ParseLength(el.Attribute("cx")?.Value) ?? 0);
        var cy = (float)(ParseLength(el.Attribute("cy")?.Value) ?? 0);
        var r = (float)(ParseLength(el.Attribute("r")?.Value) ?? 0);
        if (r <= 0) return;

        if (fill != "none")
        {
            var (fr, fg, fb) = ParseColor(fill);
            // ReSharper disable BadListLineBreaks
            FillEllipse(ctx,
                cx - r,
                cy - r,
                r * 2,
                r * 2,
                fr,
                fg,
                fb,
                opacity);
            // ReSharper restore BadListLineBreaks
        }

        // ReSharper disable once InvertIf
        if (stroke != "none" && stroke != "")
        {
            var (sr, sg, sb) = ParseColor(stroke);
            // ReSharper disable BadListLineBreaks
            StrokeEllipse(ctx,
                cx - r,
                cy - r,
                r * 2,
                r * 2,
                sr,
                sg,
                sb,
                opacity);
            // ReSharper restore BadListLineBreaks
        }
    }

    private static void RenderEllipse(
        XElement el,
        RenderContext ctx,
        string fill,
        float opacity
    )
    {
        var cx = (float)(ParseLength(el.Attribute("cx")?.Value) ?? 0);
        var cy = (float)(ParseLength(el.Attribute("cy")?.Value) ?? 0);
        var rx = (float)(ParseLength(el.Attribute("rx")?.Value) ?? 0);
        var ry2 = (float)(ParseLength(el.Attribute("ry")?.Value) ?? 0);
        if (rx <= 0 || ry2 <= 0)
            return;

        // ReSharper disable once InvertIf
        if (fill != "none")
        {
            var (fr, fg, fb) = ParseColor(fill);
            // ReSharper disable BadListLineBreaks
            FillEllipse(ctx,
                cx - rx,
                cy - ry2,
                rx * 2,
                ry2 * 2,
                fr,
                fg,
                fb,
                opacity);
            // ReSharper restore BadListLineBreaks
        }
    }

    private static void RenderLine(
        XElement el,
        RenderContext ctx,
        string stroke,
        float opacity
    )
    {
        var x1 = (float)(ParseLength(el.Attribute("x1")?.Value) ?? 0);
        var y1 = (float)(ParseLength(el.Attribute("y1")?.Value) ?? 0);
        var x2 = (float)(ParseLength(el.Attribute("x2")?.Value) ?? 0);
        var y2 = (float)(ParseLength(el.Attribute("y2")?.Value) ?? 0);

        if (stroke is "none" or "")
            return;

        var (r, g, b) = ParseColor(stroke);
        // ReSharper disable BadListLineBreaks
        DrawLine(ctx,
            x1,
            y1,
            x2,
            y2,
            r,
            g,
            b,
            opacity);
        // ReSharper restore BadListLineBreaks
    }

    private static void RenderPoly(
        XElement el,
        RenderContext ctx,
        string fill,
        string stroke,
        float opacity,
        bool close
    )
    {
        var pts = ParsePoints(el.Attribute("points")?.Value);
        if (pts.Count < 2) return;

        if (fill != "none" && close)
        {
            var (fr, fg, fb) = ParseColor(fill);
            // ReSharper disable once BadListLineBreaks
            FillPolygon(ctx,
                pts,
                fr,
                fg,
                fb,
                opacity);
        }

        // ReSharper disable once InvertIf
        if (stroke != "none" && stroke != "")
        {
            var (sr, sg, sb) = ParseColor(stroke);
            // ReSharper disable BadListLineBreaks
            for (var i = 0; i < pts.Count - 1; i++)
            {
                DrawLine(ctx,
                    pts[i].X,
                    pts[i].Y,
                    pts[i + 1].X,
                    pts[i + 1].Y,
                    sr,
                    sg,
                    sb,
                    opacity);
            }

            if (close && pts.Count > 2)
            {
                DrawLine(ctx,
                    pts[^1].X,
                    pts[^1].Y,
                    pts[0].X,
                    pts[0].Y,
                    sr,
                    sg,
                    sb,
                    opacity);
            }
            // ReSharper restore BadListLineBreaks
        }
    }

    private static void RenderPath(
        XElement el,
        RenderContext ctx,
        string fill,
        string stroke,
        float opacity
    )
    {
        var d = el.Attribute("d")?.Value;
        if (string.IsNullOrEmpty(d))
            return;

        var polygons = ParsePathToPolygons(d);

        if (fill != "none" && fill != "")
        {
            var (fr, fg, fb) = ParseColor(fill);
            foreach (var poly in polygons.Where(static poly => poly.Count >= 3))
                // ReSharper disable once BadListLineBreaks
            {
                FillPolygon(ctx,
                    poly,
                    fr,
                    fg,
                    fb,
                    opacity);
            }
        }

        // ReSharper disable once InvertIf
        if (stroke != "none" && stroke != "")
        {
            var (sr, sg, sb) = ParseColor(stroke);
            foreach (var poly in polygons)
            {
                // ReSharper disable BadListLineBreaks
                for (var i = 0; i < poly.Count - 1; i++)
                {
                    DrawLine(ctx,
                        poly[i].X,
                        poly[i].Y,
                        poly[i + 1].X,
                        poly[i + 1].Y,
                        sr,
                        sg,
                        sb,
                        opacity);
                }
                // ReSharper restore BadListLineBreaks
            }
        }
    }

    // ── Raster primitives ─────────────────────────────────────────────────────

    private static void FillRect(
        RenderContext ctx,
        float x,
        float y,
        float w,
        float h,
        byte r,
        byte g,
        byte b,
        float opacity
    )
    {
        var (px, py, pw, ph) = ctx.TransformRect(x, y, w, h);
        for (var row = py; row < py + ph; row++)
        for (var col = px; col < px + pw; col++)
        {
            ctx.BlendPixel(col,
                row,
                r,
                g,
                b,
                opacity);
        }
    }

    private static void StrokeRect(
        RenderContext ctx,
        float x,
        float y,
        float w,
        float h,
        byte r,
        byte g,
        byte b,
        float opacity
    )
    {
        // ReSharper disable BadListLineBreaks
        DrawLine(ctx,
            x,
            y,
            x + w,
            y,
            r,
            g,
            b,
            opacity);
        DrawLine(ctx,
            x + w,
            y,
            x + w,
            y + h,
            r,
            g,
            b,
            opacity);
        DrawLine(ctx,
            x + w,
            y + h,
            x,
            y + h,
            r,
            g,
            b,
            opacity);
        DrawLine(ctx,
            x,
            y + h,
            x,
            y,
            r,
            g,
            b,
            opacity);
        // ReSharper restore BadListLineBreaks
    }

    private static void FillEllipse(
        RenderContext ctx,
        float x,
        float y,
        float w,
        float h,
        byte r,
        byte g,
        byte b,
        float opacity
    )
    {
        var (px, py, pw, ph) = ctx.TransformRect(x, y, w, h);
        var cx = px + (pw / 2.0f);
        var cy = py + (ph / 2.0f);
        var rx = pw / 2.0f;
        var ry = ph / 2.0f;
        if (rx <= 0 || ry <= 0)
            return;

        for (var row = py; row < py + ph; row++)
        for (var col = px; col < px + pw; col++)
        {
            var dx = (col - cx) / rx;
            var dy = (row - cy) / ry;
            if ((dx * dx) + (dy * dy) <= 1.0f)
            {
                ctx.BlendPixel(col,
                    row,
                    r,
                    g,
                    b,
                    opacity);
            }
        }
    }

    private static void StrokeEllipse(
        RenderContext ctx,
        float x,
        float y,
        float w,
        float h,
        byte r,
        byte g,
        byte b,
        float opacity
    )
    {
        var steps = Math.Max(32, (int)(Math.PI * (w + h) / 2 * Math.Max(ctx.ScaleX, ctx.ScaleY)));
        float? prevX = null, prevY = null;
        for (var i = 0; i <= steps; i++)
        {
            var angle = 2 * Math.PI * i / steps;
            var ex = (float)(x + (w / 2) + (w / 2 * Math.Cos(angle)));
            var ey = (float)(y + (h / 2) + (h / 2 * Math.Sin(angle)));
            if (prevX.HasValue)
            {
                DrawLine(ctx,
                    prevX.Value,
                    prevY!.Value,
                    ex,
                    ey,
                    r,
                    g,
                    b,
                    opacity);
            }

            prevX = ex;
            prevY = ey;
        }
    }

    private static void DrawLine(
        RenderContext ctx,
        float x0,
        float y0,
        float x1,
        float y1,
        byte r,
        byte g,
        byte b,
        float opacity
    )
    {
        var (px0, py0) = ctx.Transform(x0, y0);
        var (px1, py1) = ctx.Transform(x1, y1);

        var dx = Math.Abs(px1 - px0);
        var dy = Math.Abs(py1 - py0);
        var sx = px0 < px1 ? 1 : -1;
        var sy = py0 < py1 ? 1 : -1;
        var err = dx - dy;
        var cx = px0;
        var cy = py0;

        for (var steps = 0; steps < 4096; steps++)
        {
            ctx.BlendPixel(cx,
                cy,
                r,
                g,
                b,
                opacity);
            if (cx == px1 && cy == py1) break;

            var e2 = 2 * err;
            if (e2 > -dy)
            {
                err -= dy;
                cx += sx;
            }

            // ReSharper disable once InvertIf
            if (e2 < dx)
            {
                err += dx;
                cy += sy;
            }
        }
    }

    private static void FillPolygon(
        RenderContext ctx,
        IReadOnlyCollection<(float X, float Y)> points,
        byte r,
        byte g,
        byte b,
        float opacity
    )
    {
        if (points.Count < 3) return;

        // Transform points to pixel space.
        var px = points.Select(p => ctx.Transform(p.X, p.Y)).ToList();

        var minY = px.Min(static p => p.Y);
        var maxY = px.Max(static p => p.Y);

        for (var scanY = minY; scanY <= maxY; scanY++)
        {
            var intersections = new List<int>();
            for (var i = 0; i < px.Count; i++)
            {
                var j = (i + 1) % px.Count;
                var y0 = px[i].Item2;
                var y1 = px[j].Item2;

                // ReSharper disable once InvertIf
                if ((y0 <= scanY && y1 > scanY) || (y1 <= scanY && y0 > scanY))
                {
                    var x = px[i].Item1 + ((scanY - y0) * (px[j].Item1 - px[i].Item1) / (y1 - y0));
                    intersections.Add(x);
                }
            }

            intersections.Sort();
            for (var k = 0; k + 1 < intersections.Count; k += 2)
            {
                for (var col = intersections[k]; col <= intersections[k + 1]; col++)
                {
                    ctx.BlendPixel(col,
                        scanY,
                        r,
                        g,
                        b,
                        opacity);
                }
            }
        }
    }

    // ── SVG path parser ───────────────────────────────────────────────────────

    private static List<List<(float X, float Y)>> ParsePathToPolygons(string d)
    {
        var result = new List<List<(float X, float Y)>>();
        var current = new List<(float X, float Y)>();
        float cx = 0, cy = 0, startX = 0, startY = 0;

        var tokens = TokenizePath(d);
        var i = 0;

        while (i < tokens.Count)
        {
            var cmd = tokens[i++];
            var isRelative = char.IsLower(cmd[0]);
            var c = char.ToUpperInvariant(cmd[0]);

            switch (c)
            {
                case 'M':
                {
                    if (current.Count > 0)
                    {
                        result.Add(current);
                        current = [];
                    }

                    var x = NextFloat(tokens, ref i);
                    var y = NextFloat(tokens, ref i);
                    if (isRelative)
                    {
                        x += cx;
                        y += cy;
                    }

                    cx = x;
                    cy = y;
                    startX = x;
                    startY = y;
                    current.Add((cx, cy));
                    // Implicit lineto after first moveto coords.
                    while (i < tokens.Count && float.TryParse(tokens[i], NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                    {
                        x = NextFloat(tokens, ref i);
                        y = NextFloat(tokens, ref i);
                        if (isRelative)
                        {
                            x += cx;
                            y += cy;
                        }

                        cx = x;
                        cy = y;
                        current.Add((cx, cy));
                    }

                    break;
                }
                case 'L':
                {
                    while (i < tokens.Count && float.TryParse(tokens[i], NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                    {
                        var x = NextFloat(tokens, ref i);
                        var y = NextFloat(tokens, ref i);
                        if (isRelative)
                        {
                            x += cx;
                            y += cy;
                        }

                        cx = x;
                        cy = y;
                        current.Add((cx, cy));
                    }

                    break;
                }
                case 'H':
                {
                    while (i < tokens.Count && float.TryParse(tokens[i], NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                    {
                        var x = NextFloat(tokens, ref i);
                        cx = isRelative ? cx + x : x;
                        current.Add((cx, cy));
                    }

                    break;
                }
                case 'V':
                {
                    while (i < tokens.Count && float.TryParse(tokens[i], NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                    {
                        var y = NextFloat(tokens, ref i);
                        cy = isRelative ? cy + y : y;
                        current.Add((cx, cy));
                    }

                    break;
                }
                case 'C':
                {
                    // Cubic Bezier — approximate with midpoint.
                    while (i < tokens.Count && float.TryParse(tokens[i], NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                    {
                        var x1 = NextFloat(tokens, ref i);
                        var y1 = NextFloat(tokens, ref i);
                        var x2 = NextFloat(tokens, ref i);
                        var y2 = NextFloat(tokens, ref i);
                        var x = NextFloat(tokens, ref i);
                        var y = NextFloat(tokens, ref i);
                        if (isRelative)
                        {
                            x1 += cx;
                            y1 += cy;
                            x2 += cx;
                            y2 += cy;
                            x += cx;
                            y += cy;
                        }

                        AppendBezierCubic(current,
                            cx,
                            cy,
                            x1,
                            y1,
                            x2,
                            y2,
                            x,
                            y);
                        cx = x;
                        cy = y;
                    }

                    break;
                }
                case 'Q':
                {
                    // Quadratic Bezier.
                    while (i < tokens.Count && float.TryParse(tokens[i], NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                    {
                        var x1 = NextFloat(tokens, ref i);
                        var y1 = NextFloat(tokens, ref i);
                        var x = NextFloat(tokens, ref i);
                        var y = NextFloat(tokens, ref i);
                        if (isRelative)
                        {
                            x1 += cx;
                            y1 += cy;
                            x += cx;
                            y += cy;
                        }

                        AppendBezierQuadratic(current,
                            cx,
                            cy,
                            x1,
                            y1,
                            x,
                            y);
                        cx = x;
                        cy = y;
                    }

                    break;
                }
                case 'A':
                {
                    // Arc — approximate as line to endpoint.
                    while (i < tokens.Count && float.TryParse(tokens[i], NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                    {
                        NextFloat(tokens, ref i);
                        NextFloat(tokens, ref i); // rx, ry
                        NextFloat(tokens, ref i);
                        NextFloat(tokens, ref i);
                        NextFloat(tokens, ref i); // rotation, largeArc, sweep
                        var x = NextFloat(tokens, ref i);
                        var y = NextFloat(tokens, ref i);
                        if (isRelative)
                        {
                            x += cx;
                            y += cy;
                        }

                        AppendArc(current,
                            cx,
                            cy,
                            x,
                            y,
                            8);
                        cx = x;
                        cy = y;
                    }

                    break;
                }
                case 'S':
                {
                    // Smooth cubic — treat as cubic with first control = current.
                    while (i < tokens.Count && float.TryParse(tokens[i], NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                    {
                        var x2 = NextFloat(tokens, ref i);
                        var y2 = NextFloat(tokens, ref i);
                        var x = NextFloat(tokens, ref i);
                        var y = NextFloat(tokens, ref i);
                        if (isRelative)
                        {
                            x2 += cx;
                            y2 += cy;
                            x += cx;
                            y += cy;
                        }

                        AppendBezierCubic(current,
                            cx,
                            cy,
                            cx,
                            cy,
                            x2,
                            y2,
                            x,
                            y);
                        cx = x;
                        cy = y;
                    }

                    break;
                }
                case 'Z':
                {
                    current.Add((startX, startY));
                    result.Add(current);
                    current = [];
                    cx = startX;
                    cy = startY;
                    break;
                }
            }
        }

        if (current.Count >= 2) result.Add(current);
        return result;
    }

    private static void AppendBezierCubic(
        ICollection<(float X, float Y)> pts,
        float x0,
        float y0,
        float x1,
        float y1,
        float x2,
        float y2,
        float x3,
        float y3,
        int steps = 12
    )
    {
        for (var step = 1; step <= steps; step++)
        {
            var t = (float)step / steps;
            var mt = 1 - t;
            var x = (mt * mt * mt * x0) + (3 * mt * mt * t * x1) + (3 * mt * t * t * x2) + (t * t * t * x3);
            var y = (mt * mt * mt * y0) + (3 * mt * mt * t * y1) + (3 * mt * t * t * y2) + (t * t * t * y3);
            pts.Add((x, y));
        }
    }

    private static void AppendBezierQuadratic(
        ICollection<(float X, float Y)> pts,
        float x0,
        float y0,
        float x1,
        float y1,
        float x2,
        float y2,
        int steps = 8
    )
    {
        for (var step = 1; step <= steps; step++)
        {
            var t = (float)step / steps;
            var mt = 1 - t;
            var x = (mt * mt * x0) + (2 * mt * t * x1) + (t * t * x2);
            var y = (mt * mt * y0) + (2 * mt * t * y1) + (t * t * y2);
            pts.Add((x, y));
        }
    }

    private static void AppendArc(
        ICollection<(float X, float Y)> pts,
        float x0,
        float y0,
        float x1,
        float y1,
        int steps
    )
    {
        for (var step = 1; step <= steps; step++)
        {
            var t = (float)step / steps;
            pts.Add((x0 + ((x1 - x0) * t), y0 + ((y1 - y0) * t)));
        }
    }

    // ── SVG path tokenizer ────────────────────────────────────────────────────

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

            // Number (possibly starting with '-').
            var start = i;
            if (d[i] == '-') i++;
            while (i < d.Length && (char.IsDigit(d[i]) || d[i] == '.')) i++;
            // Handle scientific notation.
            if (i < d.Length && (d[i] == 'e' || d[i] == 'E'))
            {
                i++;
                if (i < d.Length && (d[i] == '+' || d[i] == '-')) i++;
                while (i < d.Length && char.IsDigit(d[i])) i++;
            }

            if (i > start) result.Add(d[start..i]);
            else i++; // skip unknown char
        }

        return result;
    }

    // ── Color and attribute parsing ───────────────────────────────────────────

    private static (byte R, byte G, byte B) ParseColor(string color)
    {
        if (string.IsNullOrEmpty(color) || color == "none") return (0, 0, 0);

        // #RGB or #RRGGBB
        if (color.StartsWith('#'))
        {
            var hex = color[1..];
            if (hex.Length == 3) hex = $"{hex[0]}{hex[0]}{hex[1]}{hex[1]}{hex[2]}{hex[2]}";
            if (hex.Length >= 6 && uint.TryParse(hex[..6], NumberStyles.HexNumber, null, out var v))
                return ((byte)((v >> 16) & 0xFF), (byte)((v >> 8) & 0xFF), (byte)(v & 0xFF));
        }

        // rgb(r,g,b)
        var rgbMatch = Regex.Match(color, @"rgb\(\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*\)");
        return rgbMatch.Success
            ? (byte.Parse(rgbMatch.Groups[1].Value),
                byte.Parse(rgbMatch.Groups[2].Value),
                byte.Parse(rgbMatch.Groups[3].Value))
            : ((byte R, byte G, byte B))(
                // Named colors (most common ones)
                color.ToLowerInvariant() switch
                {
                    "black" => (0, 0, 0),
                    "white" => (255, 255, 255),
                    "red" => (255, 0, 0),
                    "green" => (0, 128, 0),
                    "blue" => (0, 0, 255),
                    "gray" or "grey" => (128, 128, 128),
                    "darkgray" or "darkgrey" => (64, 64, 64),
                    "lightgray" or "lightgrey" => (192, 192, 192),
                    "yellow" => (255, 255, 0),
                    "orange" => (255, 165, 0),
                    "purple" => (128, 0, 128),
                    "transparent" => (255, 255, 255),
                    _ => (80, 80, 80) // unknown color — dark grey
                });
    }

    private static void ParseStyle(string style, ref string fill, ref string stroke)
    {
        foreach (var part in style.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = part.Split(':', 2);
            if (kv.Length != 2) continue;

            var key = kv[0].Trim();
            var val = kv[1].Trim();

            switch (key)
            {
                case "fill":
                    fill = val;
                break;
                case "stroke":
                    stroke = val;
                break;
            }
        }
    }

    private static double? ParseLength(string? value)
    {
        if (string.IsNullOrEmpty(value)) return null;

        value = value.TrimEnd('p', 'x', 'e', 'm', '%');
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;
    }

    private static float? ParseFloat(string? value)
    {
        if (string.IsNullOrEmpty(value)) return null;

        return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;
    }

    private static List<(float X, float Y)> ParsePoints(string? value)
    {
        var result = new List<(float X, float Y)>();
        if (string.IsNullOrEmpty(value)) return result;

        var nums = Regex.Split(value.Trim(), @"[\s,]+")
            .Where(static s => s.Length > 0)
            .Select(static s => float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0f)
            .ToList();
        for (var i = 0; i + 1 < nums.Count; i += 2)
            result.Add((nums[i], nums[i + 1]));
        return result;
    }

    private static (double Vx, double Vy, double Vw, double Vh) ParseViewBox(XElement root)
    {
        var vb = root.Attribute("viewBox")?.Value;
        if (string.IsNullOrEmpty(vb)) return (0, 0, 0, 0);

        var parts = Regex.Split(vb.Trim(), @"[\s,]+");

        return parts.Length >= 4 &&
               double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x) &&
               double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y) &&
               double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var w) &&
               double.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var h)
            ? (x, y, w, h)
            : (0, 0, 0, 0);
    }

    private static float NextFloat(IReadOnlyList<string> tokens, ref int i)
    {
        if (i >= tokens.Count)
            return 0;

        var tok = tokens[i++];
        return float.TryParse(tok, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0f;
    }

    // ── Render context ────────────────────────────────────────────────────────

    private sealed class RenderContext(
        IList<byte> pixels,
        int width,
        int height,
        double scaleX,
        double scaleY,
        double offsetX,
        double offsetY
    )
    {
        public double ScaleX => scaleX;
        public double ScaleY => scaleY;

        public (int X, int Y) Transform(float x, float y) =>
            ((int)((x * scaleX) + offsetX), (int)((y * scaleY) + offsetY));

        public (int X, int Y, int W, int H) TransformRect(
            float x,
            float y,
            float w,
            float h
        )
        {
            var (px, py) = Transform(x, y);
            var pw = (int)Math.Max(1, w * scaleX);
            var ph = (int)Math.Max(1, h * scaleY);
            return (px, py, pw, ph);
        }

        public void BlendPixel(
            int x,
            int y,
            byte r,
            byte g,
            byte b,
            float opacity
        )
        {
            if (x < 0 || x >= width || y < 0 || y >= height)
                return;

            var idx = ((y * width) + x) * 3;
            if (opacity >= 1.0f)
            {
                pixels[idx] = r;
                pixels[idx + 1] = g;
                pixels[idx + 2] = b;
            }
            else
            {
                pixels[idx] = (byte)((pixels[idx] * (1 - opacity)) + (r * opacity));
                pixels[idx + 1] = (byte)((pixels[idx + 1] * (1 - opacity)) + (g * opacity));
                pixels[idx + 2] = (byte)((pixels[idx + 2] * (1 - opacity)) + (b * opacity));
            }
        }
    }
}
