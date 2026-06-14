using Unchained.Drawing;
using Unchained.Drawing.Primitives;
using Unchained.Pdf.Models;

namespace Unchained.Pdf.Rendering.Rendering;

// Axial/radial/mesh shadings (the `sh` operator and shading-pattern fills) and tiling
// patterns. The state-free gradient mathematics live in <see cref="ShadingMath" />.
internal sealed partial class PageRenderer
{
    // Paints a shading clipped to the current path's device bounding box (used for a
    // shading-pattern fill, where the path defines the painted region).
    private void PaintShadingInPathBounds(ShadingInfo sh)
    {
        var (minX, minY, maxX, maxY) = PathDeviceBounds();
        if (maxX < minX) return;

        PaintShadingRect(sh, (int)Math.Floor(minX), (int)Math.Floor(minY), (int)Math.Ceiling(maxX), (int)Math.Ceiling(maxY));
    }

    private void PaintShadingInClip(ShadingInfo sh)
    {
        var (x0, y0, x1, y1) = buffer.ClipBounds();
        PaintShadingRect(sh, x0, y0, x1 - 1, y1 - 1);
    }

    // Core gradient rasteriser: for each device pixel in [dx0..dx1]×[dy0..dy1], map back to
    // user space (inverse CTM), compute the shading's parametric t, and write the ramp colour.
    private void PaintShadingRect(
        ShadingInfo sh,
        int dx0,
        int dy0,
        int dx1,
        int dy1
    )
    {
        // Mesh shadings are painted as Gouraud-interpolated triangles, ignoring the rect.
        if (sh.IsMesh)
        {
            PaintMesh(sh);
            return;
        }

        dx0 = Math.Max(0, dx0);
        dy0 = Math.Max(0, dy0);
        dx1 = Math.Min(buffer.Width - 1, dx1);
        dy1 = Math.Min(buffer.Height - 1, dy1);
        if (dx1 < dx0 || dy1 < dy0) return;

        // Honour an active clip rectangle.
        {
            var (cx0, cy0, cx1, cy1) = buffer.ClipBounds();
            dx0 = Math.Max(dx0, cx0);
            dy0 = Math.Max(dy0, cy0);
            dx1 = Math.Min(dx1, cx1 - 1);
            dy1 = Math.Min(dy1, cy1 - 1);
            if (dx1 < dx0 || dy1 < dy0) return;
        }

        // Invert the device→user mapping. Device px = ux*scale, py = (H - uy)*scale, where
        // (ux,uy) = CTM·(x,y). Compose M = CTM then the device flip; invert the whole thing.
        if (!ShadingMath.TryInvertDeviceToUser(_gs.Ctm, scale, pageHeightPt, out var inv)) return;

        for (var py = dy0; py <= dy1; py++)
        for (var px = dx0; px <= dx1; px++)
        {
            var (ux, uy) = ShadingMath.ApplyInv(inv, px + 0.5, py + 0.5);
            if (!ShadingMath.ShadingT(sh, ux, uy, out var t)) continue;

            var (r, g, b) = sh.ColorAt(t);
            if (_gs.FillA >= 255) buffer.BlitImagePixel(px, py, r, g, b);
            else
            {
                buffer.BlendPixel(
                    px,
                    py,
                    r,
                    g,
                    b,
                    _gs.FillA,
                    _gs.BlendMode
                );
            }
        }
    }

    // Rasterises a mesh shading's triangles with barycentric Gouraud colour interpolation.
    // Vertices are in user space; each is mapped to device space via UToPixel.
    private void PaintMesh(ShadingInfo sh)
    {
        if (sh.Triangles is null) return;

        foreach (var t in sh.Triangles)
        {
            var (ax, ay) = UToPixel(t.X0, t.Y0);
            var (bx, by) = UToPixel(t.X1, t.Y1);
            var (cx, cy) = UToPixel(t.X2, t.Y2);

            var minX = (int)Math.Floor(Math.Min(ax, Math.Min(bx, cx)));
            var maxX = (int)Math.Ceiling(Math.Max(ax, Math.Max(bx, cx)));
            var minY = (int)Math.Floor(Math.Min(ay, Math.Min(by, cy)));
            var maxY = (int)Math.Ceiling(Math.Max(ay, Math.Max(by, cy)));
            minX = Math.Max(minX, 0);
            minY = Math.Max(minY, 0);
            maxX = Math.Min(maxX, buffer.Width - 1);
            maxY = Math.Min(maxY, buffer.Height - 1);
            {
                var (cx0, cy0, cx1, cy1) = buffer.ClipBounds();
                minX = Math.Max(minX, cx0);
                minY = Math.Max(minY, cy0);
                maxX = Math.Min(maxX, cx1 - 1);
                maxY = Math.Min(maxY, cy1 - 1);
            }

            var denom = ((by - cy) * (ax - cx)) + ((cx - bx) * (ay - cy));
            if (Math.Abs(denom) < RenderingConstants.DeterminantEpsilon) continue; // degenerate triangle

            for (var py = minY; py <= maxY; py++)
            for (var px = minX; px <= maxX; px++)
            {
                var fx = px + 0.5;
                var fy = py + 0.5;
                var w0 = (((by - cy) * (fx - cx)) + ((cx - bx) * (fy - cy))) / denom;
                var w1 = (((cy - ay) * (fx - cx)) + ((ax - cx) * (fy - cy))) / denom;
                var w2 = 1 - w0 - w1;
                if (w0 < -0.0001 || w1 < -0.0001 || w2 < -0.0001) continue; // outside triangle

                var r = (byte)Math.Clamp((w0 * t.R0) + (w1 * t.R1) + (w2 * t.R2), 0, 255);
                var g = (byte)Math.Clamp((w0 * t.G0) + (w1 * t.G1) + (w2 * t.G2), 0, 255);
                var b = (byte)Math.Clamp((w0 * t.B0) + (w1 * t.B1) + (w2 * t.B2), 0, 255);
                if (_gs.FillA >= 255) buffer.BlitImagePixel(px, py, r, g, b);
                else
                {
                    buffer.BlendPixel(
                        px,
                        py,
                        r,
                        g,
                        b,
                        _gs.FillA
                    );
                }
            }
        }
    }

