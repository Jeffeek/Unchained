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

    // Series palette — 6 saturated colours that cycle across series in a chart.
    private static readonly (byte R, byte G, byte B)[] SeriesPalette =
    [
        (68, 114, 196),   // accent1-ish blue
        (237, 125, 49),   // accent2-ish orange
        (112, 173, 71),   // accent6-ish green
        (255, 192, 0),    // accent4-ish yellow
        (91, 155, 213),   // accent5-ish light-blue
        (165, 165, 165),  // neutral grey
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
        // Master shapes first (lowest priority).
        if (slide.Layout?.Master is not null)
        {
            foreach (var s in slide.Layout.Master.Shapes)
            {
                if (s.PlaceholderIndex >= 0 && !map.ContainsKey(s.PlaceholderIndex))
                    map[s.PlaceholderIndex] = s;
            }
        }
        // Layout shapes (higher priority — override master).
        if (slide.Layout is not null)
        {
            foreach (var s in slide.Layout.Shapes)
            {
                if (s.PlaceholderIndex >= 0)
                    map[s.PlaceholderIndex] = s;
            }
        }
        return map;
    }

    // Resolves the effective background fill by walking slide → layout → master.
    // Returns the first fill that is not FillType.None.
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

    private static void PaintBackground(
        RasterBuffer buffer,
        Slide slide,
        ColorScheme? colorScheme)
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
                // Simple two-stop linear gradient top-to-bottom as background approximation.
                var first = fill.Gradient.Stops[0].Color.Resolve(colorScheme);
                var last = fill.Gradient.Stops[^1].Color.Resolve(colorScheme);
                ExtractArgb(first, out _, out var r1, out var g1, out var b1);
                ExtractArgb(last, out _, out var r2, out var g2, out var b2);
                var height = buffer.Height;
                for (var row = 0; row < height; row++)
                {
                    var t = (double)row / Math.Max(1, height - 1);
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
        ColorScheme? colorScheme,
        Dictionary<int, Shape>? layoutPlaceholders = null)
    {
        // Resolve geometry: if this shape is a zero-size placeholder, inherit from layout.
        var effectiveShape = shape;
        if (shape.Width.Value <= 0 || shape.Height.Value <= 0)
        {
            if (shape is GroupShape)
            {
                // A group with no explicit extent still renders its children using the
                // parent transform directly — children carry their own absolute positions.
                RenderGroup(buffer, (GroupShape)shape, transform, dpi, colorScheme, layoutPlaceholders);
                return;
            }

            if (shape.PlaceholderIndex >= 0 &&
                layoutPlaceholders is not null &&
                layoutPlaceholders.TryGetValue(shape.PlaceholderIndex, out var layoutShape) &&
                layoutShape.Width.Value > 0 && layoutShape.Height.Value > 0)
            {
                // Clone geometry from layout placeholder onto a temporary wrapper.
                effectiveShape = CreateGeometryProxy(shape, layoutShape);
            }
            else
            {
                return; // truly zero-size and no layout fallback — skip
            }
        }

        var x = transform.PxX(effectiveShape.X.Value);
        var y = transform.PxY(effectiveShape.Y.Value);
        var width = transform.PxW(effectiveShape.Width.Value);
        var height = transform.PxH(effectiveShape.Height.Value);

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

            case ChartShape chartShape when width > 0 && height > 0:
                RenderChart(buffer, chartShape, x, y, width, height, dpi, colorScheme);
                break;
        }
    }

    // Creates a temporary proxy that uses the layout placeholder's geometry
    // but the slide shape's content (text, fill, etc.).
    private static Shape CreateGeometryProxy(Shape slideShape, Shape layoutShape)
    {
        // We return the slideShape with overridden coordinates.
        // Since Shape is abstract and we don't want to clone, we use a simple trick:
        // temporarily mutate the shape's geometry. But shapes are mutable, so we
        // instead return a lightweight record that just carries the right values.
        // The simplest approach: set the geometry on the slide shape itself (it's mutable).
        // This is safe because each shape is only rendered once per frame.
        slideShape.X = layoutShape.X;
        slideShape.Y = layoutShape.Y;
        slideShape.Width = layoutShape.Width;
        slideShape.Height = layoutShape.Height;
        return slideShape;
    }

    // Renders a group by composing a child-space→slide transform onto the parent transform,
    // then recursing into the children. When the group has no explicit child coordinate space
    // (chOff/chExt absent or degenerate), children use the parent transform directly.
    private void RenderGroup(
        RasterBuffer buffer,
        GroupShape group,
        Transform parent,
        double dpi,
        ColorScheme? colorScheme,
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

    // Renders a table: per-cell fill + text, plus light grid lines.
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
        if (totalW <= 0 || totalH <= 0)
            return;

        var colEdges = new int[grid.ColumnCount + 1];
        colEdges[0] = x;
        for (var c = 0; c < grid.ColumnCount; c++)
            colEdges[c + 1] = colEdges[c] + (int)((double)grid.ColumnWidths[c].Value / totalW * width);

        var rowEdges = new int[grid.RowCount + 1];
        rowEdges[0] = y;
        for (var r = 0; r < grid.RowCount; r++)
            rowEdges[r + 1] = rowEdges[r] + (int)((double)grid.RowHeights[r].Value / totalH * height);

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

    private void RenderTextFrame(
        RasterBuffer buffer,
        TextFrame textFrame,
        int shapeX, int shapeY, int shapeWidth, int shapeHeight,
        double dpi,
        ColorScheme? colorScheme,
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

            // Collect all (word, fontName, fontSizePt, r, g, b) tokens for word-wrap.
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

                // Split run text into words (preserving spaces as part of the preceding token).
                var words = SplitIntoWords(run.Text);
                foreach (var word in words)
                    tokens.Add((word, cacheKey, embeddedBytes, fontSizePt, textR, textG, textB));
            }

            // Word-wrap: measure each token, break line when it would overflow maxX.
            var lineX = shapeX + 4;
            var lineHeight = 0;

            foreach (var (word, fontName, embBytes, sizePt, r, g, b) in tokens)
            {
                var pixelSize = (uint)Math.Max(1, Math.Round(sizePt * scale));
                var wordWidth = MeasureTextWidth(word, fontName, embBytes, pixelSize);

                // If word doesn't fit and we're not at line start, wrap.
                if (lineX + wordWidth > maxX && lineX > shapeX + 4)
                {
                    cursorY += lineHeight + 2;
                    lineX = shapeX + 4;
                    lineHeight = 0;
                    if (cursorY > maxY) break;
                }

                // Skip leading spaces at line start.
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

    // Splits text into words, keeping trailing spaces attached to each word so
    // they contribute to the measured width and allow correct wrap decisions.
    private static List<string> SplitIntoWords(string text)
    {
        var result = new List<string>();
        var i = 0;
        while (i < text.Length)
        {
            var start = i;
            // Consume non-space chars.
            while (i < text.Length && text[i] != ' ') i++;
            // Consume trailing spaces.
            while (i < text.Length && text[i] == ' ') i++;
            if (i > start)
                result.Add(text[start..i]);
        }
        return result;
    }

    // Measures the pixel width a text string would occupy using HarfBuzz advances.
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
            // Fallback: approximate 0.6 * pixelSize per character.
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

    private static void RenderPicture(
        RasterBuffer buffer,
        PictureShape shape,
        int x, int y, int width, int height)
    {
        if (shape.Image is null)
            return;
        BlitImage(buffer, shape.Image, x, y, width, height);
    }

    // ── Chart rendering ───────────────────────────────────────────────────────

    private void RenderChart(
        RasterBuffer buffer,
        ChartShape chartShape,
        int x, int y, int width, int height,
        double dpi,
        ColorScheme? colorScheme)
    {
        var chart = chartShape.Chart;
        if (chart.Data.Series.Count == 0)
            return;

        // Draw a light grey chart area background.
        buffer.FillRect(x, y, width, height, 248, 248, 248, 255);
        // Thin border.
        buffer.FillRect(x, y, width, 1, 180, 180, 180, 255);
        buffer.FillRect(x, y, 1, height, 180, 180, 180, 255);
        buffer.FillRect(x, y + height - 1, width, 1, 180, 180, 180, 255);
        buffer.FillRect(x + width - 1, y, 1, height, 180, 180, 180, 255);

        // Reserve margins for axes and title.
        var titleH = chart.HasTitle && !string.IsNullOrEmpty(chart.Title) ? (int)(dpi / 72.0 * 11) + 4 : 0;
        var legendH = chart.Legend.IsVisible ? (int)(dpi / 72.0 * 10) + 4 : 0;
        const int marginLeft = 36;
        const int marginRight = 8;
        const int marginBottom = 20;
        const int marginTop = 4;

        var plotX = x + marginLeft;
        var plotY = y + marginTop + titleH;
        var plotW = width - marginLeft - marginRight;
        var plotH = height - marginTop - titleH - marginBottom - legendH;

        if (plotW <= 0 || plotH <= 0)
            return;

        // Draw title.
        if (titleH > 0)
            RenderChartLabel(buffer, chart.Title, x + (width / 2), y + marginTop, dpi, colorScheme,
                centered: true);

        switch (chart.Type)
        {
            case ChartType.ColumnClustered:
            case ChartType.ColumnStacked:
            case ChartType.ColumnFullStacked:
                RenderColumnChart(buffer, chart, plotX, plotY, plotW, plotH, dpi, colorScheme);
                break;

            case ChartType.BarClustered:
            case ChartType.BarStacked:
            case ChartType.BarFullStacked:
                RenderBarChart(buffer, chart, plotX, plotY, plotW, plotH, dpi, colorScheme);
                break;

            case ChartType.Line:
            case ChartType.LineWithMarkers:
            case ChartType.LineStacked:
            case ChartType.LineFullStacked:
            case ChartType.LineWithMarkersStacked:
            case ChartType.LineWithMarkersFullStacked:
                RenderLineChart(buffer, chart, plotX, plotY, plotW, plotH, dpi, colorScheme);
                break;

            case ChartType.Pie:
            case ChartType.PieExploded:
                RenderPieChart(buffer, chart, plotX, plotY, plotW, plotH, colorScheme);
                break;

            case ChartType.Doughnut:
                RenderPieChart(buffer, chart, plotX, plotY, plotW, plotH, colorScheme,
                    innerRadiusFraction: 0.5);
                break;

            default:
                // Unsupported chart type: render a placeholder.
                RenderChartPlaceholder(buffer, chart.Type.ToString(), plotX, plotY, plotW, plotH,
                    dpi, colorScheme);
                break;
        }

        // Draw axis lines.
        buffer.FillRect(plotX, plotY, 1, plotH, 160, 160, 160, 255);
        buffer.FillRect(plotX, plotY + plotH - 1, plotW, 1, 160, 160, 160, 255);

        // Draw legend if visible.
        if (legendH > 0)
            RenderChartLegend(buffer, chart, x, y + height - legendH, width, legendH, dpi, colorScheme);
    }

    private void RenderColumnChart(
        RasterBuffer buffer,
        ChartModel chart,
        int plotX, int plotY, int plotW, int plotH,
        double dpi,
        ColorScheme? colorScheme)
    {
        var data = chart.Data;
        var catCount = Math.Max(1, data.Categories.Count > 0
            ? data.Categories.Count
            : data.Series.Max(static s => s.Values.Count));
        var seriesCount = data.Series.Count;
        if (seriesCount == 0) return;

        // Find value range.
        var maxVal = data.Series.SelectMany(static s => s.Values).DefaultIfEmpty(1.0).Max();
        var minVal = Math.Min(0.0, data.Series.SelectMany(static s => s.Values).DefaultIfEmpty(0.0).Min());
        var range = maxVal - minVal;
        if (range <= 0) range = 1;

        // Horizontal grid lines (3 lines at 25/50/75% of range).
        for (var gi = 1; gi <= 3; gi++)
        {
            var gy = plotY + plotH - (int)((double)gi / 4 * plotH);
            buffer.FillRect(plotX, gy, plotW, 1, 220, 220, 220, 255);
        }

        var groupW = plotW / catCount;
        var barW = Math.Max(1, groupW / (seriesCount + 1));

        for (var si = 0; si < seriesCount; si++)
        {
            var series = data.Series[si];
            var (sr, sg, sb) = ResolveSeriesColor(series, si, colorScheme);

            for (var ci = 0; ci < catCount; ci++)
            {
                var value = ci < series.Values.Count ? series.Values[ci] : 0.0;
                var barH = (int)(Math.Abs(value) / range * plotH);
                if (barH <= 0) continue;

                var barX = plotX + (ci * groupW) + (si * barW) + (barW / 2);
                var barY = value >= 0
                    ? plotY + plotH - (int)((value - minVal) / range * plotH)
                    : plotY + plotH - (int)(-minVal / range * plotH);

                buffer.FillRect(barX, barY, barW - 1, barH, sr, sg, sb, 220);
            }
        }

        // Category labels.
        for (var ci = 0; ci < Math.Min(catCount, data.Categories.Count); ci++)
        {
            var lx = plotX + (ci * groupW) + (groupW / 2);
            var ly = plotY + plotH + 3;
            var label = TruncateLabel(data.Categories[ci], 6);
            RenderChartLabel(buffer, label, lx, ly, dpi, colorScheme, centered: true);
        }
    }

    private void RenderBarChart(
        RasterBuffer buffer,
        ChartModel chart,
        int plotX, int plotY, int plotW, int plotH,
        double dpi,
        ColorScheme? colorScheme)
    {
        var data = chart.Data;
        var catCount = Math.Max(1, data.Categories.Count > 0
            ? data.Categories.Count
            : data.Series.Max(static s => s.Values.Count));
        var seriesCount = data.Series.Count;
        if (seriesCount == 0) return;

        var maxVal = data.Series.SelectMany(static s => s.Values).DefaultIfEmpty(1.0).Max();
        var minVal = Math.Min(0.0, data.Series.SelectMany(static s => s.Values).DefaultIfEmpty(0.0).Min());
        var range = maxVal - minVal;
        if (range <= 0) range = 1;

        // Vertical grid lines.
        for (var gi = 1; gi <= 3; gi++)
        {
            var gx = plotX + (int)((double)gi / 4 * plotW);
            buffer.FillRect(gx, plotY, 1, plotH, 220, 220, 220, 255);
        }

        var groupH = plotH / catCount;
        var barH = Math.Max(1, groupH / (seriesCount + 1));

        for (var si = 0; si < seriesCount; si++)
        {
            var series = data.Series[si];
            var (sr, sg, sb) = ResolveSeriesColor(series, si, colorScheme);

            for (var ci = 0; ci < catCount; ci++)
            {
                var value = ci < series.Values.Count ? series.Values[ci] : 0.0;
                var barW = (int)(Math.Abs(value) / range * plotW);
                if (barW <= 0) continue;

                var barY = plotY + (ci * groupH) + (si * barH) + (barH / 2);
                var barX = value >= 0
                    ? plotX + (int)(-minVal / range * plotW)
                    : plotX + (int)((value - minVal) / range * plotW);

                buffer.FillRect(barX, barY, barW, barH - 1, sr, sg, sb, 220);
            }
        }

        // Category labels.
        for (var ci = 0; ci < Math.Min(catCount, data.Categories.Count); ci++)
        {
            var ly = plotY + (ci * groupH) + (groupH / 2);
            var label = TruncateLabel(data.Categories[ci], 6);
            RenderChartLabel(buffer, label, plotX - 2, ly, dpi, colorScheme, centered: false);
        }
    }

    private void RenderLineChart(
        RasterBuffer buffer,
        ChartModel chart,
        int plotX, int plotY, int plotW, int plotH,
        double dpi,
        ColorScheme? colorScheme)
    {
        var data = chart.Data;
        var catCount = Math.Max(2, data.Categories.Count > 0
            ? data.Categories.Count
            : data.Series.Max(static s => s.Values.Count));
        var seriesCount = data.Series.Count;
        if (seriesCount == 0) return;

        var maxVal = data.Series.SelectMany(static s => s.Values).DefaultIfEmpty(1.0).Max();
        var minVal = Math.Min(0.0, data.Series.SelectMany(static s => s.Values).DefaultIfEmpty(0.0).Min());
        var range = maxVal - minVal;
        if (range <= 0) range = 1;

        // Horizontal grid lines.
        for (var gi = 1; gi <= 3; gi++)
        {
            var gy = plotY + plotH - (int)((double)gi / 4 * plotH);
            buffer.FillRect(plotX, gy, plotW, 1, 220, 220, 220, 255);
        }

        for (var si = 0; si < seriesCount; si++)
        {
            var series = data.Series[si];
            var (sr, sg, sb) = ResolveSeriesColor(series, si, colorScheme);
            var pointCount = series.Values.Count;
            if (pointCount < 1) continue;

            int? prevPx = null, prevPy = null;
            for (var ci = 0; ci < pointCount; ci++)
            {
                var value = series.Values[ci];
                var px = plotX + (int)((double)ci / (catCount - 1) * plotW);
                var py = plotY + plotH - (int)((value - minVal) / range * plotH);
                py = Math.Clamp(py, plotY, plotY + plotH);

                // Draw a 2px dot at the point.
                buffer.FillRect(px - 1, py - 1, 3, 3, sr, sg, sb, 255);

                // Connect to previous point with a line (Bresenham).
                if (prevPx.HasValue)
                    DrawLine(buffer, prevPx.Value, prevPy!.Value, px, py, sr, sg, sb);

                prevPx = px;
                prevPy = py;
            }
        }
    }

    private static void RenderPieChart(
        RasterBuffer buffer,
        ChartModel chart,
        int plotX, int plotY, int plotW, int plotH,
        ColorScheme? colorScheme,
        double innerRadiusFraction = 0.0)
    {
        if (chart.Data.Series.Count == 0) return;

        var values = chart.Data.Series[0].Values;
        if (values.Count == 0) return;

        var total = values.Sum();
        if (total <= 0) return;

        var cx = plotX + (plotW / 2);
        var cy = plotY + (plotH / 2);
        var radius = Math.Min(plotW, plotH) / 2 - 4;
        if (radius <= 0) return;
        var innerRadius = (int)(radius * innerRadiusFraction);

        var angle = -Math.PI / 2; // start at top

        for (var si = 0; si < values.Count; si++)
        {
            var sweep = values[si] / total * 2 * Math.PI;
            var (sr, sg, sb) = PieSliceColor(si, colorScheme);

            // Rasterize the sector by scanning every pixel in the bounding box.
            var endAngle = angle + sweep;
            for (var py = cy - radius; py <= cy + radius; py++)
            for (var px = cx - radius; px <= cx + radius; px++)
            {
                var dx = px - cx;
                var dy = py - cy;
                var dist = Math.Sqrt((dx * dx) + (dy * dy));
                if (dist > radius || dist < innerRadius) continue;

                var pixAngle = Math.Atan2(dy, dx);
                if (!AngleInRange(pixAngle, angle, endAngle)) continue;

                buffer.BlitImagePixel(px, py, sr, sg, sb);
            }

            // Draw a thin divider line.
            var ex = cx + (int)(radius * Math.Cos(angle));
            var ey = cy + (int)(radius * Math.Sin(angle));
            DrawLine(buffer, cx, cy, ex, ey, 255, 255, 255);

            angle = endAngle;
        }
    }

    private void RenderChartPlaceholder(
        RasterBuffer buffer,
        string typeLabel,
        int plotX, int plotY, int plotW, int plotH,
        double dpi,
        ColorScheme? colorScheme)
    {
        // Draw diagonal hatching to indicate an unsupported chart type.
        for (var d = 0; d < plotW + plotH; d += 12)
        {
            var x1 = plotX + d;
            var y1 = plotY;
            var x2 = plotX;
            var y2 = plotY + d;
            DrawLine(buffer,
                Math.Min(x1, plotX + plotW - 1), Math.Max(y1, plotY),
                Math.Max(x2, plotX), Math.Min(y2, plotY + plotH - 1),
                220, 220, 220);
        }
        RenderChartLabel(buffer, typeLabel, plotX + (plotW / 2), plotY + (plotH / 2),
            dpi, colorScheme, centered: true);
    }

    private void RenderChartLegend(
        RasterBuffer buffer,
        ChartModel chart,
        int x, int y, int width, int height,
        double dpi,
        ColorScheme? colorScheme)
    {
        const int swatchSize = 8;
        const int spacing = 4;
        var cursorX = x + spacing;

        for (var si = 0; si < chart.Data.Series.Count; si++)
        {
            var series = chart.Data.Series[si];
            var (sr, sg, sb) = ResolveSeriesColor(series, si, colorScheme);

            // Color swatch.
            buffer.FillRect(cursorX, y + (height / 2) - (swatchSize / 2),
                swatchSize, swatchSize, sr, sg, sb, 255);
            cursorX += swatchSize + 2;

            // Series name.
            var label = TruncateLabel(series.Name, 12);
            var labelW = label.Length * (int)(dpi / 72.0 * 6);
            RenderChartLabel(buffer, label, cursorX, y + (height / 2), dpi, colorScheme,
                centered: false);
            cursorX += labelW + spacing * 2;

            if (cursorX > x + width - 20)
                break;
        }
    }

    // Renders a small text label at pixel (px, py). Not a full text layout — just a
    // best-effort glyphed label for axes and legend.
    private void RenderChartLabel(
        RasterBuffer buffer,
        string text,
        int px, int py,
        double dpi,
        ColorScheme? colorScheme,
        bool centered)
    {
        if (string.IsNullOrEmpty(text)) return;

        var labelArgb = colorScheme is not null
            ? colorScheme.Dark1.Resolve(colorScheme)
            : 0xFF404040u;
        ExtractArgb(labelArgb, out _, out var lr, out var lg, out var lb);

        // Clamp label color to ensure visibility.
        if (lr + lg + lb > 600) { lr = 80; lg = 80; lb = 80; }

        var lineHeight = 0;
        var startX = centered ? px - (text.Length * (int)(dpi / 72.0 * 3)) : px;
        RenderRunText(buffer, text, "Arial", null, 8.0, dpi / 72.0,
            startX, py, px + 200, lr, lg, lb, ref lineHeight);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static (byte R, byte G, byte B) ResolveSeriesColor(
        ChartSeries series,
        int seriesIndex,
        ColorScheme? colorScheme)
    {
        if (series.Fill?.Type == FillType.Solid && series.Fill.Solid is not null)
        {
            var argb = series.Fill.Solid.Color.Resolve(colorScheme);
            ExtractArgb(argb, out _, out var r, out var g, out var b);
            return (r, g, b);
        }
        return SeriesPalette[seriesIndex % SeriesPalette.Length];
    }

    private static (byte R, byte G, byte B) PieSliceColor(int index, ColorScheme? colorScheme)
    {
        // Use accent colors from the theme when available; otherwise use the palette.
        if (colorScheme is not null)
        {
            var slot = index switch
            {
                0 => ThemeColorSlot.Accent1,
                1 => ThemeColorSlot.Accent2,
                2 => ThemeColorSlot.Accent3,
                3 => ThemeColorSlot.Accent4,
                4 => ThemeColorSlot.Accent5,
                _ => ThemeColorSlot.Accent6
            };
            var argb = colorScheme[slot].Resolve(colorScheme);
            ExtractArgb(argb, out _, out var r, out var g, out var b);
            return (r, g, b);
        }
        return SeriesPalette[index % SeriesPalette.Length];
    }

    // Checks whether angle is within [start, end), handling wrap-around.
    private static bool AngleInRange(double angle, double start, double end)
    {
        // Normalise to [-π, π].
        while (angle < start) angle += 2 * Math.PI;
        while (angle > end + (2 * Math.PI)) angle -= 2 * Math.PI;
        return angle >= start && angle <= end;
    }

    // Bresenham integer line draw — single-pixel width.
    private static void DrawLine(
        RasterBuffer buffer,
        int x0, int y0, int x1, int y1,
        byte r, byte g, byte b)
    {
        var dx = Math.Abs(x1 - x0);
        var dy = Math.Abs(y1 - y0);
        var sx = x0 < x1 ? 1 : -1;
        var sy = y0 < y1 ? 1 : -1;
        var err = dx - dy;

        while (true)
        {
            buffer.BlitImagePixel(x0, y0, r, g, b);
            if (x0 == x1 && y0 == y1) break;
            var e2 = 2 * err;
            if (e2 > -dy) { err -= dy; x0 += sx; }
            if (e2 < dx) { err += dx; y0 += sy; }
        }
    }

    private static string TruncateLabel(string s, int maxChars) =>
        s.Length <= maxChars ? s : s[..maxChars];

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
