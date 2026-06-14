using Unchained.Drawing;
using Unchained.Ooxml;
using Unchained.Ooxml.Drawing;
using Unchained.Ooxml.Media;
using Unchained.Pptx.Rendering.Engine.Rasterizers;
using Unchained.Pptx.Shapes;

namespace Unchained.Pptx.Rendering.Engine;

// Visual effects and non-text shape bodies: WordArt warp, 3-D bevel, drop shadow, fills
// (solid/gradient/picture), tables and pictures.
internal sealed partial class SlideRasterizer
{
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
        var rawPixels = SlideImageDecoder.TryDecodeImageToRgb(image, out var imgWidth, out var imgHeight);
        if (rawPixels is null || imgWidth <= 0 || imgHeight <= 0)
            return;

        SlideImageDecoder.BlitScaledRgb(
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

        var rawPixels = SlideImageDecoder.TryDecodeImageToRgb(shape.Image, out var imgWidth, out var imgHeight);
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
        SlideImageDecoder.BlitScaledRgb(
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
}
