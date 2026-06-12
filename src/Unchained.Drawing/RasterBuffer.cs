using System.Diagnostics.CodeAnalysis;

namespace Unchained.Drawing;

/// <summary>
///     ARGB (4 bytes per pixel, row-major) pixel buffer used as the render target.
///     The byte layout per pixel is: [R, G, B, A] at indices [4*i, 4*i+1, 4*i+2, 4*i+3].
/// </summary>
internal sealed class RasterBuffer(int width, int height)
{
    // Glyph coverage adjustment. FreeType emits linear coverage; reference rasterizers
    // (incl. Pdfium) lighten partial-coverage edge pixels, so a purely linear blit makes
    // Unchained text edges too heavy/dark. Applying gamma 2.0 to the coverage softens
    // edges to match; 2.0 minimises the true mean-absolute pixel error against Pdfium
    // (higher values keep gaming a thresholded metric while actually thinning text).
    // Full coverage (255) and zero coverage (0) are unchanged.
    private static readonly byte[] CoverageGamma = BuildCoverageGamma(2.0);
    private readonly byte[] _data = new byte[width * height * 4];

    // Per-pixel clip mask (Width × Height bytes). 0 = clipped out, non-zero = inside clip.
    // Null means no clip (every pixel is writable). Replaced entirely on each W/W* path clip;
    // intersected (AND) when clip paths are nested. Saved/restored with the graphics state.
    private byte[]? _clipMask;

    internal int Width { get; } = width;
    internal int Height { get; } = height;

    /// <summary>Sets the clip to the axis-aligned rectangle [x0,x1) × [y0,y1).</summary>
    internal void SetClipRect(
        int x0,
        int y0,
        int x1,
        int y1
    )
    {
        // Clamp to buffer bounds.
        x0 = Math.Max(0, x0);
        y0 = Math.Max(0, y0);
        x1 = Math.Min(Width, x1);
        y1 = Math.Min(Height, y1);
        _clipMask = new byte[Width * Height];
        for (var py = y0; py < y1; py++)
        for (var px = x0; px < x1; px++)
            _clipMask[(py * Width) + px] = 255;
    }

    /// <summary>
    ///     Rasterises a set of polygons into the clip mask using the given winding rule,
    ///     then ANDs the result with any existing mask (intersection of nested clips).
    /// </summary>
    internal void SetClipPolygons(
        IReadOnlyList<(double X, double Y)[]> polys,
        bool evenOdd
    )
    {
        var newMask = RasterisePolygons(polys, evenOdd);
        if (_clipMask is null)
            _clipMask = newMask;
        else
        {
            // Intersect: a pixel is inside only if it was inside both the previous and new clip.
            for (var i = 0; i < _clipMask.Length; i++)
                _clipMask[i] = (byte)(_clipMask[i] & newMask[i]);
        }
    }

    internal void ClearClip() => _clipMask = null;

    /// <summary>Snapshots the current clip mask so it can be restored on Q.</summary>
    internal byte[]? SaveClipMask() => (byte[]?)_clipMask?.Clone();

    /// <summary>Restores a previously snapshotted clip mask (from SaveClipMask).</summary>
    internal void RestoreClipMask(byte[]? saved) => _clipMask = saved;

    private bool InClip(int x, int y) => _clipMask is null || _clipMask[(y * Width) + x] != 0;

