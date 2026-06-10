using HarfBuzzSharp;
using Unchained.Drawing;
using Unchained.Drawing.Text;
using Unchained.Ooxml;
using Unchained.Ooxml.Charts;
using Unchained.Ooxml.Drawing;
using Unchained.Pptx.Core;
using Unchained.Pptx.Media;
using Unchained.Ooxml.Media;
using Unchained.Pptx.Shapes;
using Unchained.Pptx.Slides;
using Unchained.Ooxml.Text;
using LoadFlags = SharpFont.LoadFlags;
using LoadTarget = SharpFont.LoadTarget;

namespace Unchained.Pptx.Rendering.Engine;

/// <summary>
/// Rasterizes a single <see cref="Slide"/> into a <see cref="RasterBuffer"/>
/// using FreeType2 for glyph rendering and HarfBuzz for text shaping.
/// </summary>
internal sealed class SlideRasterizer(FontCache fonts, MediaStore? media = null)
{
    // Maps a coordinate-space EMU point to device pixels: px = (Scale * emu) + Offset.
    // The slide root uses Scale = px/EMU, Offset = 0; each group composes a child transform
    // onto its parent so nested shapes land in the right place.
    private readonly record struct Transform(double ScaleX, double ScaleY, double OffsetX, double OffsetY)
    {
        public int PxX(long emu) => (int)((ScaleX * emu) + OffsetX);
        public int PxY(long emu) => (int)((ScaleY * emu) + OffsetY);
        public int PxW(long emu) => (int)(ScaleX * emu);
        public int PxH(long emu) => (int)(ScaleY * emu);
    }

    // Series palette — 8 saturated colours that cycle across series in a chart.
    private static readonly (byte R, byte G, byte B)[] SeriesPalette =
    [
        (68, 114, 196), (237, 125, 49), (165, 165, 165), (255, 192, 0),
        (91, 155, 213), (112, 173, 71), (38, 68, 120), (158, 72, 14),
    ];

    internal RasterBuffer Rasterize(Slide slide, SlideSize slideSize, RenderOptions options)
    {
        var buffer = new RasterBuffer(options.WidthPx, options.HeightPx);

        // Resolve the colour scheme for this slide (slide → layout → master).
        var colorScheme = slide.Master?.Theme?.Colors;

        // Scale factor: EMU → pixels
        var scaleX = (double)options.WidthPx / slideSize.Width.Value;
        var scaleY = (double)options.HeightPx / slideSize.Height.Value;

        // Paint slide background using the inheritance chain.
        PaintBackground(buffer, slide, colorScheme);

        var root = new Transform(scaleX, scaleY, 0, 0);

        // Build a lookup table of layout placeholder shapes for geometry inheritance.
        // Key = placeholder idx, value = layout shape with that idx.
        var layoutPlaceholders = BuildLayoutPlaceholderMap(slide);

        // Composite inherited backdrop shapes from the master and layout BENEATH the slide's own
        // shapes: logos, decorative graphics, and background art live there. Placeholders are
        // skipped — the slide supplies its own (now with inherited geometry), and drawing the
        // layout's empty placeholder prompts ("Click to add title") would be wrong.
        if (slide.Layout?.Master is { } master)
            foreach (var shape in master.Shapes)
                if (!shape.IsPlaceholder)
                    RenderShape(buffer, shape, root, options.Dpi, colorScheme, layoutPlaceholders);

        if (slide.Layout is { } layout)
            foreach (var shape in layout.Shapes)
                if (!shape.IsPlaceholder)
                    RenderShape(buffer, shape, root, options.Dpi, colorScheme, layoutPlaceholders);

        // Render each shape in Z-order (insertion order = back-to-front).
        foreach (var shape in slide.Shapes)
            RenderShape(buffer, shape, root, options.Dpi, colorScheme, layoutPlaceholders);

        return buffer;
    }

    // Builds a map from placeholder index → Shape for the slide's layout (and master as fallback),
    // so zero-size placeholder shapes can inherit their geometry.
    private static Dictionary<int, Shape> BuildLayoutPlaceholderMap(Slide slide)
    {
        var map = new Dictionary<int, Shape>();
        if (slide.Layout?.Master is not null)
        {
            foreach (var s in slide.Layout.Master.Shapes)
            {
                if (s.PlaceholderIndex.HasValue && !map.ContainsKey(s.PlaceholderIndex.Value))
                    map[s.PlaceholderIndex.Value] = s;
            }
        }
        if (slide.Layout is not null)
        {
            foreach (var s in slide.Layout.Shapes)
            {
                if (s.PlaceholderIndex.HasValue)
                    map[s.PlaceholderIndex.Value] = s;
            }
        }
        return map;
    }

