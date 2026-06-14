using Unchained.Drawing.Primitives;

namespace Unchained.Pdf.Rendering.Rendering;

// Path construction (m/l/c/v/y/h/re), filling (nonzero & even-odd), stroking (with dash,
// caps and joins) and clip-path rasterisation.
internal sealed partial class PageRenderer
{
    private void PathMoveTo(double x, double y)
    {
        // Start a new subpath. Does NOT clear earlier subpaths (a path may contain several).
        _curSub = [(x, y)];
        _subpaths.Add(_curSub);
        _pathStart = _currentPoint = (x, y);
        _inPath = true;
    }

    private void PathLineTo(double x, double y)
    {
        if (_curSub is null)
        {
            PathMoveTo(x, y);
            return;
        }

        _curSub.Add((x, y));
        _currentPoint = (x, y);
    }

    // ReSharper disable once BadListLineBreaks
    private void PathRect(
        double x,
        double y,
        double w,
        double h
    )
    {
        // A rectangle is its own closed subpath (ISO 32000-1 §8.5.2.1).
        PathMoveTo(x, y);
        _curSub!.Add((x + w, y));
        _curSub.Add((x + w, y + h));
        _curSub.Add((x, y + h));
        _curSub.Add((x, y)); // close
        _currentPoint = (x, y);
    }

    private void PathCurveTo(
        double x1,
        double y1,
        double x2,
        double y2,
        double x3,
        double y3
    )
    {
        if (_curSub is null) PathMoveTo(_currentPoint.X, _currentPoint.Y);
        var p0 = _currentPoint;
        for (var t = 1; t <= 8; t++)
        {
            var s = t / 8.0;
            var u = 1 - s;
            var bx = (u * u * u * p0.X) + (3 * u * u * s * x1) + (3 * u * s * s * x2) + (s * s * s * x3);
            var by = (u * u * u * p0.Y) + (3 * u * u * s * y1) + (3 * u * s * s * y2) + (s * s * s * y3);
            _curSub!.Add((bx, by));
            _currentPoint = (bx, by);
        }
    }

    private void PathClose()
    {
        if (!_inPath || _curSub is not { Count: > 0 }) return;

        _curSub.Add(_pathStart);
        _currentPoint = _pathStart;
    }

    // Fills the current path. evenOdd selects the even-odd rule (f*/B*/b*) vs the default
    // nonzero winding rule (f/F/B/b). A single axis-aligned rectangle uses a fast FillRect
    // path; everything else is scan-converted as a polygon (all subpaths together).
    private void DrawFill(bool evenOdd)
    {
        if (_subpaths.Count == 0) return;

        // Shading pattern fill: paint the gradient clipped to the path's bounding box.
        if (_gs.FillShadingName is { } shName && shadings is not null
                                              && shadings.TryGetValue(shName, out var shInfo))
        {
            PaintShadingInPathBounds(shInfo);
            return;
        }

        // Tiling pattern fill: tile the pattern cell across the path's bounding box.
        if (_gs.FillTilingName is { } tileName && tilingPatterns is not null
                                               && tilingPatterns.TryGetValue(tileName, out var tileInfo))
        {
            PaintTilingInPathBounds(tileInfo);
            return;
        }

        byte fr = _gs.FillR, fg = _gs.FillG, fb = _gs.FillB;
        // Tiling/non-shading patterns aren't rendered. Filling them with the (often black)
        // underlying colour produces large wrong dark blocks; skipping them entirely loses
        // the region's visual weight. Real-world patterns (e.g. TikZ /pgfpat hatches)
        // average to roughly a mid-tone, so approximate with a neutral grey.
        if (_gs.FillIsPattern)
            fr = fg = fb = 160;

        if (TryGetRectangle(out var rminX, out var rminY, out var rmaxX, out var rmaxY))
        {
            var (px1, py1) = UToPixel(rminX, rmaxY);
            var (px2, py2) = UToPixel(rmaxX, rminY);
            if (HasSoftMask)
            {
                FillRectSoftMasked(
                    (int)px1,
                    (int)py1,
                    (int)(px2 - px1 + 1),
                    (int)(py2 - py1 + 1),
                    fr,
                    fg,
                    fb,
                    _gs.FillA,
                    _gs.BlendMode
                );
            }
            else
            {
                buffer.FillRect(
                    (int)px1,
                    (int)py1,
                    (int)(px2 - px1 + 1),
                    (int)(py2 - py1 + 1),
                    fr,
                    fg,
                    fb,
                    _gs.FillA,
                    _gs.BlendMode
                );
            }

            return;
        }

        FillPolygon(evenOdd, fr, fg, fb);
    }