    // Scanline-rasterises a set of polygons into a fresh Width×Height mask.
    // Each polygon is treated as implicitly closed. Same algorithm as FillPolygon in
    // PageRenderer — samples at pixel centre (y+0.5), half-open edge rule.
    private byte[] RasterisePolygons(
        IReadOnlyCollection<(double X, double Y)[]> polys,
        bool evenOdd
    )
    {
        var mask = new byte[Width * Height];
        if (polys.Count == 0) return mask;

        var minY = double.MaxValue;
        var maxY = double.MinValue;
        foreach (var pts in polys)
        foreach (var (_, py) in pts)
        {
            if (py < minY) minY = py;
            if (py > maxY) maxY = py;
        }

        var y0 = Math.Max(0, (int)Math.Floor(minY));
        var y1 = Math.Min(Height - 1, (int)Math.Ceiling(maxY));

        var xs = new List<(double X, int Dir)>();
        for (var y = y0; y <= y1; y++)
        {
            var sy = y + 0.5;
            xs.Clear();
            foreach (var pts in polys)
            {
                var n = pts.Length;
                for (var i = 0; i < n; i++)
                {
                    var (ax, ay) = pts[i];
                    var (bx, by) = pts[(i + 1) % n];
                    if (Math.Abs(ay - by) < 0.05)
                        continue;

                    if (!(sy >= Math.Min(ay, by)) || !(sy < Math.Max(ay, by)))
                        continue;

                    var t = (sy - ay) / (by - ay);
                    xs.Add((ax + (t * (bx - ax)), by > ay ? 1 : -1));
                }
            }

            if (xs.Count < 2)
                continue;

            xs.Sort(static (p, q) => p.X.CompareTo(q.X));

            var wind = 0;
            for (var i = 0; i < xs.Count - 1; i++)
            {
                wind += xs[i].Dir;
                var inside = evenOdd ? ((i + 1) & 1) == 1 : wind != 0;
                if (!inside)
                    continue;

                var xStart = Math.Max(0, (int)Math.Round(xs[i].X));
                var xEnd = Math.Min(Width - 1, (int)Math.Round(xs[i + 1].X) - 1);
                for (var px = xStart; px <= xEnd; px++)
                    mask[(y * Width) + px] = 255;
            }
        }

        return mask;
    }

    internal void Clear(byte r = 255, byte g = 255, byte b = 255)
    {
        for (var i = 0; i < _data.Length; i += 4)
        {
            _data[i] = r;
            _data[i + 1] = g;
            _data[i + 2] = b;
            _data[i + 3] = 255;
        }
    }

    [SuppressMessage("ReSharper", "BadListLineBreaks")]
    internal void SetPixel(
        int x,
        int y,
        byte r,
        byte g,
        byte b,
        byte a,
        string blendMode = "Normal"
    )
    {
        if ((uint)x >= (uint)Width || (uint)y >= (uint)Height)
            return;
        if (!InClip(x, y))
            return;

        var i = ((y * Width) + x) * 4;
        if (blendMode is "Normal" or "Compatible")
        {
            if (a == 255)
            {
                _data[i] = r;
                _data[i + 1] = g;
                _data[i + 2] = b;
            }
            else
            {
                var inv = 255 - a;
                _data[i] = (byte)(((_data[i] * inv) + (r * a)) / 255);
                _data[i + 1] = (byte)(((_data[i + 1] * inv) + (g * a)) / 255);
                _data[i + 2] = (byte)(((_data[i + 2] * inv) + (b * a)) / 255);
            }
        }
        else
        {
            // Apply the separable or non-separable blend mode to the backdrop, then
            // composite the result over the backdrop using the source alpha.
            var br = _data[i];
            var bg = _data[i + 1];
            var bb = _data[i + 2];
            Blend(blendMode,
                br,
                bg,
                bb,
                r,
                g,
                b,
                a,
                out var or,
                out var og,
                out var ob);
            _data[i] = or;
            _data[i + 1] = og;
            _data[i + 2] = ob;
        }

        _data[i + 3] = 255;
    }