    // Resolves the effective background fill by walking slide → layout → master.
    private static FillFormat? ResolveBackground(Slide slide)
    {
        if (slide.Background.Fill.Type != FillType.None)
            return slide.Background.Fill;
        if (slide.Layout?.Background.Fill.Type != FillType.None)
            return slide.Layout!.Background.Fill;
        if (slide.Master?.Background.Fill.Type != FillType.None)
            return slide.Master!.Background.Fill;
        return null;
    }

    private static void PaintBackground(RasterBuffer buffer, Slide slide, ColorScheme? colorScheme)
    {
        var fill = ResolveBackground(slide);

        if (fill is null)
        {
            buffer.Clear(r: 255, g: 255, b: 255);
            return;
        }

        switch (fill.Type)
        {
            case FillType.Solid when fill.Solid is not null:
            {
                var argb = fill.Solid.Color.Resolve(colorScheme);
                ExtractArgb(argb, out _, out var r, out var g, out var b);
                buffer.Clear(r, g, b);
                break;
            }
            case FillType.Gradient when fill.Gradient is not null && fill.Gradient.Stops.Count >= 2:
            {
                var first = fill.Gradient.Stops[0].Color.Resolve(colorScheme);
                var last = fill.Gradient.Stops[^1].Color.Resolve(colorScheme);
                ExtractArgb(first, out _, out var r1, out var g1, out var b1);
                ExtractArgb(last, out _, out var r2, out var g2, out var b2);
                var h = buffer.Height;
                for (var row = 0; row < h; row++)
                {
                    var t = (double)row / Math.Max(1, h - 1);
                    var r = (byte)(r1 + ((r2 - r1) * t));
                    var g = (byte)(g1 + ((g2 - g1) * t));
                    var bv = (byte)(b1 + ((b2 - b1) * t));
                    buffer.FillRect(0, row, buffer.Width, 1, r, g, bv, 255);
                }
                break;
            }
            default:
                buffer.Clear(r: 255, g: 255, b: 255);
                break;
        }
    }

    private void RenderShape(
        RasterBuffer buffer,
        Shape shape,
        Transform transform,
        double dpi,
        ColorScheme? colorScheme = null,
        Dictionary<int, Shape>? layoutPlaceholders = null)
    {
        // Resolve geometry: if this shape is a zero-size placeholder, inherit from layout.
        if (shape.Width.Value <= 0 || shape.Height.Value <= 0)
        {
            if (shape is GroupShape)
            {
                RenderGroup(buffer, (GroupShape)shape, transform, dpi, colorScheme, layoutPlaceholders);
                return;
            }

            if (shape.PlaceholderIndex.HasValue &&
                layoutPlaceholders is not null &&
                layoutPlaceholders.TryGetValue(shape.PlaceholderIndex.Value, out var layoutShape) &&
                layoutShape.Width.Value > 0 && layoutShape.Height.Value > 0)
            {
                shape.X = layoutShape.X;
                shape.Y = layoutShape.Y;
                shape.Width = layoutShape.Width;
                shape.Height = layoutShape.Height;
            }
            else
            {
                return;
            }
        }

        var x = transform.PxX(shape.X.Value);
        var y = transform.PxY(shape.Y.Value);
        var width = transform.PxW(shape.Width.Value);
        var height = transform.PxH(shape.Height.Value);

        switch (shape)
        {
            case GroupShape group:
                RenderGroup(buffer, group, transform, dpi, colorScheme, layoutPlaceholders);
                break;

            case AutoShape autoShape when width > 0 && height > 0:
                RenderAutoShape(buffer, autoShape, x, y, width, height, dpi, colorScheme);
                break;

            case PictureShape pictureShape when width > 0 && height > 0:
                RenderPicture(buffer, pictureShape, x, y, width, height);
                break;

            case TableShape table when width > 0 && height > 0:
                RenderTable(buffer, table, x, y, width, height, dpi, colorScheme);
                break;

            case ConnectorShape connector:
                RenderConnector(buffer, connector, x, y, width, height, colorScheme);
                break;

            case ChartShape chart when width > 0 && height > 0:
                RenderChart(buffer, chart, x, y, width, height, dpi, colorScheme);
                break;

            case SmartArtShape smartArt when width > 0 && height > 0:
                RenderSmartArt(buffer, smartArt, x, y, width, height, dpi);
                break;
        }
    }

