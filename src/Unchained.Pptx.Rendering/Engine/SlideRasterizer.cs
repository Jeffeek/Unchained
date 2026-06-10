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
    /// <summary>
    /// Renders the slide and returns a pixel buffer ready for encoding.
    /// </summary>
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

    internal RasterBuffer Rasterize(Slide slide, SlideSize slideSize, RenderOptions options)
    {
        var buffer = new RasterBuffer(options.WidthPx, options.HeightPx);

        // Scale factor: EMU → pixels
        var scaleX = (double)options.WidthPx / slideSize.Width.Value;
        var scaleY = (double)options.HeightPx / slideSize.Height.Value;

        // Paint slide background
        PaintBackground(buffer, slide, scaleX, scaleY);

        var root = new Transform(scaleX, scaleY, 0, 0);

        // Composite inherited backdrop shapes from the master and layout BENEATH the slide's own
        // shapes: logos, decorative graphics, and background art live there. Placeholders are
        // skipped — the slide supplies its own (now with inherited geometry), and drawing the
        // layout's empty placeholder prompts ("Click to add title") would be wrong.
        if (slide.Layout?.Master is { } master)
            foreach (var shape in master.Shapes)
                if (!shape.IsPlaceholder)
                    RenderShape(buffer, shape, root, options.Dpi);

        if (slide.Layout is { } layout)
            foreach (var shape in layout.Shapes)
                if (!shape.IsPlaceholder)
                    RenderShape(buffer, shape, root, options.Dpi);

        // Render each slide shape in Z-order (insertion order = back-to-front)
        foreach (var shape in slide.Shapes)
            RenderShape(buffer, shape, root, options.Dpi);

        return buffer;
    }

    private static void PaintBackground(RasterBuffer buffer, Slide slide, double scaleX, double scaleY)
    {
        var fill = slide.Background.Fill;
        if (fill.Type == FillType.Solid && fill.Solid is not null)
        {
            var argb = fill.Solid.Color.Resolve(null);
            ExtractArgb(argb, out var a, out var r, out var g, out var b);
            buffer.Clear(r, g, b);
        }
        else
        {
            // Default: white background
            buffer.Clear(r: 255, g: 255, b: 255);
        }
    }

    private void RenderShape(
        RasterBuffer buffer,
        Shape shape,
        Transform transform,
        double dpi)
    {
        var x = transform.PxX(shape.X.Value);
        var y = transform.PxY(shape.Y.Value);
        var width = transform.PxW(shape.Width.Value);
        var height = transform.PxH(shape.Height.Value);

        switch (shape)
        {
            case GroupShape group:
                RenderGroup(buffer, group, transform, dpi);
                break;

            case AutoShape autoShape when width > 0 && height > 0:
                RenderAutoShape(buffer, autoShape, x, y, width, height, dpi);
                break;

            case PictureShape pictureShape when width > 0 && height > 0:
                RenderPicture(buffer, pictureShape, x, y, width, height);
                break;

            case TableShape table when width > 0 && height > 0:
                RenderTable(buffer, table, x, y, width, height, dpi);
                break;

            case ConnectorShape connector:
                RenderConnector(buffer, connector, x, y, width, height);
                break;

            case ChartShape chart when width > 0 && height > 0:
                RenderChart(buffer, chart, x, y, width, height, dpi);
                break;

            case SmartArtShape smartArt when width > 0 && height > 0:
                RenderSmartArt(buffer, smartArt, x, y, width, height, dpi);
                break;
        }
    }

    // Renders a group by composing a child-space→slide transform onto the parent transform,
    // then recursing into the children. When the group has no explicit child coordinate space
    // (chOff/chExt absent or degenerate), children use the parent transform directly — their
    // coordinates are already absolute on the slide.
    private void RenderGroup(RasterBuffer buffer, GroupShape group, Transform parent, double dpi)
    {
        var childTransform = parent;

        var chExtW = group.ChildExtentWidth.Value;
        var chExtH = group.ChildExtentHeight.Value;
        if (chExtW > 0 && chExtH > 0 && group.Width.Value > 0 && group.Height.Value > 0)
        {
            // Map child space [chOff, chOff+chExt] onto group rect [off, off+ext] on the slide,
            // then through the parent transform to pixels.
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
            RenderShape(buffer, child, childTransform, dpi);
    }

    private void RenderAutoShape(
        RasterBuffer buffer,
        AutoShape shape,
        int x, int y, int width, int height,
        double dpi)
    {
        // Paint fill
        if (shape.Fill.Type == FillType.Solid && shape.Fill.Solid is not null)
        {
            var argb = shape.Fill.Solid.Color.Resolve(null);
            ExtractArgb(argb, out var a, out var r, out var g, out var b);
            buffer.FillRect(x, y, width, height, r, g, b, a);
        }

        // Render text frame
        RenderTextFrame(buffer, shape.TextFrame, x, y, width, height, dpi);
    }

    // Renders a table: per-cell fill + text, plus light grid lines. Column/row sizes come from
    // the grid's EMU widths/heights, scaled to fit the shape rectangle so it always fills its box.
    private void RenderTable(
        RasterBuffer buffer,
        TableShape table,
        int x, int y, int width, int height,
        double dpi)
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

            // Cell fill.
            if (cell.Fill.Type == FillType.Solid && cell.Fill.Solid is not null)
            {
                var argb = cell.Fill.Solid.Color.Resolve(null);
                ExtractArgb(argb, out var a, out var fr, out var fg, out var fb);
                buffer.FillRect(cx, cy, cw, ch, fr, fg, fb, a);
            }

            // Light grid border so structure is visible even without explicit borders.
            buffer.FillRect(cx, cy, cw, 1, 200, 200, 200, 255);
            buffer.FillRect(cx, cy, 1, ch, 200, 200, 200, 255);
            buffer.FillRect(cx, cy + ch - 1, cw, 1, 200, 200, 200, 255);
            buffer.FillRect(cx + cw - 1, cy, 1, ch, 200, 200, 200, 255);

            // Cell text.
            RenderTextFrame(buffer, cell.TextFrame, cx + 2, cy + 2, cw - 4, ch - 4, dpi);
        }
    }

    // ── Connector ────────────────────────────────────────────────────────────
    // Draws the connector as a straight line across its bounding box, honouring flips so the
    // direction matches the source. Bent/curved routing is approximated by a straight segment.
    private static void RenderConnector(
        RasterBuffer buffer,
        ConnectorShape shape,
        int x, int y, int width, int height)
    {
        // A connector with no extent still has direction via its 1-D bounding box.
        var x0 = x;
        var y0 = y;
        var x1 = x + width;
        var y1 = y + height;
        if (shape.FlipHorizontal) (x0, x1) = (x1, x0);
        if (shape.FlipVertical) (y0, y1) = (y1, y0);

        byte r = 0, g = 0, b = 0;
        if (shape.Line.Fill.Type == FillType.Solid && shape.Line.Fill.Solid is not null)
            ExtractArgb(shape.Line.Fill.Solid.Color.Resolve(null), out _, out r, out g, out b);

        var thickness = Math.Max(1, (int)Math.Round((shape.Line.WidthPoints ?? 1.0) * 1.333));
        buffer.DrawLine(x0, y0, x1, y1, r, g, b, thickness);
    }

    // ── Chart ────────────────────────────────────────────────────────────────
    // Renders a lightweight visual of the chart from its data model: a framed plot area, the
    // title, and bars/columns/lines/pie slices for the first few series. Not pixel-faithful to
    // PowerPoint, but ensures chart slides carry real content instead of rendering blank.
    private void RenderChart(
        RasterBuffer buffer,
        ChartShape shape,
        int x, int y, int width, int height,
        double dpi)
    {
        // Light background + border so the chart area is always visible.
        buffer.FillRect(x, y, width, height, 255, 255, 255, 255);
        DrawBorder(buffer, x, y, width, height, 180, 180, 180);

        var model = shape.Chart;
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
        if (series.Count == 0) return;

        var type = model.Type.ToString();
        if (type.StartsWith("Pie", StringComparison.Ordinal) || type.StartsWith("Doughnut", StringComparison.Ordinal))
            RenderPieChart(buffer, series[0], plotX, plotY, plotW, plotH);
        else if (type.StartsWith("Line", StringComparison.Ordinal) || type.StartsWith("Scatter", StringComparison.Ordinal))
            RenderLineChart(buffer, series, plotX, plotY, plotW, plotH);
        else
            RenderBarChart(buffer, series, plotX, plotY, plotW, plotH,
                horizontal: type.StartsWith("Bar", StringComparison.Ordinal));
    }

    private static readonly (byte R, byte G, byte B)[] SeriesPalette =
    [
        (68, 114, 196), (237, 125, 49), (165, 165, 165), (255, 192, 0),
        (91, 155, 213), (112, 173, 71), (38, 68, 120), (158, 72, 14),
    ];

    private static void RenderBarChart(
        RasterBuffer buffer, IReadOnlyList<ChartSeries> series,
        int x, int y, int w, int h, bool horizontal)
    {
        var maxVal = series.SelectMany(static s => s.Values).DefaultIfEmpty(0).Max();
        if (maxVal <= 0) maxVal = 1;
        var categories = series.Max(static s => s.Values.Count);
        if (categories == 0) return;

        // Group bars per category; series side by side.
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

        // Sweep each pixel in the bounding circle and colour it by which slice its angle falls in.
        var bounds = new double[series.Values.Count + 1];
        for (var i = 0; i < series.Values.Count; i++)
            bounds[i + 1] = bounds[i] + (series.Values[i] / total);

        for (var py = -radius; py <= radius; py++)
        for (var px = -radius; px <= radius; px++)
        {
            if ((px * px) + (py * py) > radius * radius) continue;
            var angle = Math.Atan2(py, px); // -PI..PI
            var frac = (angle + Math.PI) / (2 * Math.PI);
            var slice = 0;
            for (var i = 0; i < series.Values.Count; i++)
                if (frac >= bounds[i] && frac < bounds[i + 1]) { slice = i; break; }
            var color = SeriesPalette[slice % SeriesPalette.Length];
            buffer.BlitImagePixel(cx + px, cy + py, color.R, color.G, color.B);
        }
    }

    // ── SmartArt ────────────────────────────────────────────────────────────────
    // Renders the diagram's node text as a vertical stack of labelled boxes inside the frame.
    // The real SmartArt layout engine is out of scope; this guarantees node text is visible.
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

    private static void DrawBorder(RasterBuffer buffer, int x, int y, int w, int h, byte r, byte g, byte b)
    {
        buffer.FillRect(x, y, w, 1, r, g, b, 255);
        buffer.FillRect(x, y + h - 1, w, 1, r, g, b, 255);
        buffer.FillRect(x, y, 1, h, r, g, b, 255);
        buffer.FillRect(x + w - 1, y, 1, h, r, g, b, 255);
    }

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
        double dpi)
    {
        if (textFrame.Paragraphs.Count == 0)
            return;

        // Default text metrics: use standard 96 DPI baseline
        var scale = dpi / 72.0;

        // Default text color: black
        byte textR = 0, textG = 0, textB = 0;

        // Top-left inset (default PowerPoint body margins: ~91440 EMU ≈ 7 pt each)
        var cursorY = shapeY + 4;

        foreach (var paragraph in textFrame.Paragraphs)
        {
            if (paragraph.Runs.Count == 0)
            {
                // Empty paragraph — advance by one line height
                cursorY += (int)(12.0 * scale) + 2;
                continue;
            }

            var cursorX = shapeX + 4;
            var lineHeight = 0;

            foreach (var run in paragraph.Runs)
            {
                if (string.IsNullOrEmpty(run.Text))
                    continue;

                // Resolve font size: run → paragraph default fallback → 12pt
                var fontSizePt = run.Format.FontSizePoints ?? 12.0;

                // Resolve text color from run fill if set
                if (run.Format.Fill?.Type == FillType.Solid && run.Format.Fill.Solid is not null)
                {
                    var argb = run.Format.Fill.Solid.Color.Resolve(null);
                    ExtractArgb(argb, out _, out textR, out textG, out textB);
                }
                else
                {
                    textR = 0; textG = 0; textB = 0;
                }

                // Select font name — use LatinFont if set, else fallback key
                var fontName = run.Format.LatinFont ?? SelectFontName(run);

                // Prefer an embedded font (matched by typeface + style) so custom fonts
                // render in their real shape instead of a bundled substitute. The cache key
                // is style-qualified so regular/bold of the same typeface don't collide.
                var embeddedBytes = ResolveEmbeddedFont(run, fontName, out var cacheKey);

                cursorX = RenderRunText(
                    buffer,
                    run.Text,
                    cacheKey,
                    embeddedBytes,
                    fontSizePt,
                    scale,
                    cursorX,
                    cursorY,
                    shapeX + shapeWidth - 4,
                    textR, textG, textB,
                    ref lineHeight
                );
            }

            cursorY += lineHeight + 2;
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

                // Pen baseline position. BlitGlyphFromFace applies BitmapLeft/BitmapTop itself,
                // reading the glyph slot at correct native struct offsets. SharpFont's managed
                // ftFace.Glyph accessor uses the wrong face->glyph offset on Windows x64
                // (FT_Long NativeLong mismatch) and yields a garbage/empty bitmap — which is
                // why text was missing from slide renders on Windows.
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

        // Decode the embedded image bytes to raw RGB pixels using BCL APIs only.
        var rawPixels = TryDecodeImageToRgb(shape.Image, out var imgWidth, out var imgHeight);
        if (rawPixels is null || imgWidth <= 0 || imgHeight <= 0)
        {
            // Undecodable format (GIF/EMF/WMF/SVG/WDP): draw a light placeholder so the picture's
            // presence and position are visible instead of leaving the slide blank.
            buffer.FillRect(x, y, width, height, 235, 235, 235, 255);
            DrawBorder(buffer, x, y, width, height, 170, 170, 170);
            // A diagonal cross hints "image not rendered".
            buffer.DrawLine(x, y, x + width - 1, y + height - 1, 200, 200, 200, 1);
            buffer.DrawLine(x + width - 1, y, x, y + height - 1, 200, 200, 200, 1);
            return;
        }

        // Nearest-neighbour blit with scaling to destination rect
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

    private static byte[]? TryDecodeImageToRgb(EmbeddedImage image, out int width, out int height)
    {
        width = 0;
        height = 0;

        var bytes = image.Data.Span;
        if (bytes.IsEmpty)
            return null;

        // PNG and baseline JPEG are decodable with BCL-only code. EMF/WMF/SVG/WDP are not
        // yet supported here — those shapes are left transparent (tracked as a known gap).
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

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static string SelectFontName(Run run)
    {
        if (run.Format.Bold == InheritableBool.True)
            return "Arial Bold";

        return "Arial";
    }

    // Looks up embedded font bytes for the run's typeface + style. Returns the bytes (or null
    // when no embedded font matches) and sets cacheKey: a style-qualified key for embedded
    // fonts so the FontCache keeps regular/bold/italic of the same typeface as distinct faces,
    // or the plain font name when falling back to a bundled substitute.
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