    // Applies a PDF blend mode (ISO 32000-1 §11.3.5) to a source colour over a backdrop.
    // For separable modes: result = Normal-composite(BlendFn(Cb, Cs), Cs, alpha).
    // For non-separable modes: operates on all three channels simultaneously.
    private static void Blend(
        string mode,
        byte br,
        byte bg,
        byte bb, // backdrop
        byte sr,
        byte sg,
        byte sb, // source
        byte a,  // source alpha
        out byte or,
        out byte og,
        out byte ob
    )
    {
        switch (mode)
        {
            case "Multiply":
            {
                var mr = (byte)(br * sr / 255);
                var mg = (byte)(bg * sg / 255);
                var mb = (byte)(bb * sb / 255);
                AlphaComposite(br,
                    bg,
                    bb,
                    mr,
                    mg,
                    mb,
                    a,
                    out or,
                    out og,
                    out ob);
                break;
            }
            case "Screen":
            {
                var mr = (byte)(br + sr - (br * sr / 255));
                var mg = (byte)(bg + sg - (bg * sg / 255));
                var mb = (byte)(bb + sb - (bb * sb / 255));
                AlphaComposite(br,
                    bg,
                    bb,
                    mr,
                    mg,
                    mb,
                    a,
                    out or,
                    out og,
                    out ob);
                break;
            }
            case "Overlay":
            {
                var mr = HardLightChannel(sr, br);
                var mg = HardLightChannel(sg, bg);
                var mb = HardLightChannel(sb, bb);
                AlphaComposite(br,
                    bg,
                    bb,
                    mr,
                    mg,
                    mb,
                    a,
                    out or,
                    out og,
                    out ob);
                break;
            }
            case "Darken":
            {
                var mr = Math.Min(br, sr);
                var mg = Math.Min(bg, sg);
                var mb = Math.Min(bb, sb);
                AlphaComposite(br,
                    bg,
                    bb,
                    mr,
                    mg,
                    mb,
                    a,
                    out or,
                    out og,
                    out ob);
                break;
            }
            case "Lighten":
            {
                var mr = Math.Max(br, sr);
                var mg = Math.Max(bg, sg);
                var mb = Math.Max(bb, sb);
                AlphaComposite(br,
                    bg,
                    bb,
                    mr,
                    mg,
                    mb,
                    a,
                    out or,
                    out og,
                    out ob);
                break;
            }
            case "ColorDodge":
            {
                var mr = ColorDodgeChannel(br, sr);
                var mg = ColorDodgeChannel(bg, sg);
                var mb = ColorDodgeChannel(bb, sb);
                AlphaComposite(br,
                    bg,
                    bb,
                    mr,
                    mg,
                    mb,
                    a,
                    out or,
                    out og,
                    out ob);
                break;
            }
            case "ColorBurn":
            {
                var mr = ColorBurnChannel(br, sr);
                var mg = ColorBurnChannel(bg, sg);
                var mb = ColorBurnChannel(bb, sb);
                AlphaComposite(br,
                    bg,
                    bb,
                    mr,
                    mg,
                    mb,
                    a,
                    out or,
                    out og,
                    out ob);
                break;
            }
            case "HardLight":
            {
                var mr = HardLightChannel(br, sr);
                var mg = HardLightChannel(bg, sg);
                var mb = HardLightChannel(bb, sb);
                AlphaComposite(br,
                    bg,
                    bb,
                    mr,
                    mg,
                    mb,
                    a,
                    out or,
                    out og,
                    out ob);
                break;
            }
            case "SoftLight":
            {
                var mr = SoftLightChannel(br, sr);
                var mg = SoftLightChannel(bg, sg);
                var mb = SoftLightChannel(bb, sb);
                AlphaComposite(br,
                    bg,
                    bb,
                    mr,
                    mg,
                    mb,
                    a,
                    out or,
                    out og,
                    out ob);
                break;
            }
            case "Difference":
            {
                var mr = (byte)Math.Abs(br - sr);
                var mg = (byte)Math.Abs(bg - sg);
                var mb = (byte)Math.Abs(bb - sb);
                AlphaComposite(br,
                    bg,
                    bb,
                    mr,
                    mg,
                    mb,
                    a,
                    out or,
                    out og,
                    out ob);
                break;
            }
            case "Exclusion":
            {
                var mr = (byte)(br + sr - (2 * br * sr / 255));
                var mg = (byte)(bg + sg - (2 * bg * sg / 255));
                var mb = (byte)(bb + sb - (2 * bb * sb / 255));
                AlphaComposite(br,
                    bg,
                    bb,
                    mr,
                    mg,
                    mb,
                    a,
                    out or,
                    out og,
                    out ob);
                break;
            }
            case "Hue":
            {
                RgbToHsl(br,
                    bg,
                    bb,
                    out _,
                    out var sl,
                    out var ll);
                RgbToHsl(sr,
                    sg,
                    sb,
                    out var hs,
                    out _,
                    out _);
                HslToRgb(hs,
                    sl,
                    ll,
                    out var mr,
                    out var mg,
                    out var mb);
                AlphaComposite(br,
                    bg,
                    bb,
                    mr,
                    mg,
                    mb,
                    a,
                    out or,
                    out og,
                    out ob);
                break;
            }
            case "Saturation":
            {
                RgbToHsl(br,
                    bg,
                    bb,
                    out var hb,
                    out _,
                    out var lb);
                RgbToHsl(sr,
                    sg,
                    sb,
                    out _,
                    out var ss,
                    out _);
                HslToRgb(hb,
                    ss,
                    lb,
                    out var mr,
                    out var mg,
                    out var mb);
                AlphaComposite(br,
                    bg,
                    bb,
                    mr,
                    mg,
                    mb,
                    a,
                    out or,
                    out og,
                    out ob);
                break;
            }
            case "Color":
            {
                RgbToHsl(br,
                    bg,
                    bb,
                    out _,
                    out _,
                    out var lb);
                RgbToHsl(sr,
                    sg,
                    sb,
                    out var hs,
                    out var ss,
                    out _);
                HslToRgb(hs,
                    ss,
                    lb,
                    out var mr,
                    out var mg,
                    out var mb);
                AlphaComposite(br,
                    bg,
                    bb,
                    mr,
                    mg,
                    mb,
                    a,
                    out or,
                    out og,
                    out ob);
                break;
            }
            case "Luminosity":
            {
                RgbToHsl(br,
                    bg,
                    bb,
                    out var hb,
                    out var sb2,
                    out _);
                RgbToHsl(sr,
                    sg,
                    sb,
                    out _,
                    out _,
                    out var ls);
                HslToRgb(hb,
                    sb2,
                    ls,
                    out var mr,
                    out var mg,
                    out var mb);
                AlphaComposite(br,
                    bg,
                    bb,
                    mr,
                    mg,
                    mb,
                    a,
                    out or,
                    out og,
                    out ob);
                break;
            }
            default:
            {
                // Unknown mode: fall back to Normal compositing.
                var inv = 255 - a;
                or = (byte)(((br * inv) + (sr * a)) / 255);
                og = (byte)(((bg * inv) + (sg * a)) / 255);
                ob = (byte)(((bb * inv) + (sb * a)) / 255);
                break;
            }
        }
    }

