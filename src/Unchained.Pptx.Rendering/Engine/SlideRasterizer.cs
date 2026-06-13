using Unchained.Drawing;
using Unchained.Drawing.Decoders;
using Unchained.Drawing.Extensions;
using Unchained.Drawing.Text;
using Unchained.Drawing.Text.Extensions;
using Unchained.Ooxml;
using Unchained.Ooxml.Charts;
using Unchained.Ooxml.Drawing;
using Unchained.Ooxml.Media;
using Unchained.Ooxml.Text;
using Unchained.Pptx.Core;
using Unchained.Pptx.Media;
using Unchained.Pptx.Rendering.Models;
using Unchained.Pptx.Shapes;
using Unchained.Pptx.Slides;
using Buffer = HarfBuzzSharp.Buffer;

namespace Unchained.Pptx.Rendering.Engine;

/// <summary>
///     Rasterizes a single <see cref="Slide" /> into a <see cref="RasterBuffer" />
///     using FreeType2 for glyph rendering and HarfBuzz for text shaping.
/// </summary>
internal sealed class SlideRasterizer(FontCache fonts, MediaStore? media = null)
{
    // Series palette — 8 saturated colours that cycle across series in a chart.
    private static readonly (byte R, byte G, byte B)[] SeriesPalette =
    [
        (68, 114, 196), (237, 125, 49), (165, 165, 165), (255, 192, 0),
        (91, 155, 213), (112, 173, 71), (38, 68, 120), (158, 72, 14)
    ];

    internal RasterBuffer Rasterize(Slide slide, SlideSize slideSize, RenderOptions options)
    {
        var buffer = new RasterBuffer(options.WidthPx, options.HeightPx);

        // Resolve the colour scheme and font scheme for this slide (slide → layout → master).
        var colorScheme = slide.Master.Theme.Colors;
        var fontScheme = slide.Master.Theme.Fonts;

        // Scale factor: EMU → pixels
        var scaleX = (double)options.WidthPx / slideSize.Width.Value;
        var scaleY = (double)options.HeightPx / slideSize.Height.Value;

        // Paint slide background using the inheritance chain.
        PaintBackground(buffer, slide, colorScheme);

        var root = new Transform(scaleX, scaleY, 0, 0);

        // Build a lookup table of layout placeholder shapes for geometry inheritance.
        var layoutPlaceholders = BuildLayoutPlaceholderMap(slide);

        // Composite inherited backdrop shapes from the master and layout BENEATH the slide's own shapes.
        if (slide.Layout.Master is { } master)
        {
            foreach (var shape in master.Shapes.Where(static shape => !shape.IsPlaceholder))
            {
                RenderShape(buffer,
                    shape,
                    root,
                    options.Dpi,
                    colorScheme,
                    layoutPlaceholders,
                    fontScheme);
            }
        }

        if (slide.Layout is { } layout)
        {
            foreach (var shape in layout.Shapes.Where(static shape => !shape.IsPlaceholder))
            {
                RenderShape(buffer,
                    shape,
                    root,
                    options.Dpi,
                    colorScheme,
                    layoutPlaceholders,
                    fontScheme);
            }
        }

        // Render each shape in Z-order (insertion order = back-to-front).
        foreach (var shape in slide.Shapes)
        {
            RenderShape(buffer,
                shape,
                root,
                options.Dpi,
                colorScheme,
                layoutPlaceholders,
                fontScheme);
        }

        return buffer;
    }

    // Builds a map from placeholder index → Shape for the slide's layout (and master as fallback),
    // so zero-size placeholder shapes can inherit their geometry.
    private static Dictionary<int, Shape> BuildLayoutPlaceholderMap(Slide slide)
    {
        var map = new Dictionary<int, Shape>();
        foreach (var s in slide.Layout.Master.Shapes)
        {
            if (s.PlaceholderIndex.HasValue)
                map.TryAdd(s.PlaceholderIndex.Value, s);
        }

        foreach (var s in slide.Layout.Shapes)
        {
            if (s.PlaceholderIndex.HasValue)
                map[s.PlaceholderIndex.Value] = s;
        }

        return map;
    }

    // Resolves the effective background fill by walking slide → layout → master.
    private static FillFormat? ResolveBackground(Slide slide) =>
        slide.Background.Fill.Type != FillType.None
            ? slide.Background.Fill
            : slide.Layout.Background.Fill.Type != FillType.None
                ? slide.Layout.Background.Fill
                : slide.Master.Background.Fill.Type != FillType.None
                    ? slide.Master.Background.Fill
                    : null;