    private void RenderGroup(
        RasterBuffer buffer,
        GroupShape group,
        Transform parent,
        double dpi,
        ColorScheme? colorScheme = null,
        Dictionary<int, Shape>? layoutPlaceholders = null)
    {
        var childTransform = parent;

        var chExtW = group.ChildExtentWidth.Value;
        var chExtH = group.ChildExtentHeight.Value;
        if (chExtW > 0 && chExtH > 0 && group.Width.Value > 0 && group.Height.Value > 0)
        {
            var sx = (double)group.Width.Value / chExtW;
            var sy = (double)group.Height.Value / chExtH;
            var groupPxX = parent.PxX(group.X.Value);
            var groupPxY = parent.PxY(group.Y.Value);

            childTransform = new Transform(
                parent.ScaleX * sx,
                parent.ScaleY * sy,
                groupPxX - (parent.ScaleX * sx * group.ChildOffsetX.Value),
                groupPxY - (parent.ScaleY * sy * group.ChildOffsetY.Value));
        }

        foreach (var child in group.Children)
            RenderShape(buffer, child, childTransform, dpi, colorScheme, layoutPlaceholders);
    }

    private void RenderAutoShape(
        RasterBuffer buffer,
        AutoShape shape,
        int x, int y, int width, int height,
        double dpi,
        ColorScheme? colorScheme)
    {
        PaintFill(buffer, shape.Fill, x, y, width, height, colorScheme, shape.StyleFillColor);
        RenderTextFrame(buffer, shape.TextFrame, x, y, width, height, dpi, colorScheme,
            styleTextColor: shape.StyleTextColor);
    }

    private void RenderTable(
        RasterBuffer buffer,
        TableShape table,
        int x, int y, int width, int height,
        double dpi,
        ColorScheme? colorScheme)
    {
        var grid = table.Grid;
        if (grid.ColumnCount == 0 || grid.RowCount == 0)
            return;

        var totalW = grid.ColumnWidths.Sum(static c => c.Value);
        var totalH = grid.RowHeights.Sum(static r => r.Value);

        // Column x-edges and row y-edges in pixels. When the grid omits explicit EMU sizes
        // (common in minimal fixtures), fall back to equal distribution so the table still
        // fills its box instead of collapsing to nothing.
        var colEdges = new int[grid.ColumnCount + 1];
        colEdges[0] = x;
        for (var c = 0; c < grid.ColumnCount; c++)
        {
            var frac = totalW > 0 ? (double)grid.ColumnWidths[c].Value / totalW : 1.0 / grid.ColumnCount;
            colEdges[c + 1] = colEdges[c] + (int)(frac * width);
        }

        var rowEdges = new int[grid.RowCount + 1];
        rowEdges[0] = y;
        for (var r = 0; r < grid.RowCount; r++)
        {
            var frac = totalH > 0 ? (double)grid.RowHeights[r].Value / totalH : 1.0 / grid.RowCount;
            rowEdges[r + 1] = rowEdges[r] + (int)(frac * height);
        }

        for (var r = 0; r < grid.RowCount; r++)
        for (var c = 0; c < grid.ColumnCount; c++)
        {
            var cell = grid[c, r];
            if (cell.IsHorizontalMergeContinuation || cell.IsVerticalMergeContinuation)
                continue;

            var cx = colEdges[c];
            var cy = rowEdges[r];
            var cw = colEdges[Math.Min(c + cell.ColumnSpan, grid.ColumnCount)] - cx;
            var ch = rowEdges[Math.Min(r + cell.RowSpan, grid.RowCount)] - cy;
            if (cw <= 0 || ch <= 0)
                continue;

            PaintFill(buffer, cell.Fill, cx, cy, cw, ch, colorScheme);

            // Light grid lines so structure is visible even without explicit borders.
            buffer.FillRect(cx, cy, cw, 1, 200, 200, 200, 255);
            buffer.FillRect(cx, cy, 1, ch, 200, 200, 200, 255);
            buffer.FillRect(cx, cy + ch - 1, cw, 1, 200, 200, 200, 255);
            buffer.FillRect(cx + cw - 1, cy, 1, ch, 200, 200, 200, 255);

            RenderTextFrame(buffer, cell.TextFrame, cx + 2, cy + 2, cw - 4, ch - 4, dpi, colorScheme);
        }
    }