    // Alpha-composites a blended colour (mr,mg,mb) over the backdrop (br,bg,bb) using the
    // source alpha: result = backdrop + alpha * (blended - backdrop).
    private static void AlphaComposite(
        byte br,
        byte bg,
        byte bb,
        byte mr,
        byte mg,
        byte mb,
        byte a,
        out byte or,
        out byte og,
        out byte ob
    )
    {
        var inv = 255 - a;
        or = (byte)(((br * inv) + (mr * a)) / 255);
        og = (byte)(((bg * inv) + (mg * a)) / 255);
        ob = (byte)(((bb * inv) + (mb * a)) / 255);
    }

    private static byte HardLightChannel(byte cb, byte cs) =>
        cs < 128
            ? (byte)(2 * cb * cs / 255)
            : (byte)(255 - (2 * (255 - cb) * (255 - cs) / 255));

    private static byte ColorDodgeChannel(byte cb, byte cs) =>
        cs == 255 ? (byte)255 : (byte)Math.Min(255, cb * 255 / (255 - cs));

    private static byte ColorBurnChannel(byte cb, byte cs) =>
        cs == 0 ? (byte)0 : (byte)Math.Max(0, 255 - ((255 - cb) * 255 / cs));

    private static byte SoftLightChannel(byte cb, byte cs)
    {
        var b = cb / 255.0;
        var s = cs / 255.0;
        double result;
        if (s <= 0.5)
            result = b - ((1 - (2 * s)) * b * (1 - b));
        else
        {
            var d = b <= 0.25
                ? ((((16 * b) - 12) * b) + 4) * b
                : Math.Sqrt(b);
            result = b + (((2 * s) - 1) * (d - b));
        }

        return (byte)Math.Clamp((int)Math.Round(result * 255), 0, 255);
    }