    // Device-space bounding box of the current path's subpaths.
    private (double MinX, double MinY, double MaxX, double MaxY) PathDeviceBounds()
    {
        var minX = double.MaxValue;
        var minY = double.MaxValue;
        var maxX = double.MinValue;
        var maxY = double.MinValue;
        foreach (var sub in _subpaths)
        foreach (var (ux, uy) in sub)
        {
            var (px, py) = UToPixel(ux, uy);
            if (px < minX) minX = px;
            if (py < minY) minY = py;
            if (px > maxX) maxX = px;
            if (py > maxY) maxY = py;
        }

        return (minX, minY, maxX, maxY);
    }

    // Scan-converts all current subpaths to device pixels and fills using the given winding
    // rule. Each subpath is treated as implicitly closed (PDF fills close open subpaths).
    private void FillPolygon(
        bool evenOdd,
        byte fr,
        byte fg,
        byte fb
    )
    {
        // Flatten every subpath to device-space points and find the vertical extent.
        var polys = new List<(double X, double Y)[]>(_subpaths.Count);
        var minY = double.MaxValue;
        var maxY = double.MinValue;
        foreach (var sub in _subpaths)
        {
            if (sub.Count < 2) continue;

            var pts = new (double X, double Y)[sub.Count];
            for (var i = 0; i < sub.Count; i++)
            {
                var (px, py) = UToPixel(sub[i].X, sub[i].Y);
                pts[i] = (px, py);
                if (py < minY) minY = py;
                if (py > maxY) maxY = py;
            }

            polys.Add(pts);
        }

        if (polys.Count == 0) return;

        var y0 = Math.Max(0, (int)Math.Floor(minY));
        var y1 = Math.Min(buffer.Height - 1, (int)Math.Ceiling(maxY));

        // For each scanline, collect edge crossings (x, winding direction), then fill the
        // spans selected by the winding rule. Sample at pixel centres (y + 0.5).
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
                    var (bx, by) = pts[(i + 1) % n];        // implicit close
                    if (Math.Abs(ay - by) < 0.05) continue; // horizontal edge contributes no crossing
                    // Half-open [min,max) so shared vertices aren't double-counted.
                    if (!(sy >= Math.Min(ay, by)) || !(sy < Math.Max(ay, by))) continue;

                    var t = (sy - ay) / (by - ay);
                    var cx = ax + (t * (bx - ax));
                    xs.Add((cx, by > ay ? 1 : -1));
                }
            }

            if (xs.Count < 2) continue;

            xs.Sort(static (p, q) => p.X.CompareTo(q.X));

            var wind = 0;
            for (var i = 0; i < xs.Count - 1; i++)
            {
                wind += xs[i].Dir;
                var inside = evenOdd ? ((i + 1) & 1) == 1 : wind != 0;
                if (!inside) continue;

                var xStart = (int)Math.Round(xs[i].X);
                var xEnd = (int)Math.Round(xs[i + 1].X);
                if (xEnd <= xStart) continue;

                if (HasSoftMask)
                {
                    FillSpanSoftMasked(
                        y,
                        xStart,
                        xEnd - 1,
                        fr,
                        fg,
                        fb,
                        _gs.FillA,
                        _gs.BlendMode
                    );
                }
                else
                {
                    buffer.FillSpan(
                        y,
                        xStart,
                        xEnd - 1,
                        fr,
                        fg,
                        fb,
                        _gs.FillA,
                        _gs.BlendMode
                    );
                }
            }
        }
    }

    private void DrawStroke()
    {
        // Line width is specified in user-space units, which the CTM scales before the
        // device-space (DPI) scale is applied (ISO 32000-1 §8.4.3.2). Using only the device
        // scale ignores any cm scaling — e.g. a chart drawn under `cm 0.1 0 0 0.1` with
        // `w 5` must render as 0.5 user units, not 5, or every stroke is ~10× too thick and
        // bleeds outside its intended bounds. Use the CTM's average linear scale.
        var ctmScale = CtmAverageScale();
        var thickPx = Math.Max(1, (int)Math.Round(_gs.LineWidth * ctmScale * scale));

        // Dash pattern: convert the user-space on/off lengths to device pixels once.
        var dashPx = _gs.DashLengths.Length > 0
            ? _gs.DashLengths.Select(d => Math.Max(0.0, d * ctmScale * scale)).ToArray()
            : null;
        // A pattern of all zeros means "solid" — ignore it.
        if (dashPx is not null && dashPx.All(static d => d <= 0)) dashPx = null;

        foreach (var sub in _subpaths)
        {
            for (var i = 0; i + 1 < sub.Count; i++)
            {
                var (x0, y0) = UToPixel(sub[i].X, sub[i].Y);
                var (x1, y1) = UToPixel(sub[i + 1].X, sub[i + 1].Y);
                if (dashPx is null)
                {
                    buffer.DrawLine(
                        (int)x0,
                        (int)y0,
                        (int)x1,
                        (int)y1,
                        _gs.StrokeR,
                        _gs.StrokeG,
                        _gs.StrokeB,
                        thickPx,
                        _gs.StrokeA,
                        _gs.BlendMode
                    );
                }
                else
                {
                    DrawDashedLine(
                        x0,
                        y0,
                        x1,
                        y1,
                        thickPx,
                        dashPx
                    );
                }
            }

            // Line joins at interior vertices (where two segments meet).
            // Only meaningful when stroke is thick enough to show gaps.
            if (_gs.LineJoin != 0 && thickPx > 1 && sub.Count >= 3)
            {
                var half = thickPx / 2;
                for (var i = 1; i + 1 < sub.Count; i++)
                {
                    var (px, py) = UToPixel(sub[i].X, sub[i].Y);
                    DrawLineJoin(
                        UToPixel(sub[i - 1].X, sub[i - 1].Y),
                        (px, py),
                        UToPixel(sub[i + 1].X, sub[i + 1].Y),
                        half
                    );
                }
            }

            // Line caps on open subpaths (cap = 1 round, 2 projecting square).
            if (_gs.LineCap == 0 || sub.Count < 2) continue;

            var capR = Math.Max(1, thickPx / 2);
            var (ax, ay) = UToPixel(sub[0].X, sub[0].Y);
            var (bx, by) = UToPixel(sub[^1].X, sub[^1].Y);
            if (_gs.LineCap == 1)
            {
                buffer.FillCircle(
                    (int)ax,
                    (int)ay,
                    capR,
                    _gs.StrokeR,
                    _gs.StrokeG,
                    _gs.StrokeB,
                    _gs.StrokeA,
                    _gs.BlendMode
                );
                buffer.FillCircle(
                    (int)bx,
                    (int)by,
                    capR,
                    _gs.StrokeR,
                    _gs.StrokeG,
                    _gs.StrokeB,
                    _gs.StrokeA,
                    _gs.BlendMode
                );
            }
            else
            {
                buffer.FillRect(
                    (int)ax - capR,
                    (int)ay - capR,
                    thickPx,
                    thickPx,
                    _gs.StrokeR,
                    _gs.StrokeG,
                    _gs.StrokeB,
                    _gs.StrokeA,
                    _gs.BlendMode
                );
                buffer.FillRect(
                    (int)bx - capR,
                    (int)by - capR,
                    thickPx,
                    thickPx,
                    _gs.StrokeR,
                    _gs.StrokeG,
                    _gs.StrokeB,
                    _gs.StrokeA,
                    _gs.BlendMode
                );
            }
        }
    }

    // Renders the line join at vertex B where segment A→B meets segment B→C.
    // The join fills the gap between the two stroke bands at the corner.
    private void DrawLineJoin(
        (double X, double Y) a,
        (double X, double Y) b,
        (double X, double Y) c,
        int half
    )
    {
        // Direction vectors of incoming (A→B) and outgoing (B→C) segments.
        var dxIn = b.X - a.X;
        var dyIn = b.Y - a.Y;
        var dxOut = c.X - b.X;
        var dyOut = c.Y - b.Y;
        var lenIn = Vector2D.Magnitude(dxIn, dyIn);
        var lenOut = Vector2D.Magnitude(dxOut, dyOut);
        if (lenIn < RenderingConstants.Epsilon || lenOut < RenderingConstants.Epsilon) return;

        // Unit normals (perpendicular to each segment, pointing "outward").
        var nxIn = -dyIn / lenIn;
        var nyIn = dxIn / lenIn;
        var nxOut = -dyOut / lenOut;
        var nyOut = dxOut / lenOut;

        var bx = (int)b.X;
        var by = (int)b.Y;

        switch (_gs.LineJoin)
        {
            case 1: // Round — fill circle at join vertex
                buffer.FillCircle(
                    bx,
                    by,
                    half,
                    _gs.StrokeR,
                    _gs.StrokeG,
                    _gs.StrokeB,
                    _gs.StrokeA,
                    _gs.BlendMode
                );
            break;

            case 2: // Bevel — fill triangle between the two outer corners and the vertex
            {
                var ox1 = (int)(bx + (nxIn * half));
                var oy1 = (int)(by + (nyIn * half));
                var ox2 = (int)(bx + (nxOut * half));
                var oy2 = (int)(by + (nyOut * half));
                buffer.FillTriangle(
                    bx,
                    by,
                    ox1,
                    oy1,
                    ox2,
                    oy2,
                    _gs.StrokeR,
                    _gs.StrokeG,
                    _gs.StrokeB,
                    _gs.StrokeA,
                    _gs.BlendMode
                );
                // Also fill the inner side.
                var ix1 = (int)(bx - (nxIn * half));
                var iy1 = (int)(by - (nyIn * half));
                var ix2 = (int)(bx - (nxOut * half));
                var iy2 = (int)(by - (nyOut * half));
                buffer.FillTriangle(
                    bx,
                    by,
                    ix1,
                    iy1,
                    ix2,
                    iy2,
                    _gs.StrokeR,
                    _gs.StrokeG,
                    _gs.StrokeB,
                    _gs.StrokeA,
                    _gs.BlendMode
                );
                break;
            }

            default: // Miter (0) — extend outer edges to intersection point
            {
                // Compute where the outer edges of the two strokes would intersect.
                // Edge 1: point = b + nIn*half, direction = (dxIn/lenIn, dyIn/lenIn)
                // Edge 2: point = b + nOut*half, direction = (dxOut/lenOut, dyOut/lenOut)
                // Fall back to bevel if the angle is too shallow (miter limit exceeded).
                var sinHalf = (nxIn * dyOut / lenOut) - (nyIn * dxOut / lenOut);
                if (Math.Abs(sinHalf) < RenderingConstants.Epsilon) break; // parallel segments

                var miterLen = half / Math.Abs(sinHalf);
                if (miterLen > half * _gs.MiterLimit) goto case 2; // exceed limit → bevel

                var mx = bx + ((nxIn + nxOut) * half / 2.0 / Math.Max(RenderingConstants.Epsilon, Math.Abs(sinHalf)));
                var my = by + ((nyIn + nyOut) * half / 2.0 / Math.Max(RenderingConstants.Epsilon, Math.Abs(sinHalf)));

                var ox1 = (int)(bx + (nxIn * half));
                var oy1 = (int)(by + (nyIn * half));
                var ox2 = (int)(bx + (nxOut * half));
                var oy2 = (int)(by + (nyOut * half));
                buffer.FillTriangle(
                    (int)mx,
                    (int)my,
                    ox1,
                    oy1,
                    ox2,
                    oy2,
                    _gs.StrokeR,
                    _gs.StrokeG,
                    _gs.StrokeB,
                    _gs.StrokeA,
                    _gs.BlendMode
                );
                // Inner side.
                var ix1 = (int)(bx - (nxIn * half));
                var iy1 = (int)(by - (nyIn * half));
                var ix2 = (int)(bx - (nxOut * half));
                var iy2 = (int)(by - (nyOut * half));
                var imx = bx - ((nxIn + nxOut) * half / 2.0 / Math.Max(RenderingConstants.Epsilon, Math.Abs(sinHalf)));
                var imy = by - ((nyIn + nyOut) * half / 2.0 / Math.Max(RenderingConstants.Epsilon, Math.Abs(sinHalf)));
                buffer.FillTriangle(
                    (int)imx,
                    (int)imy,
                    ix1,
                    iy1,
                    ix2,
                    iy2,
                    _gs.StrokeR,
                    _gs.StrokeG,
                    _gs.StrokeB,
                    _gs.StrokeA,
                    _gs.BlendMode
                );
                break;
            }
        }
    }

    // Draws a line as a dash pattern by walking its length and emitting "on" sub-segments.
    // dashPx alternates on/off lengths starting with "on"; an odd-length array repeats to
    // form the full cycle (ISO 32000-1 §8.4.3.6). Phase is assumed 0 per segment.
    private void DrawDashedLine(
        double x0,
        double y0,
        double x1,
        double y1,
        int thickPx,
        IReadOnlyList<double> dashPx
    )
    {
        var dx = x1 - x0;
        var dy = y1 - y0;
        var len = Vector2D.Magnitude(dx, dy);
        if (len < RenderingConstants.Epsilon) return;

        var ux = dx / len;
        var uy = dy / len;

        var pos = 0.0;
        var idx = 0;
        var on = true;
        while (pos < len)
        {
            var seg = dashPx[idx % dashPx.Count];
            if (seg <= 0)
            {
                idx++;
                on = !on;
                continue;
            }

            var end = Math.Min(len, pos + seg);
            if (on)
            {
                var ax = x0 + (ux * pos);
                var ay = y0 + (uy * pos);
                var bx = x0 + (ux * end);
                var by = y0 + (uy * end);
                buffer.DrawLine(
                    (int)ax,
                    (int)ay,
                    (int)bx,
                    (int)by,
                    _gs.StrokeR,
                    _gs.StrokeG,
                    _gs.StrokeB,
                    thickPx,
                    _gs.StrokeA,
                    _gs.BlendMode
                );
            }

            pos = end;
            idx++;
            on = !on;
        }
    }

    // Rasterises the current path into the buffer's clip mask using the given winding rule.
    // Replaces the old bbox approximation — now every pixel is tested against the true
    // clip polygon, so diagonal edges, circles, and holes clip correctly.
    private void ApplyPendingClip(bool evenOdd = false)
    {
        // Flatten every subpath to device-space point arrays.
        var polys = new List<(double X, double Y)[]>(_subpaths.Count);
        foreach (var sub in _subpaths)
        {
            if (sub.Count < 2)
                continue;

            var pts = new (double X, double Y)[sub.Count];
            for (var i = 0; i < sub.Count; i++)
                pts[i] = UToPixel(sub[i].X, sub[i].Y);
            polys.Add(pts);
        }

        if (polys.Count == 0) return;

        buffer.SetClipPolygons(polys, evenOdd);
    }

    // True when the path is a single axis-aligned rectangle (the common case: page
    // backgrounds, table cells, rules). Returns its user-space bounds. Such paths keep the
    // fast FillRect path and avoid the scanline rasteriser.
    private bool TryGetRectangle(
        out double minX,
        out double minY,
        out double maxX,
        out double maxY
    )
    {
        minX = minY = maxX = maxY = 0;
        if (_subpaths.Count != 1) return false;

        var sub = _subpaths[0];
        // 4 or 5 points (5th = explicit close back to start).
        if (sub.Count is < 4 or > 5) return false;

        var distinctX = sub.Select(static p => p.X).Distinct().Count();
        var distinctY = sub.Select(static p => p.Y).Distinct().Count();
        if (distinctX != 2 || distinctY != 2) return false;

        minX = sub.Min(static p => p.X);
        maxX = sub.Max(static p => p.X);
        minY = sub.Min(static p => p.Y);
        maxY = sub.Max(static p => p.Y);

        return true;
    }
}