    // ── Connector ────────────────────────────────────────────────────────────
    private static void RenderConnector(
        RasterBuffer buffer,
        ConnectorShape shape,
        int x, int y, int width, int height,
        ColorScheme? colorScheme)
    {
        var x0 = x;
        var y0 = y;
        var x1 = x + width;
        var y1 = y + height;
        if (shape.FlipHorizontal) (x0, x1) = (x1, x0);
        if (shape.FlipVertical) (y0, y1) = (y1, y0);

        byte r = 0, g = 0, b = 0;
        if (shape.Line.Fill.Type == FillType.Solid && shape.Line.Fill.Solid is not null)
            ExtractArgb(shape.Line.Fill.Solid.Color.Resolve(colorScheme), out _, out r, out g, out b);

        var thickness = Math.Max(1, (int)Math.Round((shape.Line.WidthPoints ?? 1.0) * 1.333));
        buffer.DrawLine(x0, y0, x1, y1, r, g, b, thickness);
    }

    // ── Chart ─────────────────────────────────────────────────────────────────
    private void RenderChart(
        RasterBuffer buffer,
        ChartShape shape,
        int x, int y, int width, int height,
        double dpi,
        ColorScheme? colorScheme)
    {
        buffer.FillRect(x, y, width, height, 255, 255, 255, 255);
        DrawBorder(buffer, x, y, width, height, 180, 180, 180);

        var model = shape.Chart;
        if (model.Data.Series.Count == 0) return;

        var titleH = 0;
        if (model.HasTitle && !string.IsNullOrWhiteSpace(model.Title))
        {
            RenderTextFrameText(buffer, model.Title, x + 6, y + 4, width - 12, 14.0, dpi, 60, 60, 60);
            titleH = (int)(18 * dpi / 72.0);
        }

        var plotX = x + 8;
        var plotY = y + titleH + 6;
        var plotW = width - 16;
        var plotH = height - titleH - 16;
        if (plotW <= 4 || plotH <= 4) return;

        var series = model.Data.Series;
        var type = model.Type.ToString();
        if (type.StartsWith("Pie", StringComparison.Ordinal) || type.StartsWith("Doughnut", StringComparison.Ordinal))
            RenderPieChart(buffer, series[0], plotX, plotY, plotW, plotH);
        else if (type.StartsWith("Line", StringComparison.Ordinal) || type.StartsWith("Scatter", StringComparison.Ordinal))
            RenderLineChart(buffer, series, plotX, plotY, plotW, plotH);
        else
            RenderBarChart(buffer, series, plotX, plotY, plotW, plotH,
                horizontal: type.StartsWith("Bar", StringComparison.Ordinal));
    }

    private static void RenderBarChart(
        RasterBuffer buffer, IReadOnlyList<ChartSeries> series,
        int x, int y, int w, int h, bool horizontal)
    {
        var maxVal = series.SelectMany(static s => s.Values).DefaultIfEmpty(0).Max();
        if (maxVal <= 0) maxVal = 1;
        var categories = series.Max(static s => s.Values.Count);
        if (categories == 0) return;

        var groupGap = 4;
        var groupSpan = (horizontal ? h : w) / categories;
        var barSpan = Math.Max(1, (groupSpan - groupGap) / Math.Max(1, series.Count));

        for (var c = 0; c < categories; c++)
        {
            for (var s = 0; s < series.Count; s++)
            {
                if (c >= series[s].Values.Count) continue;
                var val = series[s].Values[c];
                var color = SeriesPalette[s % SeriesPalette.Length];

                if (horizontal)
                {
                    var barLen = (int)(val / maxVal * w);
                    var by = y + (c * groupSpan) + (s * barSpan);
                    buffer.FillRect(x, by, barLen, barSpan - 1, color.R, color.G, color.B, 255);
                }
                else
                {
                    var barLen = (int)(val / maxVal * h);
                    var bx = x + (c * groupSpan) + (s * barSpan);
                    buffer.FillRect(bx, y + h - barLen, barSpan - 1, barLen, color.R, color.G, color.B, 255);
                }
            }
        }
    }