    // Converts 0-255 RGB to HSL (H in [0,1), S in [0,1], L in [0,1]).
    private static void RgbToHsl(
        byte r,
        byte g,
        byte b,
        out double h,
        out double s,
        out double l
    )
    {
        var rf = r / 255.0;
        var gf = g / 255.0;
        var bf = b / 255.0;
        var max = Math.Max(rf, Math.Max(gf, bf));
        var min = Math.Min(rf, Math.Min(gf, bf));
        l = (max + min) / 2.0;
        var delta = max - min;
        if (delta < 1e-10)
        {
            h = 0;
            s = 0;
            return;
        }

        s = l > 0.5 ? delta / (2 - max - min) : delta / (max + min);
        if (Math.Abs(max - rf) < 0.05) h = (((gf - bf) / delta) + (gf < bf ? 6 : 0)) / 6.0;
        else if (Math.Abs(max - gf) < 0.05) h = (((bf - rf) / delta) + 2) / 6.0;
        else h = (((rf - gf) / delta) + 4) / 6.0;
    }

    // Converts HSL (H in [0,1), S in [0,1], L in [0,1]) to 0-255 RGB.
    private static void HslToRgb(
        double h,
        double s,
        double l,
        out byte r,
        out byte g,
        out byte b
    )
    {
        if (s < 1e-10)
        {
            var v = (byte)Math.Clamp((int)Math.Round(l * 255), 0, 255);
            r = g = b = v;
            return;
        }

        var q = l < 0.5 ? l * (1 + s) : l + s - (l * s);
        var p = (2 * l) - q;
        r = (byte)Math.Clamp((int)Math.Round(HueToRgb(p, q, h + (1.0 / 3)) * 255), 0, 255);
        g = (byte)Math.Clamp((int)Math.Round(HueToRgb(p, q, h) * 255), 0, 255);
        b = (byte)Math.Clamp((int)Math.Round(HueToRgb(p, q, h - (1.0 / 3)) * 255), 0, 255);
    }

    private static double HueToRgb(double p, double q, double t)
    {
        if (t < 0) t += 1;
        if (t > 1) t -= 1;
        return t switch
        {
            < 1.0 / 6 => p + ((q - p) * 6 * t),
            < 1.0 / 2 => q,
            < 2.0 / 3 => p + ((q - p) * ((2.0 / 3) - t) * 6),
            _ => p
        };
    }

    [SuppressMessage("ReSharper", "BadListLineBreaks")]
    internal void FillRect(
        int x,
        int y,
        int w,
        int h,
        byte r,
        byte g,
        byte b,
        byte a = 255,
        string blendMode = "Normal"
    )
    {
        var x1 = Math.Max(0, x);
        var y1 = Math.Max(0, y);
        var x2 = Math.Min(Width, x + w);
        var y2 = Math.Min(Height, y + h);
        for (var py = y1; py < y2; py++)
        for (var px = x1; px < x2; px++)
        {
            SetPixel(px,
                py,
                r,
                g,
                b,
                a,
                blendMode);
        }
    }