    // Tiles a pattern cell across the current path's device bounding box. Renders one cell
    // to a small buffer (recursively via a child PageRenderer), then blits it on the lattice
    // defined by XStep/YStep under the pattern matrix. Clipped to the path bbox + active clip.
    private void PaintTilingInPathBounds(TilingPatternInfo tp)
    {
        if (_tilingDepth >= 2) return; // guard against pattern-in-pattern recursion

        var (minX, minY, maxX, maxY) = PathDeviceBounds();
        if (maxX < minX) return;

        // Pattern cell size in device pixels (pattern matrix scale × device scale).
        var pm = tp.Matrix;
        var sxv = Vector2D.Magnitude(pm[0], pm[1]);
        var syv = Vector2D.Magnitude(pm[2], pm[3]);
        var stepXpx = Math.Abs(tp.XStep) * sxv * scale;
        var stepYpx = Math.Abs(tp.YStep) * syv * scale;
        if (stepXpx < 0.5 || stepYpx < 0.5) return;

        // Cap tile pixel size and total tile count to keep this bounded.
        var tileW = Math.Clamp((int)Math.Ceiling(stepXpx), 1, 256);
        var tileH = Math.Clamp((int)Math.Ceiling(stepYpx), 1, 256);

        // Render one cell into its own buffer. The cell content draws in pattern space with
        // BBox origin; we scale pattern→tile pixels and flip Y to match the cell content.
        var tile = new RasterBuffer(tileW, tileH);
        tile.Clear();
        var cellScaleX = tileW / (tp.XStep == 0 ? 1 : Math.Abs(tp.XStep));
        var cellScaleY = tileH / (tp.YStep == 0 ? 1 : Math.Abs(tp.YStep));
        var cellScale = Math.Min(cellScaleX, cellScaleY);
        // Initial CTM translates the BBox lower-left to the tile origin.
        double[] cellCtm = [1, 0, 0, 1, -tp.BBox[0], -tp.BBox[1]];
        var cell = new PageRenderer(
                tile,
                fonts,
                cellScale,
                tp.YStep == 0 ? tileH / cellScale : Math.Abs(tp.YStep),
                embeddedFontBytes,
                imageXObjects,
                cellCtm,
                toUnicodeMaps,
                compositeFonts,
                extGStateAlphas,
                shadings,
                tilingPatterns,
                null,
                colorSpaces,
                type3Fonts
            )
            { _tilingDepth = _tilingDepth + 1 };
        // Uncoloured (PaintType 2) cells use the current fill colour.
        if (tp.PaintType == 2)
            cell.SetInitialFillColor(_gs.FillR, _gs.FillG, _gs.FillB);
        cell.Render(tp.Operators, EmptyFontMap);

        // Build a mask of which tile pixels are "ink" (non-white) to avoid painting the white
        // background over existing content.
        var tileData = tile.ToArgbBytes();

        var x0 = Math.Max(0, (int)Math.Floor(minX));
        var y0 = Math.Max(0, (int)Math.Floor(minY));
        var x1 = Math.Min(buffer.Width - 1, (int)Math.Ceiling(maxX));
        var y1 = Math.Min(buffer.Height - 1, (int)Math.Ceiling(maxY));
        {
            var (cx0, cy0, cx1, cy1) = buffer.ClipBounds();
            x0 = Math.Max(x0, cx0);
            y0 = Math.Max(y0, cy0);
            x1 = Math.Min(x1, cx1 - 1);
            y1 = Math.Min(y1, cy1 - 1);
        }

        for (var py = y0; py <= y1; py++)
        for (var px = x0; px <= x1; px++)
        {
            var tx = (((px - x0) % tileW) + tileW) % tileW;
            var ty = (((py - y0) % tileH) + tileH) % tileH;
            var o = ((ty * tileW) + tx) * 4;
            var r = tileData[o];
            var g = tileData[o + 1];
            var b = tileData[o + 2];
            if (r >= 250 && g >= 250 && b >= 250) continue; // skip the cell's white background

            buffer.BlitImagePixel(px, py, r, g, b);
        }
    }
}