    private static void RenderLineChart(
        RasterBuffer buffer, IReadOnlyList<ChartSeries> series,
        int x, int y, int w, int h)
    {
        var maxVal = series.SelectMany(static s => s.Values).DefaultIfEmpty(0).Max();
        if (maxVal <= 0) maxVal = 1;

        for (var s = 0; s < series.Count; s++)
        {
            var vals = series[s].Values;
            if (vals.Count < 2) continue;
            var color = SeriesPalette[s % SeriesPalette.Length];
            var stepX = (double)w / (vals.Count - 1);

            for (var i = 0; i < vals.Count - 1; i++)
            {
                var x0 = x + (int)(i * stepX);
                var x1 = x + (int)((i + 1) * stepX);
                var y0 = y + h - (int)(vals[i] / maxVal * h);
                var y1 = y + h - (int)(vals[i + 1] / maxVal * h);
                buffer.DrawLine(x0, y0, x1, y1, color.R, color.G, color.B, 2);
            }
        }
    }

    private static void RenderPieChart(
        RasterBuffer buffer, ChartSeries series, int x, int y, int w, int h)
    {
        var total = series.Values.Sum();
        if (total <= 0) return;

        var cx = x + (w / 2);
        var cy = y + (h / 2);
        var radius = Math.Min(w, h) / 2 - 2;
        if (radius <= 0) return;

        var bounds = new double[series.Values.Count + 1];
        for (var i = 0; i < series.Values.Count; i++)
            bounds[i + 1] = bounds[i] + (series.Values[i] / total);

        for (var py = -radius; py <= radius; py++)
        for (var px = -radius; px <= radius; px++)
        {
            if ((px * px) + (py * py) > radius * radius) continue;
            var angle = Math.Atan2(py, px);
            var frac = (angle + Math.PI) / (2 * Math.PI);
            var slice = 0;
            for (var i = 0; i < series.Values.Count; i++)
                if (frac >= bounds[i] && frac < bounds[i + 1]) { slice = i; break; }
            var color = SeriesPalette[slice % SeriesPalette.Length];
            buffer.BlitImagePixel(cx + px, cy + py, color.R, color.G, color.B);
        }
    }

    // ── SmartArt ──────────────────────────────────────────────────────────────
    private void RenderSmartArt(
        RasterBuffer buffer,
        SmartArtShape shape,
        int x, int y, int width, int height,
        double dpi)
    {
        var nodes = FlattenSmartArt(shape.Nodes).Where(static t => !string.IsNullOrWhiteSpace(t)).ToList();
        if (nodes.Count == 0)
        {
            DrawBorder(buffer, x, y, width, height, 180, 180, 180);
            return;
        }

        var boxH = Math.Max(12, Math.Min(48, (height - 4) / nodes.Count - 4));
        var cy = y + 2;
        for (var i = 0; i < nodes.Count; i++)
        {
            var color = SeriesPalette[i % SeriesPalette.Length];
            buffer.FillRect(x + 2, cy, width - 4, boxH, color.R, color.G, color.B, 255);
            RenderTextFrameText(buffer, nodes[i], x + 8, cy + 2, width - 16, 11.0, dpi, 255, 255, 255);
            cy += boxH + 4;
            if (cy > y + height) break;
        }
    }

    private static IEnumerable<string> FlattenSmartArt(IEnumerable<SmartArtNode> nodes)
    {
        foreach (var node in nodes)
        {
            yield return node.Text;
            foreach (var child in FlattenSmartArt(node.Children))
                yield return child;
        }
    }

    // ── Fill helper ───────────────────────────────────────────────────────────
    // Paints a FillFormat into a pixel rect, handling Solid, Gradient, and Picture fills.
    // When fill is None, falls back to the shape's StyleFillColor (from p:style/a:fillRef).
    private static void PaintFill(
        RasterBuffer buffer,
        FillFormat fill,
        int x, int y, int width, int height,
        ColorScheme? colorScheme,
        ColorSpec? styleFillColor = null)
    {
        switch (fill.Type)
        {
            case FillType.Solid when fill.Solid is not null:
            {
                var argb = fill.Solid.Color.Resolve(colorScheme);
                ExtractArgb(argb, out var a, out var r, out var g, out var b);
                buffer.FillRect(x, y, width, height, r, g, b, a);
                break;
            }
            case FillType.Gradient when fill.Gradient is not null && fill.Gradient.Stops.Count >= 2:
            {
                var stops = fill.Gradient.Stops;
                var first = stops[0].Color.Resolve(colorScheme);
                var last = stops[^1].Color.Resolve(colorScheme);
                ExtractArgb(first, out _, out var r1, out var g1, out var b1);
                ExtractArgb(last, out _, out var r2, out var g2, out var b2);
                const int bands = 8;
                for (var i = 0; i < bands; i++)
                {
                    var t = (double)i / (bands - 1);
                    var r = (byte)(r1 + ((r2 - r1) * t));
                    var g = (byte)(g1 + ((g2 - g1) * t));
                    var bv = (byte)(b1 + ((b2 - b1) * t));
                    var bandY = y + (int)((double)i / bands * height);
                    var bandH = (int)((double)(i + 1) / bands * height) - (bandY - y);
                    buffer.FillRect(x, bandY, width, Math.Max(1, bandH), r, g, bv, 255);
                }
                break;
            }
            case FillType.Picture when fill.Picture?.Image is not null:
            {
                BlitImage(buffer, fill.Picture.Image, x, y, width, height);
                break;
            }
            case FillType.None when styleFillColor.HasValue:
            {
                var argb = styleFillColor.Value.Resolve(colorScheme);
                ExtractArgb(argb, out var a, out var r, out var g, out var b);
                if (a > 0)
                    buffer.FillRect(x, y, width, height, r, g, b, a);
                break;
            }
        }
    }