    // Fills the inclusive horizontal span [x0, x1] on row y with an opaque colour.
    // Used by the scanline polygon rasteriser; clipped to the buffer bounds.
    internal void FillSpan(
        int y,
        int x0,
        int x1,
        byte r,
        byte g,
        byte b,
        byte a = 255,
        string blendMode = "Normal"
    )
    {
        if ((uint)y >= (uint)Height)
            return;

        var xa = Math.Max(0, x0);
        var xb = Math.Min(Width - 1, x1);
        for (var px = xa; px <= xb; px++)
        {
            SetPixel(px,
                y,
                r,
                g,
                b,
                a,
                blendMode);
        }
    }

    // Bresenham line with configurable thickness.
    [SuppressMessage("ReSharper", "BadListLineBreaks")]
    internal void DrawLine(
        int x0,
        int y0,
        int x1,
        int y1,
        byte r,
        byte g,
        byte b,
        int thicknessPx = 1,
        byte a = 255,
        string blendMode = "Normal"
    )
    {
        var dx = Math.Abs(x1 - x0);
        var dy = Math.Abs(y1 - y0);
        var sx = x0 < x1 ? 1 : -1;
        var sy = y0 < y1 ? 1 : -1;
        var err = dx - dy;
        var half = thicknessPx / 2;

        while (true)
        {
            for (var ty = -half; ty <= half; ty++)
            for (var tx = -half; tx <= half; tx++)
            {
                SetPixel(x0 + tx,
                    y0 + ty,
                    r,
                    g,
                    b,
                    a,
                    blendMode);
            }

            if (x0 == x1 && y0 == y1)
                break;

            var e2 = 2 * err;
            if (e2 > -dy)
            {
                err -= dy;
                x0 += sx;
            }

            if (e2 >= dx)
                continue;

            err += dx;
            y0 += sy;
        }
    }

    private static byte[] BuildCoverageGamma(double gamma)
    {
        var t = new byte[256];
        for (var i = 0; i < 256; i++)
            t[i] = (byte)Math.Clamp((int)Math.Round(255.0 * Math.Pow(i / 255.0, gamma)), 0, 255);
        return t;
    }

    internal void BlitGrayBitmap(
        int destX,
        int destY,
        int glyphWidth,
        int glyphHeight,
        int pitch,
        byte[] glyphBuffer,
        bool invertRows,
        byte r,
        byte g,
        byte b,
        string blendMode = "Normal"
    )
    {
        for (var row = 0; row < glyphHeight; row++)
        {
            var srcRow = invertRows ? glyphHeight - 1 - row : row;
            var rowOffset = srcRow * pitch;
            for (var col = 0; col < glyphWidth; col++)
            {
                var alpha = CoverageGamma[glyphBuffer[rowOffset + col]];
                if (alpha == 0)
                    continue;

                SetPixel(destX + col,
                    destY + row,
                    r,
                    g,
                    b,
                    alpha,
                    blendMode);
            }
        }
    }

    // Writes an opaque RGB pixel directly (used for image blitting, no alpha).
    [SuppressMessage("ReSharper", "BadListLineBreaks")]
    internal void BlitImagePixel(
        int x,
        int y,
        byte r,
        byte g,
        byte b
    )
    {
        if ((uint)x >= (uint)Width || (uint)y >= (uint)Height)
            return;
        if (!InClip(x, y))
            return;

        var i = ((y * Width) + x) * 4;
        _data[i] = r;
        _data[i + 1] = g;
        _data[i + 2] = b;
        _data[i + 3] = 255;
    }

    // Reads the RGB value of a pixel (used by warp blit).
    internal (byte R, byte G, byte B) GetPixelRgb(int x, int y)
    {
        if ((uint)x >= (uint)Width || (uint)y >= (uint)Height)
            return (255, 255, 255);
        var i = ((y * Width) + x) * 4;
        return (_data[i], _data[i + 1], _data[i + 2]);
    }