    private static void PaintBackground(RasterBuffer buffer, Slide slide, ColorScheme? colorScheme)
    {
        var fill = ResolveBackground(slide);

        if (fill is null)
        {
            buffer.Clear();
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
                    buffer.FillRect(0,
                        row,
                        buffer.Width,
                        1,
                        r,
                        g,
                        bv);
                }

                break;
            }
            case FillType.None:
            case FillType.Pattern:
            case FillType.Picture:
            case FillType.Group:
            default:
                buffer.Clear();
            break;
        }
    }

    private void RenderShape(
        RasterBuffer buffer,
        Shape shape,
        Transform transform,
        double dpi,
        ColorScheme? colorScheme = null,
        Dictionary<int, Shape>? layoutPlaceholders = null,
        FontScheme? fontScheme = null
    )
    {
        // Resolve geometry: if this shape is a zero-size placeholder, inherit from layout.
        if (shape.Width.Value <= 0 || shape.Height.Value <= 0)
        {
            if (shape is GroupShape groupShape)
            {
                RenderGroup(buffer,
                    groupShape,
                    transform,
                    dpi,
                    colorScheme,
                    layoutPlaceholders,
                    fontScheme);
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
                return;
        }

        var x = transform.PxX(shape.X.Value);
        var y = transform.PxY(shape.Y.Value);
        var width = transform.PxW(shape.Width.Value);
        var height = transform.PxH(shape.Height.Value);

        switch (shape)
        {
            case GroupShape group:
                RenderGroup(buffer,
                    group,
                    transform,
                    dpi,
                    colorScheme,
                    layoutPlaceholders,
                    fontScheme);
            break;

            case AutoShape autoShape when width > 0 && height > 0:
                RenderAutoShape(buffer,
                    autoShape,
                    x,
                    y,
                    width,
                    height,
                    dpi,
                    colorScheme,
                    fontScheme);
            break;

            case PictureShape pictureShape when width > 0 && height > 0:
                RenderPicture(buffer,
                    pictureShape,
                    x,
                    y,
                    width,
                    height);
            break;

            case TableShape table when width > 0 && height > 0:
                RenderTable(buffer,
                    table,
                    x,
                    y,
                    width,
                    height,
                    dpi,
                    colorScheme);
            break;

            case ConnectorShape connector:
                RenderConnector(buffer,
                    connector,
                    x,
                    y,
                    width,
                    height,
                    colorScheme);
            break;

            case ChartShape chart when width > 0 && height > 0:
                RenderChart(buffer,
                    chart,
                    x,
                    y,
                    width,
                    height,
                    dpi);
            break;

            case SmartArtShape smartArt when width > 0 && height > 0:
                RenderSmartArt(buffer,
                    smartArt,
                    x,
                    y,
                    width,
                    height,
                    dpi);
            break;
        }
    }

    private void RenderGroup(
        RasterBuffer buffer,
        GroupShape group,
        Transform parent,
        double dpi,
        ColorScheme? colorScheme = null,
        Dictionary<int, Shape>? layoutPlaceholders = null,
        FontScheme? fontScheme = null
    )
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
        {
            RenderShape(buffer,
                child,
                childTransform,
                dpi,
                colorScheme,
                layoutPlaceholders,
                fontScheme);
        }
    }

    private void RenderAutoShape(
        RasterBuffer buffer,
        AutoShape shape,
        int x,
        int y,
        int width,
        int height,
        double dpi,
        ColorScheme? colorScheme,
        FontScheme? fontScheme = null
    )
    {
        // Drop shadow — rendered before the fill so it sits underneath the shape.
        if (shape.Effects.OuterShadow is not null)
        {
            RenderDropShadow(buffer,
                shape.Effects.OuterShadow,
                x,
                y,
                width,
                height,
                dpi,
                colorScheme);
        }

        PaintFill(buffer,
            shape.Fill,
            x,
            y,
            width,
            height,
            colorScheme,
            shape.StyleFillColor);

        // 3-D bevel — edge highlights/shadows after fill, before text.
        if (shape.ThreeD is { IsEmpty: false, TopBevel: not null })
        {
            RenderBevel(buffer,
                shape.ThreeD,
                x,
                y,
                width,
                height,
                dpi);
        }

        // WordArt warp: render text to offscreen buffer then blit with curve displacement.
        if (shape.TextFrame.Format.Warp is not null && width > 0 && height > 0)
        {
            var textBuffer = new RasterBuffer(width, height);
            textBuffer.Clear(0, 0, 0); // transparent black
            RenderTextFrame(textBuffer,
                shape.TextFrame,
                0,
                0,
                width,
                height,
                dpi,
                colorScheme,
                shape.StyleTextColor,
                shape.PlaceholderType,
                fontScheme);
            BlitWarpedText(buffer,
                textBuffer,
                x,
                y,
                width,
                height,
                shape.TextFrame.Format.Warp.Preset);
        }
        else
        {
            RenderTextFrame(buffer,
                shape.TextFrame,
                x,
                y,
                width,
                height,
                dpi,
                colorScheme,
                shape.StyleTextColor,
                shape.PlaceholderType,
                fontScheme);
        }
    }

    // Blits a text buffer onto the main buffer with per-column Y displacement for WordArt warp.
    private static void BlitWarpedText(
        RasterBuffer buffer,
        RasterBuffer textBuffer,
        int x,
        int y,
        int width,
        int height,
        string preset
    )
    {
        for (var col = 0; col < width; col++)
        {
            var t = (double)col / Math.Max(1, width - 1);
            var yOffset = ComputeWarpOffset(preset, t, height);

            for (var row = 0; row < height; row++)
            {
                var (r, g, b) = textBuffer.GetPixelRgb(col, row);
                // Only blit non-white pixels (text ink).
                if (r >= 250 && g >= 250 && b >= 250) continue;

                var destRow = row + yOffset;
                buffer.BlitImagePixel(x + col, y + destRow, r, g, b);
            }
        }
    }

    // Returns vertical pixel displacement for a given horizontal position (0..1)
    // based on the WordArt preset.
    private static int ComputeWarpOffset(string preset, double t, int height)
    {
        var amplitude = height * 0.25; // max 25% of height
        return preset.ToLowerInvariant() switch
        {
            var p when p.Contains("archup") || p.Contains("arch") =>
                // Arch up: sine curve, max offset at centre.
                (int)(-amplitude * Math.Sin(Math.PI * t)),
            var p when p.Contains("archdown") =>
                (int)(amplitude * Math.Sin(Math.PI * t)),
            var p when p.Contains("wave") =>
                (int)(amplitude * 0.5 * Math.Sin(2 * Math.PI * t)),
            var p when p.Contains("circle") =>
                (int)(-amplitude * Math.Sin(Math.PI * t)),
            var p when p.Contains("chevron") =>
                t < 0.5
                    ? (int)(-amplitude * (t * 2))
                    : (int)(-amplitude * ((1 - t) * 2)),
            var p when p.Contains("inflate") =>
                (int)(-amplitude * Math.Sin(Math.PI * t) * 0.5),
            _ => 0
        };
    }

    // Renders a 3-D bevel by drawing highlight lines on top/left edges and
    // shadow lines on bottom/right edges. Width is derived from the bevel's EMU size.
    private static void RenderBevel(
        RasterBuffer buffer,
        Shape3DFormat threeD,
        int x,
        int y,
        int width,
        int height,
        double dpi
    )
    {
        var bevel = threeD.TopBevel!;
        var bevelPx = Math.Max(1, Math.Min(8, (int)(bevel.Width.ToPoints() * dpi / RenderingConstants.PointsPerInch)));

        // Highlight: top and left edges (lighter).
        for (var i = 0; i < bevelPx; i++)
        {
            var alpha = (byte)(180 - (i * 20));
            buffer.FillRect(x + i,
                y + i,
                width - (i * 2),
                1,
                255,
                255,
                255,
                alpha);
            buffer.FillRect(x + i,
                y + i,
                1,
                height - (i * 2),
                255,
                255,
                255,
                alpha);
        }

        // Shadow: bottom and right edges (darker).
        for (var i = 0; i < bevelPx; i++)
        {
            var alpha = (byte)(140 - (i * 15));
            buffer.FillRect(x + i,
                y + height - 1 - i,
                width - (i * 2),
                1,
                0,
                0,
                0,
                alpha);
            buffer.FillRect(x + width - 1 - i,
                y + i,
                1,
                height - (i * 2),
                0,
                0,
                0,
                alpha);
        }
    }

    // Renders an outer (drop) shadow by painting a series of progressively lighter offset
    // rectangles. This approximates the Gaussian blur at reasonable cost.
    private static void RenderDropShadow(
        RasterBuffer buffer,
        OuterShadowEffect shadow,
        int x,
        int y,
        int width,
        int height,
        double dpi,
        ColorScheme? colorScheme
    )
    {
        var argb = shadow.Color.Resolve(colorScheme);
        ExtractArgb(argb, out var baseA, out var sr, out var sg, out var sb);
        if (baseA == 0) return;

        // Convert EMU offsets to pixels.
        var scale = dpi / EmuConversions.EmuPerInch; // EMU → inches → px
        var dist = shadow.Distance.Value * scale;
        var angleRad = shadow.DirectionDegrees * Math.PI / 180.0;
        var offX = (int)(dist * Math.Cos(angleRad));
        var offY = (int)(dist * Math.Sin(angleRad));

        var blurPx = (int)(shadow.BlurRadius.Value * scale);
        blurPx = Math.Max(0, Math.Min(blurPx, 12));

        // Draw the shadow in layers: core (full alpha) + blur rings (fading).
        var layers = blurPx == 0 ? 1 : 3;
        for (var layer = 0; layer < layers; layer++)
        {
            var alpha = (byte)(baseA * (layers - layer) / layers);
            if (alpha == 0) continue;

            var sx = x + offX - layer;
            var sy = y + offY - layer;
            var sw = width + (layer * 2);
            var sh = height + (layer * 2);
            buffer.FillRect(sx,
                sy,
                sw,
                sh,
                sr,
                sg,
                sb,
                alpha);
        }
    }

    private void RenderTable(
        RasterBuffer buffer,
        TableShape table,
        int x,
        int y,
        int width,
        int height,
        double dpi,
        ColorScheme? colorScheme
    )
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

            PaintFill(buffer,
                cell.Fill,
                cx,
                cy,
                cw,
                ch,
                colorScheme);

            // Cell borders — use explicit border colors when set, fall back to light grey grid lines.
            DrawCellBorder(buffer,
                cell.TopBorder,
                cx,
                cy,
                cw,
                1,
                colorScheme);
            DrawCellBorder(buffer,
                cell.LeftBorder,
                cx,
                cy,
                1,
                ch,
                colorScheme);
            DrawCellBorder(buffer,
                cell.BottomBorder,
                cx,
                cy + ch - 1,
                cw,
                1,
                colorScheme);
            DrawCellBorder(buffer,
                cell.RightBorder,
                cx + cw - 1,
                cy,
                1,
                ch,
                colorScheme);

            RenderTextFrame(buffer,
                cell.TextFrame,
                cx + 2,
                cy + 2,
                cw - 4,
                ch - 4,
                dpi,
                colorScheme);
        }
    }

    // ── Connector ────────────────────────────────────────────────────────────
    private static void RenderConnector(
        RasterBuffer buffer,
        Shape shape,
        int x,
        int y,
        int width,
        int height,
        ColorScheme? colorScheme
    )
    {
        var x0 = x;
        var y0 = y;
        var x1 = x + width;
        var y1 = y + height;
        if (shape.FlipHorizontal) (x0, x1) = (x1, x0);
        if (shape.FlipVertical) (y0, y1) = (y1, y0);

        byte r = 0, g = 0, b = 0;
        if (shape.Line.Fill is { Type: FillType.Solid, Solid: not null })
            ExtractArgb(shape.Line.Fill.Solid.Color.Resolve(colorScheme), out _, out r, out g, out b);

        var thickness = Math.Max(1, (int)Math.Round((shape.Line.WidthPoints ?? 1.0) * 1.333));
        buffer.DrawLine(x0,
            y0,
            x1,
            y1,
            r,
            g,
            b,
            thickness);

        // Arrow heads — draw at endpoints if configured.
        if (shape.Line.HeadArrow.HeadType != ArrowHeadType.None)
        {
            DrawArrowHead(buffer,
                x1,
                y1,
                x0,
                y0,
                shape.Line.HeadArrow.HeadType,
                shape.Line.HeadArrow.Width,
                shape.Line.HeadArrow.Length,
                r,
                g,
                b);
        }

        if (shape.Line.TailArrow.HeadType != ArrowHeadType.None)
        {
            DrawArrowHead(buffer,
                x0,
                y0,
                x1,
                y1,
                shape.Line.TailArrow.HeadType,
                shape.Line.TailArrow.Width,
                shape.Line.TailArrow.Length,
                r,
                g,
                b);
        }
    }

    // Draws a filled arrowhead at tipX/tipY pointing FROM fromX/fromY.
    private static void DrawArrowHead(
        RasterBuffer buffer,
        int fromX,
        int fromY,
        int tipX,
        int tipY,
        ArrowHeadType headType,
        ArrowHeadSize headWidth,
        ArrowHeadSize headLength,
        byte r,
        byte g,
        byte b
    )
    {
        if (headType == ArrowHeadType.None) return;

        var size = headLength switch
        {
            ArrowHeadSize.Small => 6,
            ArrowHeadSize.Large => 14,
            _ => 10
        };
        var halfWidth = headWidth switch
        {
            ArrowHeadSize.Small => 3,
            ArrowHeadSize.Large => 7,
            _ => 5
        };

        var dx = tipX - fromX;
        var dy = tipY - fromY;
        var len = Math.Sqrt((dx * dx) + (dy * dy));
        if (len < 1) return;

        var ux = dx / len;
        var uy = dy / len;
        var px = -uy;

        // Base of the arrowhead triangle (opposite the tip).
        var bx = tipX - (int)(ux * size);
        var by = tipY - (int)(uy * size);
        var lx = bx + (int)(px * halfWidth);
        var ly = by + (int)(ux * halfWidth);
        var rx2 = bx - (int)(px * halfWidth);
        var ry2 = by - (int)(ux * halfWidth);

        DrawFilledTriangle(buffer,
            tipX,
            tipY,
            lx,
            ly,
            rx2,
            ry2,
            r,
            g,
            b);
    }

    // Fills a triangle using horizontal scan lines.
    private static void DrawFilledTriangle(
        RasterBuffer buffer,
        int x0,
        int y0,
        int x1,
        int y1,
        int x2,
        int y2,
        byte r,
        byte g,
        byte b
    )
    {
        if (y0 > y1)
        {
            (x0, x1) = (x1, x0);
            (y0, y1) = (y1, y0);
        }

        if (y0 > y2)
        {
            (x0, x2) = (x2, x0);
            (y0, y2) = (y2, y0);
        }

        if (y1 > y2)
        {
            (x1, x2) = (x2, x1);
            (y1, y2) = (y2, y1);
        }

        var totalH = y2 - y0;
        if (totalH == 0)
        {
            buffer.DrawLine(x0,
                y0,
                x2,
                y0,
                r,
                g,
                b);
            return;
        }

        for (var scanY = y0; scanY <= y2; scanY++)
        {
            var isUpperHalf = scanY < y1;
            var segH = isUpperHalf ? y1 - y0 : y2 - y1;
            var alpha = (double)(scanY - y0) / totalH;
            var beta = segH == 0 ? 1.0 : (double)(scanY - (isUpperHalf ? y0 : y1)) / segH;

            var ax = (int)(x0 + ((x2 - x0) * alpha));
            var bx2 = isUpperHalf
                ? (int)(x0 + ((x1 - x0) * beta))
                : (int)(x1 + ((x2 - x1) * beta));

            if (ax > bx2) (ax, bx2) = (bx2, ax);
            buffer.FillRect(ax,
                scanY,
                bx2 - ax + 1,
                1,
                r,
                g,
                b);
        }
    }

    // ── Chart ─────────────────────────────────────────────────────────────────
    private void RenderChart(
        RasterBuffer buffer,
        ChartShape shape,
        int x,
        int y,
        int width,
        int height,
        double dpi
    )
    {
        buffer.FillRect(x,
            y,
            width,
            height,
            255,
            255,
            255);
        DrawBorder(buffer,
            x,
            y,
            width,
            height,
            180,
            180,
            180);

        var model = shape.Chart;
        if (model.Data.Series.Count == 0) return;

        var titleH = 0;
        if (model.HasTitle && !string.IsNullOrWhiteSpace(model.Title))
        {
            RenderTextFrameText(buffer,
                model.Title,
                x + 6,
                y + 4,
                width - 12,
                14.0,
                dpi,
                60,
                60,
                60);
            titleH = (int)(18 * dpi / RenderingConstants.PointsPerInch);
        }

        // Reserve margins for axes: left for value labels, bottom for category labels.
        const int axisLeft = RenderingConstants.ChartAxisMarginLeft;
        const int axisBottom = RenderingConstants.ChartAxisMarginBottom;
        var plotX = x + axisLeft;
        var plotY = y + titleH + 6;
        var plotW = width - axisLeft - 8;
        var plotH = height - titleH - axisBottom - 8;
        if (plotW <= 4 || plotH <= 4) return;

        var series = model.Data.Series;
        var type = model.Type.ToString();
        if (type.StartsWith("Pie", StringComparison.Ordinal) || type.StartsWith("Doughnut", StringComparison.Ordinal))
        {
            RenderPieChart(buffer,
                series[0],
                plotX,
                plotY,
                plotW,
                plotH);
        }
        else if (type.StartsWith("Line", StringComparison.Ordinal) || type.StartsWith("Scatter", StringComparison.Ordinal))
        {
            var maxVal = series.SelectMany(static s => s.Values).DefaultIfEmpty(1).Max();
            var minVal = Math.Min(0.0, series.SelectMany(static s => s.Values).DefaultIfEmpty(0).Min());
            RenderChartAxes(buffer,
                plotX,
                plotY,
                plotW,
                plotH,
                minVal,
                maxVal,
                dpi,
                model.Data.Categories,
                false);
            RenderLineChart(buffer,
                series,
                plotX,
                plotY,
                plotW,
                plotH);
        }
        else
        {
            var isHorizontal = type.StartsWith("Bar", StringComparison.Ordinal);
            var maxVal = series.SelectMany(static s => s.Values).DefaultIfEmpty(1).Max();
            var minVal = Math.Min(0.0, series.SelectMany(static s => s.Values).DefaultIfEmpty(0).Min());
            RenderChartAxes(buffer,
                plotX,
                plotY,
                plotW,
                plotH,
                minVal,
                maxVal,
                dpi,
                model.Data.Categories,
                isHorizontal);
            RenderBarChart(buffer,
                series,
                plotX,
                plotY,
                plotW,
                plotH,
                isHorizontal);
        }

        // Legend
        if (model.Legend.IsVisible && series.Count > 0)
        {
            RenderChartLegend(buffer,
                series,
                x,
                y + height - 14,
                width,
                dpi);
        }
    }

    // Draws axis lines, grid lines, value tick labels, and category labels.
    private void RenderChartAxes(
        RasterBuffer buffer,
        int plotX,
        int plotY,
        int plotW,
        int plotH,
        double minVal,
        double maxVal,
        double dpi,
        IReadOnlyList<string> categories,
        bool horizontal
    )
    {
        if (Math.Abs(maxVal - minVal) < 0.0001) maxVal = minVal + 1;
        var range = maxVal - minVal;

        // Value axis: 4 evenly spaced ticks.
        const int tickCount = 4;
        for (var t = 0; t <= tickCount; t++)
        {
            var frac = (double)t / tickCount;
            var val = minVal + (range * frac);
            var label = FormatAxisValue(val);

            if (!horizontal)
            {
                var gy = plotY + plotH - (int)(frac * plotH);
                // Gridline
                buffer.FillRect(plotX,
                    gy,
                    plotW,
                    1,
                    230,
                    230,
                    230);
                // Tick label
                RenderTextFrameText(buffer,
                    label,
                    plotX - 38,
                    gy - 6,
                    36,
                    8.0,
                    dpi,
                    100,
                    100,
                    100);
            }
            else
            {
                var gx = plotX + (int)(frac * plotW);
                buffer.FillRect(gx,
                    plotY,
                    1,
                    plotH,
                    230,
                    230,
                    230);
                RenderTextFrameText(buffer,
                    label,
                    gx - 12,
                    plotY + plotH + 2,
                    36,
                    8.0,
                    dpi,
                    100,
                    100,
                    100);
            }
        }

        // Axis lines
        buffer.FillRect(plotX,
            plotY,
            1,
            plotH,
            160,
            160,
            160);
        buffer.FillRect(plotX,
            plotY + plotH,
            plotW,
            1,
            160,
            160,
            160);

        // Category labels (up to 8 to avoid crowding).
        // ReSharper disable once InvertIf
        if (categories.Count > 0)
        {
            var maxLabels = Math.Min(8, categories.Count);
            var step = Math.Max(1, categories.Count / maxLabels);
            for (var ci = 0; ci < categories.Count; ci += step)
            {
                var label = TruncateLabel(categories[ci], 8);
                if (!horizontal)
                {
                    var lx = plotX + (int)((ci + 0.5) / categories.Count * plotW) - 12;
                    RenderTextFrameText(buffer,
                        label,
                        lx,
                        plotY + plotH + 2,
                        28,
                        7.0,
                        dpi,
                        80,
                        80,
                        80);
                }
                else
                {
                    var ly = plotY + (int)((ci + 0.5) / categories.Count * plotH) - 5;
                    RenderTextFrameText(buffer,
                        label,
                        plotX - 38,
                        ly,
                        36,
                        7.0,
                        dpi,
                        80,
                        80,
                        80);
                }
            }
        }
    }

    // Draws a small legend strip at the bottom of the chart.
    private void RenderChartLegend(
        RasterBuffer buffer,
        IReadOnlyList<ChartSeries> series,
        int x,
        int y,
        int width,
        double dpi
    )
    {
        const int swatchSize = 8;
        var cursorX = x + 8;
        for (var si = 0; si < Math.Min(series.Count, 6); si++)
        {
            var color = SeriesPalette[si % SeriesPalette.Length];
            buffer.FillRect(cursorX,
                y + 3,
                swatchSize,
                swatchSize,
                color.R,
                color.G,
                color.B);
            cursorX += swatchSize + 2;
            var name = TruncateLabel(series[si].Name, 10);
            RenderTextFrameText(buffer,
                name,
                cursorX,
                y + 3,
                80,
                8.0,
                dpi,
                60,
                60,
                60);
            cursorX += 90;
            if (cursorX > x + width - 20) break;
        }
    }

    private static string FormatAxisValue(double val) =>
        Math.Abs(val) switch
        {
            >= 1_000_000 => $"{val / 1_000_000:G3}M",
            >= 1_000 => $"{val / 1_000:G3}K",
            _ => Math.Abs(val - Math.Floor(val)) < 0.05 ? ((int)val).ToString() : $"{val:G3}"
        };

    private static string TruncateLabel(string s, int maxChars) =>
        s.Length <= maxChars ? s : s[..maxChars];

    private static void RenderBarChart(
        RasterBuffer buffer,
        IReadOnlyList<ChartSeries> series,
        int x,
        int y,
        int w,
        int h,
        bool horizontal
    )
    {
        var maxVal = series.SelectMany(static s => s.Values).DefaultIfEmpty(0).Max();
        if (maxVal <= 0) maxVal = 1;
        var categories = series.Max(static s => s.Values.Count);
        if (categories == 0) return;

        const int groupGap = 4;
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
                    buffer.FillRect(x,
                        by,
                        barLen,
                        barSpan - 1,
                        color.R,
                        color.G,
                        color.B);
                }
                else
                {
                    var barLen = (int)(val / maxVal * h);
                    var bx = x + (c * groupSpan) + (s * barSpan);
                    buffer.FillRect(bx,
                        y + h - barLen,
                        barSpan - 1,
                        barLen,
                        color.R,
                        color.G,
                        color.B);
                }
            }
        }
    }

    private static void RenderLineChart(
        RasterBuffer buffer,
        IReadOnlyList<ChartSeries> series,
        int x,
        int y,
        int w,
        int h
    )
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
                buffer.DrawLine(x0,
                    y0,
                    x1,
                    y1,
                    color.R,
                    color.G,
                    color.B,
                    2);
            }
        }
    }

    private static void RenderPieChart(
        RasterBuffer buffer,
        ChartSeries series,
        int x,
        int y,
        int w,
        int h
    )
    {
        var total = series.Values.Sum();
        if (total <= 0) return;

        var cx = x + (w / 2);
        var cy = y + (h / 2);
        var radius = (Math.Min(w, h) / 2) - 2;
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
            {
                if (!(frac >= bounds[i]) || !(frac < bounds[i + 1])) continue;

                slice = i;
                break;
            }

            var color = SeriesPalette[slice % SeriesPalette.Length];
            buffer.BlitImagePixel(cx + px, cy + py, color.R, color.G, color.B);
        }
    }

    // ── SmartArt ──────────────────────────────────────────────────────────────
    // ── SmartArt ──────────────────────────────────────────────────────────────
    // Selects a layout heuristically from the node tree structure, then renders.
    private void RenderSmartArt(
        RasterBuffer buffer,
        SmartArtShape shape,
        int x,
        int y,
        int width,
        int height,
        double dpi
    )
    {
        var roots = shape.Nodes.Where(static n => !string.IsNullOrWhiteSpace(n.Text) || n.Children.Count > 0).ToList();
        if (roots.Count == 0)
        {
            DrawBorder(buffer,
                x,
                y,
                width,
                height,
                180,
                180,
                180);
            return;
        }

        var hasChildren = roots.Any(static n => n.Children.Count > 0);
        var flatTexts = FlattenSmartArt(roots).Where(static t => !string.IsNullOrWhiteSpace(t)).ToList();

        if (hasChildren)
        {
            RenderSmartArtHierarchy(buffer,
                roots,
                x,
                y,
                width,
                height,
                dpi);
        }
        else
        {
            switch (roots.Count)
            {
                case 4 when width >= height:
                    RenderSmartArtMatrix(buffer,
                        flatTexts,
                        x,
                        y,
                        width,
                        height,
                        dpi);
                break;
                case >= 3 and <= 6 when !hasChildren:
                    RenderSmartArtCycle(buffer,
                        flatTexts,
                        x,
                        y,
                        width,
                        height,
                        dpi);
                break;
                case >= 3 when height > width:
                    RenderSmartArtPyramid(buffer,
                        flatTexts,
                        x,
                        y,
                        width,
                        height,
                        dpi);
                break;
                default:
                    RenderSmartArtLinear(buffer,
                        flatTexts,
                        x,
                        y,
                        width,
                        height,
                        dpi);
                break;
            }
        }
    }

    // Linear list: stacked colored boxes top-to-bottom.
    private void RenderSmartArtLinear(
        RasterBuffer buffer,
        IReadOnlyList<string> nodes,
        int x,
        int y,
        int width,
        int height,
        double dpi
    )
    {
        var boxH = Math.Max(12, Math.Min(48, ((height - 4) / Math.Max(1, nodes.Count)) - 4));
        var cy = y + 2;
        for (var i = 0; i < nodes.Count; i++)
        {
            var color = SeriesPalette[i % SeriesPalette.Length];
            buffer.FillRect(x + 2,
                cy,
                width - 4,
                boxH,
                color.R,
                color.G,
                color.B);
            RenderTextFrameText(buffer,
                nodes[i],
                x + 8,
                cy + 2,
                width - 16,
                10.0,
                dpi,
                255,
                255,
                255);
            cy += boxH + 4;
            if (cy > y + height) break;
        }
    }

    // Cycle: nodes arranged in a circle with colored circles.
    private void RenderSmartArtCycle(
        RasterBuffer buffer,
        IReadOnlyList<string> nodes,
        int x,
        int y,
        int width,
        int height,
        double dpi
    )
    {
        var cx = x + (width / 2);
        var cy2 = y + (height / 2);
        var radius = (Math.Min(width, height) / 2) - 20;
        var nodeR = Math.Max(10, radius / 3);
        for (var i = 0; i < nodes.Count; i++)
        {
            var angle = (2 * Math.PI * i / nodes.Count) - (Math.PI / 2);
            var nx = cx + (int)(radius * Math.Cos(angle));
            var ny = cy2 + (int)(radius * Math.Sin(angle));
            var color = SeriesPalette[i % SeriesPalette.Length];
            // Draw circle by filling a square and cropping with distance check.
            for (var py = ny - nodeR; py <= ny + nodeR; py++)
            for (var px = nx - nodeR; px <= nx + nodeR; px++)
            {
                var dx = px - nx;
                var dy = py - ny;
                if ((dx * dx) + (dy * dy) <= nodeR * nodeR)
                    buffer.BlitImagePixel(px, py, color.R, color.G, color.B);
            }

            RenderTextFrameText(buffer,
                TruncateLabel(nodes[i], 8),
                nx - nodeR,
                ny - 5,
                nodeR * 2,
                8.0,
                dpi,
                255,
                255,
                255);
        }
    }

    // Hierarchy: root node at top, children in a row below.
    private void RenderSmartArtHierarchy(
        RasterBuffer buffer,
        IReadOnlyList<SmartArtNode> roots,
        int x,
        int y,
        int width,
        int height,
        double dpi
    )
    {
        var boxW = Math.Max(40, Math.Min(120, (width / Math.Max(1, roots.Count)) - 8));
        var boxH = Math.Max(20, Math.Min(40, (height / 3) - 8));
        var levelH = boxH + 16;

        var nodeSpacing = Math.Max(boxW + 8, width / Math.Max(1, roots.Count));
        for (var i = 0; i < roots.Count; i++)
            DrawNode(roots[i], x + (i * nodeSpacing) + 4, y + 4, i);

        return;

        void DrawNode(
            SmartArtNode node,
            int nx,
            int ny,
            int colorIdx
        )
        {
            var color = SeriesPalette[colorIdx % SeriesPalette.Length];
            buffer.FillRect(nx,
                ny,
                boxW,
                boxH,
                color.R,
                color.G,
                color.B);
            RenderTextFrameText(buffer,
                TruncateLabel(node.Text, 10),
                nx + 4,
                ny + 4,
                boxW - 8,
                9.0,
                dpi,
                255,
                255,
                255);

            if (node.Children.Count == 0) return;

            var childW = Math.Max(30, (width - 8) / Math.Max(1, node.Children.Count));
            var childY = ny + levelH;
            if (childY > y + height) return;

            for (var ci = 0; ci < node.Children.Count; ci++)
            {
                var childX = x + (ci * childW) + 4;
                // Connect line
                buffer.DrawLine(nx + (boxW / 2),
                    ny + boxH,
                    childX + (childW / 2),
                    childY,
                    180,
                    180,
                    180);
                DrawNode(node.Children[ci], childX, childY, colorIdx + ci + 1);
            }
        }
    }

    // Matrix: 2×2 grid of colored boxes.
    private void RenderSmartArtMatrix(
        RasterBuffer buffer,
        IReadOnlyList<string> nodes,
        int x,
        int y,
        int width,
        int height,
        double dpi
    )
    {
        var cellW = (width - 6) / 2;
        var cellH = (height - 6) / 2;
        for (var i = 0; i < Math.Min(4, nodes.Count); i++)
        {
            var col = i % 2;
            var row = i / 2;
            var cx2 = x + 2 + (col * (cellW + 2));
            var cy3 = y + 2 + (row * (cellH + 2));
            var color = SeriesPalette[i % SeriesPalette.Length];
            buffer.FillRect(cx2,
                cy3,
                cellW,
                cellH,
                color.R,
                color.G,
                color.B);
            RenderTextFrameText(buffer,
                nodes[i],
                cx2 + 4,
                cy3 + (cellH / 2) - 6,
                cellW - 8,
                10.0,
                dpi,
                255,
                255,
                255);
        }
    }

    // Pyramid: stacked trapezoids narrowing to the top.
    private void RenderSmartArtPyramid(
        RasterBuffer buffer,
        IReadOnlyList<string> nodes,
        int x,
        int y,
        int width,
        int height,
        double dpi
    )
    {
        var n = Math.Min(nodes.Count, 6);
        var rowH = height / n;
        for (var i = 0; i < n; i++)
        {
            var row = n - 1 - i; // bottom = wide, top = narrow
            var frac = (double)(row + 1) / n;
            var rowW = (int)(width * frac);
            var rx = x + ((width - rowW) / 2);
            var ry = y + (i * rowH);
            var color = SeriesPalette[i % SeriesPalette.Length];
            buffer.FillRect(rx,
                ry,
                rowW,
                rowH - 2,
                color.R,
                color.G,
                color.B);
            RenderTextFrameText(buffer,
                TruncateLabel(nodes[i], 12),
                rx + 4,
                ry + (rowH / 2) - 5,
                rowW - 8,
                9.0,
                dpi,
                255,
                255,
                255);
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
        int x,
        int y,
        int width,
        int height,
        ColorScheme? colorScheme,
        ColorSpec? styleFillColor = null
    )
    {
        switch (fill.Type)
        {
            case FillType.Solid when fill.Solid is not null:
            {
                var argb = fill.Solid.Color.Resolve(colorScheme);
                ExtractArgb(argb, out var a, out var r, out var g, out var b);
                buffer.FillRect(x,
                    y,
                    width,
                    height,
                    r,
                    g,
                    b,
                    a);
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
                    buffer.FillRect(x,
                        bandY,
                        width,
                        Math.Max(1, bandH),
                        r,
                        g,
                        bv);
                }

                break;
            }
            case FillType.Picture when fill.Picture?.Image is not null:
            {
                BlitImage(buffer,
                    fill.Picture.Image,
                    x,
                    y,
                    width,
                    height);
                break;
            }
            case FillType.None when styleFillColor.HasValue:
            {
                var argb = styleFillColor.Value.Resolve(colorScheme);
                ExtractArgb(argb, out var a, out var r, out var g, out var b);
                if (a > 0)
                {
                    buffer.FillRect(x,
                        y,
                        width,
                        height,
                        r,
                        g,
                        b,
                        a);
                }

                break;
            }
            case FillType.Pattern:
            case FillType.Group:
            default:
                // Nothing to paint: unsupported fill types, or a guarded case whose
                // condition was not met (e.g. Solid with no colour, None with no style
                // fallback, Gradient with fewer than two stops).
            break;
        }
    }

    // Decodes an EmbeddedImage and blits it into the destination rect using nearest-neighbour scaling.
    private static void BlitImage(
        RasterBuffer buffer,
        EmbeddedImage image,
        int x,
        int y,
        int width,
        int height
    )
    {
        var rawPixels = TryDecodeImageToRgb(image, out var imgWidth, out var imgHeight);
        if (rawPixels is null || imgWidth <= 0 || imgHeight <= 0)
            return;

        BlitScaledRgb(
            buffer,
            rawPixels,
            imgWidth,
            imgHeight,
            x,
            y,
            width,
            height
        );
    }

    // Nearest-neighbour blit of a packed RGB source into the destination rect.
    private static void BlitScaledRgb(
        RasterBuffer buffer,
        IReadOnlyList<byte> rawPixels,
        int imgWidth,
        int imgHeight,
        int x,
        int y,
        int width,
        int height
    )
    {
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
        int x,
        int y,
        int width,
        int height
    )
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
            buffer.FillRect(x,
                y,
                width,
                height,
                235,
                235,
                235);
            DrawBorder(buffer,
                x,
                y,
                width,
                height,
                170,
                170,
                170);
            buffer.DrawLine(x,
                y,
                x + width - 1,
                y + height - 1,
                200,
                200,
                200);
            buffer.DrawLine(x + width - 1,
                y,
                x,
                y + height - 1,
                200,
                200,
                200);
            return;
        }

        // Nearest-neighbour blit with scaling to destination rect.
        BlitScaledRgb(
            buffer,
            rawPixels,
            imgWidth,
            imgHeight,
            x,
            y,
            width,
            height
        );
    }

    // ── Text ──────────────────────────────────────────────────────────────────

    // Renders a single line of text at a fixed size/colour (used by chart titles + SmartArt nodes).
    private void RenderTextFrameText(
        RasterBuffer buffer,
        string text,
        int x,
        int y,
        int maxW,
        double sizePt,
        double dpi,
        byte r,
        byte g,
        byte b
    )
    {
        var scale = dpi / RenderingConstants.PointsPerInch;
        var lineHeight = 0;
        RenderRunText(buffer,
            text,
            TextConstants.FallbackLatinFont,
            null,
            sizePt,
            scale,
            x,
            y,
            x + maxW,
            r,
            g,
            b,
            ref lineHeight);
    }

    private void RenderTextFrame(
        RasterBuffer buffer,
        TextFrame textFrame,
        int shapeX,
        int shapeY,
        int shapeWidth,
        int shapeHeight,
        double dpi,
        ColorScheme? colorScheme = null,
        ColorSpec? styleTextColor = null,
        PlaceholderType placeholderType = PlaceholderType.None,
        FontScheme? fontScheme = null
    )
    {
        if (textFrame.Paragraphs.Count == 0)
            return;

        var scale = dpi / RenderingConstants.PointsPerInch;
        var marginLeft = (int)(textFrame.Format.MarginLeft.ToPoints() * scale / dpi * 96);
        var marginTop = (int)(textFrame.Format.MarginTop.ToPoints() * scale / dpi * 96);
        var marginBottom = (int)(textFrame.Format.MarginBottom.ToPoints() * scale / dpi * 96);

        // Multi-column layout: distribute paragraphs evenly across columns.
        var colCount = Math.Max(1, textFrame.Format.ColumnCount);
        if (colCount > 1)
        {
            var spacingPx = (int)(textFrame.Format.ColumnSpacing.ToPoints() * scale / dpi * 96);
            var colW = (shapeWidth - ((colCount - 1) * spacingPx)) / colCount;
            if (colW > 0)
            {
                // Evenly distribute paragraphs across columns.
                var parasPerCol = (int)Math.Ceiling((double)textFrame.Paragraphs.Count / colCount);
                for (var col = 0; col < colCount; col++)
                {
                    var colX = shapeX + (col * (colW + spacingPx));
                    var start = col * parasPerCol;
                    var end = Math.Min(start + parasPerCol, textFrame.Paragraphs.Count);
                    if (start >= end) break;

                    // Build a temporary TextFrame with just this column's paragraphs.
                    var colFrame = new TextFrame
                    {
                        Format =
                        {
                            VerticalAnchor = textFrame.Format.VerticalAnchor,
                            Autofit = textFrame.Format.Autofit,
                            MarginLeft = textFrame.Format.MarginLeft,
                            MarginTop = textFrame.Format.MarginTop,
                            MarginBottom = textFrame.Format.MarginBottom,
                            MarginRight = textFrame.Format.MarginRight
                        }
                    };
                    for (var pi = start; pi < end; pi++)
                        colFrame.Paragraphs.Add(textFrame.Paragraphs[pi]);

                    RenderTextFrame(buffer,
                        colFrame,
                        colX,
                        shapeY,
                        colW,
                        shapeHeight,
                        dpi,
                        colorScheme,
                        styleTextColor,
                        placeholderType,
                        fontScheme);
                }

                return;
            }
        }

        var innerLeft = shapeX + Math.Max(4, marginLeft);
        var maxX = shapeX + shapeWidth - 4;
        var maxY = shapeY + shapeHeight - 2;

        // Default font size based on placeholder type when run has no explicit size.
        var defaultFontSize = placeholderType switch
        {
            PlaceholderType.Title => 36.0,
            PlaceholderType.CenteredTitle => 36.0,
            PlaceholderType.Subtitle => 24.0,
            PlaceholderType.Body => 18.0,
            _ => TextConstants.DefaultFontSizePt
        };

        // Default text color priority: styleTextColor → theme dk1 → black.
        uint defaultTextArgb;
        if (styleTextColor.HasValue)
            defaultTextArgb = styleTextColor.Value.Resolve(colorScheme);
        else if (colorScheme is not null)
            defaultTextArgb = colorScheme.Dark1.Resolve(colorScheme);
        else
            defaultTextArgb = 0xFF000000u;
        ExtractArgb(defaultTextArgb, out _, out var defaultR, out var defaultG, out var defaultB);

        // Measure total text height for vertical anchor (Middle/Bottom).
        var anchor = textFrame.Format.VerticalAnchor;
        var startY = shapeY + Math.Max(4, marginTop);
        if (anchor is TextAnchor.Middle
            or TextAnchor.Bottom
            or TextAnchor.MiddleCentered
            or TextAnchor.BottomCentered)
        {
            var totalTextH = MeasureTotalTextHeight(
                textFrame,
                scale,
                defaultFontSize);
            var availH = shapeHeight - marginTop - marginBottom;
            var offset = availH - totalTextH;
            if (anchor is TextAnchor.Middle
                or TextAnchor.MiddleCentered)
                startY = shapeY + marginTop + (int)(offset / 2.0);
            else
                startY = shapeY + marginTop + offset;
            startY = Math.Max(shapeY + 2, startY);
        }

        // Auto-fit: if ShrinkText, find the largest scale that makes all text fit.
        var fontScale = 1.0;
        if (textFrame.Format.Autofit == TextAutofit.ShrinkText)
        {
            var availH = shapeHeight - Math.Max(4, marginTop) - Math.Max(2, marginBottom);
            if (availH > 0)
            {
                // Binary search: scale between 0.1 and 1.0
                var lo = 0.1;
                var hi = 1.0;
                for (var iter = 0; iter < 8; iter++)
                {
                    var mid = (lo + hi) / 2;
                    var h = MeasureTotalTextHeight(textFrame, scale * mid, defaultFontSize * mid);
                    if (h <= availH) lo = mid;
                    else hi = mid;
                }

                fontScale = lo;
            }
        }

        var cursorY = startY;

        foreach (var paragraph in textFrame.Paragraphs)
        {
            if (paragraph.Runs.Count == 0)
            {
                cursorY += (int)(defaultFontSize * fontScale * scale) + 2;
                if (cursorY > maxY) return;

                continue;
            }

            // Collect word tokens for word-wrap.
            var tokens = new List<(string Word, string FontName, byte[]? EmbBytes, double SizePt, byte R, byte G, byte B)>();
            foreach (var run in paragraph.Runs)
            {
                if (string.IsNullOrEmpty(run.Text)) continue;

                var fontSizePt = (run.Format.FontSizePoints ?? defaultFontSize) * fontScale;
                byte textR, textG, textB;
                if (run.Format.Fill is { Type: FillType.Solid, Solid: not null })
                {
                    var argb = run.Format.Fill.Solid.Color.Resolve(colorScheme);
                    ExtractArgb(argb, out _, out textR, out textG, out textB);
                }
                else
                {
                    textR = defaultR;
                    textG = defaultG;
                    textB = defaultB;
                }

                var fontName = ResolveFont(run.Format.LatinFont ?? SelectFontName(run), fontScheme);
                var embeddedBytes = ResolveEmbeddedFont(run, fontName, out var cacheKey);

                tokens.AddRange(SplitIntoWords(run.Text).Select(word => (word, cacheKey, embeddedBytes, fontSizePt, textR, textG, textB)));
            }

            var lineX = innerLeft;
            var lineHeight = 0;

            foreach (var (word, fontName, embBytes, sizePt, r, g, b) in tokens)
            {
                var pixelSize = (uint)Math.Max(1, Math.Round(sizePt * scale));
                var wordWidth = MeasureTextWidth(word, fontName, embBytes, pixelSize);

                if (lineX + wordWidth > maxX && lineX > innerLeft)
                {
                    cursorY += lineHeight + 2;
                    lineX = innerLeft;
                    lineHeight = 0;
                    if (cursorY > maxY) break;
                }

                var renderWord = lineX == innerLeft ? word.TrimStart() : word;
                if (string.IsNullOrEmpty(renderWord)) continue;

                var dummy = 0;
                lineX = RenderRunText(buffer,
                    renderWord,
                    fontName,
                    embBytes,
                    sizePt,
                    scale,
                    lineX,
                    cursorY,
                    maxX,
                    r,
                    g,
                    b,
                    ref dummy);
                if (dummy > lineHeight) lineHeight = dummy;
            }

            cursorY += lineHeight + 2;
            if (cursorY > maxY) return;
        }
    }

    // Resolves +mj-lt / +mn-lt theme font references to real font family names.
    private static string ResolveFont(string fontName, FontScheme? fontScheme)
    {
        if (fontScheme is null) return fontName;

        return fontName switch
        {
            OoxmlScaling.ThemeMajorLatinFont => fontScheme.MajorFont.LatinFont is { Length: > 0 } mj ? mj : fontName,
            OoxmlScaling.ThemeMinorLatinFont => fontScheme.MinorFont.LatinFont is { Length: > 0 } mn ? mn : fontName,
            _ => fontName
        };
    }

    // Estimates the total pixel height of all paragraphs in a text frame for vertical anchor.
    private static int MeasureTotalTextHeight(
        TextFrame textFrame,
        double scale,
        double defaultFontSize
    )
    {
        var total = 0;
        foreach (var para in textFrame.Paragraphs)
        {
            if (para.Runs.Count == 0)
            {
                total += (int)(defaultFontSize * scale) + 2;
                continue;
            }

            var maxSize = para.Runs.Max(r => r.Format.FontSizePoints ?? defaultFontSize);
            total += (int)(maxSize * scale) + 2;
        }

        return total;
    }

    // Splits text into words, keeping trailing spaces attached to each word.
    private static IEnumerable<string> SplitIntoWords(string text)
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
    private int MeasureTextWidth(
        string text,
        string fontName,
        byte[]? embeddedBytes,
        uint pixelSize
    )
    {
        if (string.IsNullOrEmpty(text)) return 0;

        try
        {
            var (ftFace, hbFont) = fonts.GetFonts(fontName, embeddedBytes);
            ftFace.SetPixelSize(pixelSize);
            var hbScale = (int)(pixelSize * TextShapingConstants.HarfBuzzFixed);
            hbFont.SetScale(hbScale, hbScale);

            using var hbBuffer = new Buffer();
            hbBuffer.AddUtf8(text.ToUtf8Span());
            hbBuffer.GuessSegmentProperties();
            hbFont.Shape(hbBuffer);

            return hbBuffer.GlyphPositions.Sum(static p => p.XAdvance) / TextShapingConstants.HarfBuzzFixed;
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
        int startX,
        int startY,
        int maxX,
        byte r,
        byte g,
        byte b,
        ref int lineHeight
    )
    {
        var cursorX = startX;

        // ReSharper disable once EmptyGeneralCatchClause
        try
        {
            var (ftFace, hbFont) = fonts.GetFonts(fontName, embeddedBytes);
            var pixelSize = (uint)Math.Max(1, Math.Round(fontSizePt * scale));
            ftFace.SetPixelSize(pixelSize);

            if (lineHeight < (int)pixelSize)
                lineHeight = (int)pixelSize;

            var hbScale = (int)(pixelSize * TextShapingConstants.HarfBuzzFixed);
            hbFont.SetScale(hbScale, hbScale);

            using var hbBuffer = new Buffer();
            hbBuffer.AddUtf8(text.ToUtf8Span());
            hbBuffer.GuessSegmentProperties();
            hbFont.Shape(hbBuffer);

            var glyphInfos = hbBuffer.GlyphInfos;
            var glyphPositions = hbBuffer.GlyphPositions;

            for (var i = 0; i < glyphInfos.Length; i++)
            {
                var glyphId = glyphInfos[i].Codepoint;

                if (!ftFace.TryLoadGlyph(glyphId))
                    continue;

                var penX = cursorX + (glyphPositions[i].XOffset / TextShapingConstants.HarfBuzzFixed);
                var penY = startY + (int)pixelSize + (glyphPositions[i].YOffset / TextShapingConstants.HarfBuzzFixed);

                buffer.BlitGlyphFromFace(penX,
                    penY,
                    ftFace,
                    r,
                    g,
                    b);

                cursorX += glyphPositions[i].XAdvance / TextShapingConstants.HarfBuzzFixed;

                if (cursorX >= maxX)
                    break;
            }
        }
        catch { }

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

        if (IsSvg(bytes))
        {
            // Render SVG at a reasonable fixed resolution; caller scales to dest rect.
            const int svgRenderSize = 256;
            var pixels = SvgDecoder.TryDecodeToRgb(
                bytes,
                svgRenderSize,
                svgRenderSize,
                out width,
                out height);
            return pixels;
        }

        return null;
    }

    private static bool IsPng(ReadOnlySpan<byte> bytes) =>
        bytes.Length >= 8 &&
        bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47 &&
        bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A;

    private static bool IsJpeg(ReadOnlySpan<byte> bytes) =>
        bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF;

    private static bool IsSvg(ReadOnlySpan<byte> bytes)
    {
        // SVG starts with XML declaration or <svg tag (possibly with BOM).
        if (bytes.Length < 4) return false;
        // Skip UTF-8 BOM if present.
        var start = (bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF) ? 3 : 0;
        // Look for <svg or <?xml within the first 512 bytes.
        var searchLen = Math.Min(bytes.Length - start, 512);
        var text = bytes.Slice(start, searchLen).FromUtf8Span();
        return text.Contains("<svg", StringComparison.OrdinalIgnoreCase) ||
               (text.Contains("<?xml", StringComparison.OrdinalIgnoreCase) &&
                text.Contains("svg", StringComparison.OrdinalIgnoreCase));
    }

    // ── Shared helpers ────────────────────────────────────────────────────────

    private static void DrawBorder(
        RasterBuffer buffer,
        int x,
        int y,
        int w,
        int h,
        byte r,
        byte g,
        byte b
    )
    {
        buffer.FillRect(x,
            y,
            w,
            1,
            r,
            g,
            b);
        buffer.FillRect(x,
            y + h - 1,
            w,
            1,
            r,
            g,
            b);
        buffer.FillRect(x,
            y,
            1,
            h,
            r,
            g,
            b);
        buffer.FillRect(x + w - 1,
            y,
            1,
            h,
            r,
            g,
            b);
    }

    // Draws a single cell border line. When the border has an explicit solid color, that
    // color is used. When width is explicitly 0 the border is suppressed. Otherwise falls
    // back to light grey so table structure remains visible even without explicit borders.
    private static void DrawCellBorder(
        RasterBuffer buffer,
        LineFormat border,
        int x,
        int y,
        int w,
        int h,
        ColorScheme? colorScheme
    )
    {
        // Explicit zero-width border means "no border".
        if (border.WidthPoints is <= 0)
            return;

        byte r, g, b;
        if (border.Fill is { Type: FillType.Solid, Solid: not null })
        {
            var argb = border.Fill.Solid.Color.Resolve(colorScheme);
            ExtractArgb(argb, out _, out r, out g, out b);
        }
        else
        {
            // No explicit color — use light grey fallback so table structure stays visible.
            r = 200;
            g = 200;
            b = 200;
        }

        var thickness = border.WidthPoints.HasValue
            ? Math.Max(1, (int)Math.Round(border.WidthPoints.Value * 1.333))
            : 1;
        buffer.FillRect(x,
            y,
            w,
            thickness,
            r,
            g,
            b);
        if (h > thickness)
        {
            buffer.FillRect(x,
                y,
                thickness,
                h,
                r,
                g,
                b);
        }
    }

    private static string SelectFontName(Run run)
    {
        if (run.Format.Bold == InheritableBool.True)
            return TextConstants.FallbackLatinFontBold;

        return TextConstants.FallbackLatinFont;
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

    private static void ExtractArgb(
        uint argb,
        out byte a,
        out byte r,
        out byte g,
        out byte b
    ) => (a, r, g, b) = ColorMath.UnpackArgb(argb);

    // Maps a coordinate-space EMU point to device pixels: px = (Scale * emu) + Offset.
    // The slide root uses Scale = px/EMU, Offset = 0; each group composes a child transform
    // onto its parent so nested shapes land in the right place.
    private readonly record struct Transform(double ScaleX,
        double ScaleY,
        double OffsetX,
        double OffsetY
    )
    {
        public int PxX(long emu) => (int)((ScaleX * emu) + OffsetX);
        public int PxY(long emu) => (int)((ScaleY * emu) + OffsetY);
        public int PxW(long emu) => (int)(ScaleX * emu);
        public int PxH(long emu) => (int)(ScaleY * emu);
    }
}