    // Decodes an EmbeddedImage and blits it into the destination rect using nearest-neighbour scaling.
    private static void BlitImage(
        RasterBuffer buffer,
        EmbeddedImage image,
        int x, int y, int width, int height)
    {
        var rawPixels = TryDecodeImageToRgb(image, out var imgWidth, out var imgHeight);
        if (rawPixels is null || imgWidth <= 0 || imgHeight <= 0)
            return;

        for (var py = 0; py < height; py++)
        {
            var srcY = py * imgHeight / height;
            for (var px = 0; px < width; px++)
            {
                var srcX = px * imgWidth / width;
                var srcOffset = ((srcY * imgWidth) + srcX) * 3;
                buffer.BlitImagePixel(
                    x + px,
                    y + py,
                    rawPixels[srcOffset],
                    rawPixels[srcOffset + 1],
                    rawPixels[srcOffset + 2]
                );
            }
        }
    }

    // ── Picture ───────────────────────────────────────────────────────────────
    private static void RenderPicture(
        RasterBuffer buffer,
        PictureShape shape,
        int x, int y, int width, int height)
    {
        if (shape.Image is null)
            return;

        var imageBytes = shape.Image.Data;
        if (imageBytes.IsEmpty)
            return;

        var rawPixels = TryDecodeImageToRgb(shape.Image, out var imgWidth, out var imgHeight);
        if (rawPixels is null || imgWidth <= 0 || imgHeight <= 0)
        {
            // Undecodable format (GIF/EMF/WMF/SVG/WDP): draw a light placeholder.
            buffer.FillRect(x, y, width, height, 235, 235, 235, 255);
            DrawBorder(buffer, x, y, width, height, 170, 170, 170);
            buffer.DrawLine(x, y, x + width - 1, y + height - 1, 200, 200, 200, 1);
            buffer.DrawLine(x + width - 1, y, x, y + height - 1, 200, 200, 200, 1);
            return;
        }

        // Nearest-neighbour blit with scaling to destination rect.
        for (var py = 0; py < height; py++)
        {
            var srcY = py * imgHeight / height;
            for (var px = 0; px < width; px++)
            {
                var srcX = px * imgWidth / width;
                var srcOffset = ((srcY * imgWidth) + srcX) * 3;
                buffer.BlitImagePixel(
                    x + px,
                    y + py,
                    rawPixels[srcOffset],
                    rawPixels[srcOffset + 1],
                    rawPixels[srcOffset + 2]
                );
            }
        }
    }

    // ── Text ──────────────────────────────────────────────────────────────────

    // Renders a single line of text at a fixed size/colour (used by chart titles + SmartArt nodes).
    private void RenderTextFrameText(
        RasterBuffer buffer, string text, int x, int y, int maxW, double sizePt, double dpi,
        byte r, byte g, byte b)
    {
        var scale = dpi / 72.0;
        var lineHeight = 0;
        RenderRunText(buffer, text, "Arial", null, sizePt, scale, x, y, x + maxW, r, g, b, ref lineHeight);
    }