    // Composites an RGB pixel over the existing background using the given alpha and blend mode.
    // Used for image soft masks (/SMask) and image blitting with transparency.
    internal void BlendPixel(
        int x,
        int y,
        byte r,
        byte g,
        byte b,
        byte a,
        string blendMode = "Normal"
    ) =>
        SetPixel(x,
            y,
            r,
            g,
            b,
            a,
            blendMode);

    /// <summary>
    ///     Returns the axis-aligned bounding box of the active clip mask in device pixels,
    ///     or the full buffer bounds if no clip is set. Used as a fast iteration range for
    ///     shading and pattern paint operations.
    /// </summary>
    internal (int X0, int Y0, int X1, int Y1) ClipBounds()
    {
        if (_clipMask is null) return (0, 0, Width, Height);
        var x0 = Width;
        var y0 = Height;
        var x1 = 0;
        var y1 = 0;
        for (var y = 0; y < Height; y++)
        for (var x = 0; x < Width; x++)
        {
            if (_clipMask[(y * Width) + x] == 0) continue;
            if (x < x0) x0 = x;
            if (y < y0) y0 = y;
            if (x > x1) x1 = x;
            if (y > y1) y1 = y;
        }

        // No pixel was inside — return empty rect.
        return x0 > x1 ? (0, 0, 0, 0) : (x0, y0, x1 + 1, y1 + 1);
    }

    internal byte[] ToArgbBytes() => _data;

    // Fills a filled circle at (cx,cy) with radius r — used for round line caps and joins.
    internal void FillCircle(
        int cx,
        int cy,
        int r,
        byte red,
        byte grn,
        byte blu,
        byte a = 255,
        string blendMode = "Normal"
    )
    {
        var r2 = r * r;
        for (var dy = -r; dy <= r; dy++)
        for (var dx = -r; dx <= r; dx++)
        {
            if ((dx * dx) + (dy * dy) <= r2)
            {
                SetPixel(cx + dx,
                    cy + dy,
                    red,
                    grn,
                    blu,
                    a,
                    blendMode);
            }
        }
    }

    // Fills the triangle (x0,y0)-(x1,y1)-(x2,y2) — used for bevel and miter joins.
    // Uses the same scanline algorithm as the polygon rasteriser.
    internal void FillTriangle(
        int x0,
        int y0,
        int x1,
        int y1,
        int x2,
        int y2,
        byte r,
        byte g,
        byte b,
        byte a = 255,
        string blendMode = "Normal"
    )
    {
        var pts = new (double X, double Y)[] { (x0, y0), (x1, y1), (x2, y2) };
        var minY = Math.Max(0, (int)Math.Floor((double)Math.Min(y0, Math.Min(y1, y2))));
        var maxY = Math.Min(Height - 1, (int)Math.Ceiling((double)Math.Max(y0, Math.Max(y1, y2))));
        for (var y = minY; y <= maxY; y++)
        {
            var sy = y + 0.5;
            var crosses = new List<double>();
            for (var i = 0; i < 3; i++)
            {
                var (ax, ay) = pts[i];
                var (bx, by) = pts[(i + 1) % 3];
                if (ay == by) continue;
                if (sy >= Math.Min(ay, by) && sy < Math.Max(ay, by))
                    crosses.Add(ax + ((sy - ay) / (by - ay) * (bx - ax)));
            }

            if (crosses.Count < 2) continue;
            crosses.Sort();
            var xa = Math.Max(0, (int)Math.Round(crosses[0]));
            var xb = Math.Min(Width - 1, (int)Math.Round(crosses[^1]));
            for (var px = xa; px <= xb; px++)
            {
                SetPixel(px,
                    y,
                    r,
                    g,
                    b,
                    a,
                    blendMode);
            }
        }
    }
}