    private void RenderTextFrame(
        RasterBuffer buffer,
        TextFrame textFrame,
        int shapeX, int shapeY, int shapeWidth, int shapeHeight,
        double dpi,
        ColorScheme? colorScheme = null,
        ColorSpec? styleTextColor = null)
    {
        if (textFrame.Paragraphs.Count == 0)
            return;

        var scale = dpi / 72.0;
        var maxX = shapeX + shapeWidth - 4;
        var maxY = shapeY + shapeHeight - 2;

        // Default text color priority: styleTextColor → theme dk1 → black.
        uint defaultTextArgb;
        if (styleTextColor.HasValue)
            defaultTextArgb = styleTextColor.Value.Resolve(colorScheme);
        else if (colorScheme is not null)
            defaultTextArgb = colorScheme.Dark1.Resolve(colorScheme);
        else
            defaultTextArgb = 0xFF000000u;
        ExtractArgb(defaultTextArgb, out _, out var defaultR, out var defaultG, out var defaultB);

        var cursorY = shapeY + 4;

        foreach (var paragraph in textFrame.Paragraphs)
        {
            if (paragraph.Runs.Count == 0)
            {
                cursorY += (int)(12.0 * scale) + 2;
                if (cursorY > maxY) return;
                continue;
            }

            // Collect all word tokens for word-wrap.
            var tokens = new List<(string Word, string FontName, byte[]? EmbBytes, double SizePt, byte R, byte G, byte B)>();
            foreach (var run in paragraph.Runs)
            {
                if (string.IsNullOrEmpty(run.Text)) continue;

                var fontSizePt = run.Format.FontSizePoints ?? 12.0;
                byte textR, textG, textB;
                if (run.Format.Fill?.Type == FillType.Solid && run.Format.Fill.Solid is not null)
                {
                    var argb = run.Format.Fill.Solid.Color.Resolve(colorScheme);
                    ExtractArgb(argb, out _, out textR, out textG, out textB);
                }
                else
                {
                    textR = defaultR; textG = defaultG; textB = defaultB;
                }

                var fontName = run.Format.LatinFont ?? SelectFontName(run);
                var embeddedBytes = ResolveEmbeddedFont(run, fontName, out var cacheKey);

                foreach (var word in SplitIntoWords(run.Text))
                    tokens.Add((word, cacheKey, embeddedBytes, fontSizePt, textR, textG, textB));
            }

            var lineX = shapeX + 4;
            var lineHeight = 0;

            foreach (var (word, fontName, embBytes, sizePt, r, g, b) in tokens)
            {
                var pixelSize = (uint)Math.Max(1, Math.Round(sizePt * scale));
                var wordWidth = MeasureTextWidth(word, fontName, embBytes, pixelSize);

                if (lineX + wordWidth > maxX && lineX > shapeX + 4)
                {
                    cursorY += lineHeight + 2;
                    lineX = shapeX + 4;
                    lineHeight = 0;
                    if (cursorY > maxY) break;
                }

                var renderWord = lineX == shapeX + 4 ? word.TrimStart() : word;
                if (string.IsNullOrEmpty(renderWord)) continue;

                var dummy = 0;
                lineX = RenderRunText(buffer, renderWord, fontName, embBytes, sizePt, scale,
                    lineX, cursorY, maxX, r, g, b, ref dummy);
                if (dummy > lineHeight) lineHeight = dummy;
            }

            cursorY += lineHeight + 2;
            if (cursorY > maxY) return;
        }
    }

    // Splits text into words, keeping trailing spaces attached to each word.
    private static List<string> SplitIntoWords(string text)
    {
        var result = new List<string>();
        var i = 0;
        while (i < text.Length)
        {
            var start = i;
            while (i < text.Length && text[i] != ' ') i++;
            while (i < text.Length && text[i] == ' ') i++;
            if (i > start)
                result.Add(text[start..i]);
        }
        return result;
    }

    // Measures the pixel width of text using HarfBuzz advances.
    private int MeasureTextWidth(string text, string fontName, byte[]? embeddedBytes, uint pixelSize)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        try
        {
            var (ftFace, hbFont) = fonts.GetFonts(fontName, embeddedBytes);
            ftFace.SetPixelSizes(0, pixelSize);
            var hbScale = (int)(pixelSize * 64);
            hbFont.SetScale(hbScale, hbScale);

            using var hbBuffer = new HarfBuzzSharp.Buffer();
            hbBuffer.AddUtf8(System.Text.Encoding.UTF8.GetBytes(text));
            hbBuffer.GuessSegmentProperties();
            hbFont.Shape(hbBuffer);

            return hbBuffer.GlyphPositions.Sum(static p => p.XAdvance) / 64;
        }
        catch
        {
            return (int)(text.Length * pixelSize * 0.6);
        }
    }

    private int RenderRunText(
        RasterBuffer buffer,
        string text,
        string fontName,
        byte[]? embeddedBytes,
        double fontSizePt,
        double scale,
        int startX, int startY,
        int maxX,
        byte r, byte g, byte b,
        ref int lineHeight)
    {
        var cursorX = startX;

        // ReSharper disable once EmptyGeneralCatchClause
        try
        {
            var (ftFace, hbFont) = fonts.GetFonts(fontName, embeddedBytes);
            var pixelSize = (uint)Math.Max(1, Math.Round(fontSizePt * scale));
            ftFace.SetPixelSizes(0, pixelSize);

            if (lineHeight < (int)pixelSize)
                lineHeight = (int)pixelSize;

            var hbScale = (int)(pixelSize * 64);
            hbFont.SetScale(hbScale, hbScale);

            using var hbBuffer = new HarfBuzzSharp.Buffer();
            hbBuffer.AddUtf8(System.Text.Encoding.UTF8.GetBytes(text));
            hbBuffer.GuessSegmentProperties();
            hbFont.Shape(hbBuffer);

            var glyphInfos = hbBuffer.GlyphInfos;
            var glyphPositions = hbBuffer.GlyphPositions;

            for (var i = 0; i < glyphInfos.Length; i++)
            {
                var glyphId = glyphInfos[i].Codepoint;

                // ReSharper disable once EmptyGeneralCatchClause
                try
                {
                    ftFace.LoadGlyph(glyphId, LoadFlags.Render, LoadTarget.Normal);
                }
                catch
                {
                    continue;
                }

                var penX = cursorX + (glyphPositions[i].XOffset / 64);
                var penY = startY + (int)pixelSize + (glyphPositions[i].YOffset / 64);

                buffer.BlitGlyphFromFace(penX, penY, ftFace, r, g, b);

                cursorX += glyphPositions[i].XAdvance / 64;

                if (cursorX >= maxX)
                    break;
            }
        }
        catch
        {
        }

        return cursorX;
    }

    // ── Image decoding ────────────────────────────────────────────────────────

    private static byte[]? TryDecodeImageToRgb(EmbeddedImage image, out int width, out int height)
    {
        width = 0;
        height = 0;

        var bytes = image.Data.Span;
        if (bytes.IsEmpty)
            return null;

        if (IsPng(bytes))
            return PngDecoder.TryDecodeToRgb(bytes, out width, out height);
        if (IsJpeg(bytes))
            return JpegDecoder.TryDecodeToRgb(bytes, out width, out height);

        return null;
    }

    private static bool IsPng(ReadOnlySpan<byte> bytes) =>
        bytes.Length >= 8 &&
        bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47 &&
        bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A;

    private static bool IsJpeg(ReadOnlySpan<byte> bytes) =>
        bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF;

    // ── Shared helpers ────────────────────────────────────────────────────────

    private static void DrawBorder(RasterBuffer buffer, int x, int y, int w, int h, byte r, byte g, byte b)
    {
        buffer.FillRect(x, y, w, 1, r, g, b, 255);
        buffer.FillRect(x, y + h - 1, w, 1, r, g, b, 255);
        buffer.FillRect(x, y, 1, h, r, g, b, 255);
        buffer.FillRect(x + w - 1, y, 1, h, r, g, b, 255);
    }

    private static string SelectFontName(Run run)
    {
        if (run.Format.Bold == InheritableBool.True)
            return "Arial Bold";
        return "Arial";
    }

    private byte[]? ResolveEmbeddedFont(Run run, string fontName, out string cacheKey)
    {
        cacheKey = fontName;
        if (media is null || media.Fonts.Count == 0)
            return null;

        var style = ResolveStyle(run);
        var data = media.FindFontData(fontName, style);
        if (data is null)
            return null;

        cacheKey = $"{fontName}#{style}#embedded";
        return data.Value.ToArray();
    }

    private static EmbeddedFontStyle ResolveStyle(Run run)
    {
        var bold = run.Format.Bold == InheritableBool.True;
        var italic = run.Format.Italic == InheritableBool.True;
        return (bold, italic) switch
        {
            (true, true) => EmbeddedFontStyle.BoldItalic,
            (true, false) => EmbeddedFontStyle.Bold,
            (false, true) => EmbeddedFontStyle.Italic,
            _ => EmbeddedFontStyle.Regular
        };
    }

    private static void ExtractArgb(uint argb, out byte a, out byte r, out byte g, out byte b)
    {
        a = (byte)((argb >> 24) & 0xFF);
        r = (byte)((argb >> 16) & 0xFF);
        g = (byte)((argb >> 8) & 0xFF);
        b = (byte)(argb & 0xFF);
    }
}
