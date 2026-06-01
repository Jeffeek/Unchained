using SharpFont;
using Unchained.Pdf.Core;
using Unchained.Pdf.Models;

namespace Unchained.Pdf.Rendering.Rendering;

/// <summary>
/// Walks a list of <see cref="ContentOperator"/> records and rasterizes them
/// into a <see cref="RasterBuffer"/> using the PDF graphics model (ISO 32000-1 §8–9).
/// </summary>
internal sealed class PageRenderer(
    RasterBuffer buffer,
    FontCache fonts,
    double scale,
    double pageHeightPt
)
{
    // Current path segments accumulated between path construction operators.
    private readonly List<(double X, double Y)> _currentPath = [];
    private (double X, double Y) _pathStart;
    private (double X, double Y) _currentPoint;
    private bool _inPath;

    // Graphics state stack.
    private readonly Stack<GraphicsState> _gsStack = new();
    private GraphicsState _gs = new();

    internal void Render(
        IEnumerable<ContentOperator> operators,
        IReadOnlyDictionary<string, string> fontMap
    )
    {
        foreach (var op in operators)
            Execute(op, fontMap);
    }

    private void Execute(ContentOperator op, IReadOnlyDictionary<string, string> fontMap)
    {
        switch (op.Name)
        {
            // ── Graphics state ────────────────────────────────────────────────
            case "q": _gsStack.Push(_gs.Clone()); break;
            case "Q":
            {
                if (_gsStack.Count > 0) _gs = _gsStack.Pop();
                break;
            }
            case "cm" when op.Operands.Count >= 6:
            {
                double[] m = [Num(op, 0), Num(op, 1), Num(op, 2), Num(op, 3), Num(op, 4), Num(op, 5)];
                _gs.Ctm = GraphicsState.MultiplyMatrix(_gs.Ctm, m);
                break;
            }

            // ── Colour operators ──────────────────────────────────────────────
            case "g" when op.Operands.Count >= 1:
            {
                SetFillGray(Num(op, 0));
                break;
            }
            case "G" when op.Operands.Count >= 1:
            {
                SetStrokeGray(Num(op, 0));
                break;
            }
            case "rg" when op.Operands.Count >= 3:
            {
                SetFillRgb(Num(op, 0), Num(op, 1), Num(op, 2));
                break;
            }
            case "RG" when op.Operands.Count >= 3:
            {
                SetStrokeRgb(Num(op, 0), Num(op, 1), Num(op, 2));
                break;
            }
            case "k" when op.Operands.Count >= 4:
            {
                var (r, g, b) = CmykToRgb(Num(op, 0), Num(op, 1), Num(op, 2), Num(op, 3));
                SetFillRgb(r, g, b);
                break;
            }
            case "K" when op.Operands.Count >= 4:
            {
                var (r, g, b) = CmykToRgb(Num(op, 0), Num(op, 1), Num(op, 2), Num(op, 3));
                SetStrokeRgb(r, g, b);
                break;
            }

            // ── Line width ────────────────────────────────────────────────────
            case "w" when op.Operands.Count >= 1:
            {
                _gs.LineWidth = Num(op, 0);
                break;
            }
            // ── Path construction ─────────────────────────────────────────────
            case "m" when op.Operands.Count >= 2:
            {
                PathMoveTo(Num(op, 0), Num(op, 1));
                break;
            }
            case "l" when op.Operands.Count >= 2:
            {
                PathLineTo(Num(op, 0), Num(op, 1));
                break;
            }
            case "c" when op.Operands.Count >= 6:
            {
                PathCurveTo(
                    Num(op, 0),
                    Num(op, 1),
                    Num(op, 2),
                    Num(op, 3),
                    Num(op, 4),
                    Num(op, 5)
                );
                break;
            }
            case "v" when op.Operands.Count >= 4:
            {
                PathCurveTo(
                    _currentPoint.X,
                    _currentPoint.Y,
                    Num(op, 0),
                    Num(op, 1),
                    Num(op, 2),
                    Num(op, 3)
                );
                break;
            }
            case "y" when op.Operands.Count >= 4:
            {
                PathCurveTo(
                    Num(op, 0),
                    Num(op, 1),
                    Num(op, 2),
                    Num(op, 3),
                    Num(op, 2),
                    Num(op, 3)
                );
                break;
            }
            case "h":
            {
                if (_inPath)
                {
                    _currentPath.Add(_pathStart);
                    _currentPoint = _pathStart;
                }

                break;
            }
            case "re" when op.Operands.Count >= 4:
            {
                PathRect(Num(op, 0), Num(op, 1), Num(op, 2), Num(op, 3));
                break;
            }

            // ── Path painting ─────────────────────────────────────────────────
            case "S":
            {
                StrokePath();
                break;
            }
            case "s":
            {
                _currentPath.Add(_pathStart);
                StrokePath();
                break;
            }
            case "f":
            case "F":
            case "f*":
            {
                FillPath();
                break;
            }
            case "B":
            case "b":
            case "B*":
            case "b*":
            {
                FillPath();
                StrokePath();
                break;
            }
            case "n":
                ClearPath();
            break;

            // ── Text object ───────────────────────────────────────────────────
            case "BT":
            {
                _gs.TextMatrix = [1, 0, 0, 1, 0, 0];
                _gs.TextLineMatrix = [1, 0, 0, 1, 0, 0];
                break;
            }
            case "ET":
            {
                break;
            }

            // ── Text state ────────────────────────────────────────────────────
            case "Tf" when op.Operands.Count >= 2:
            {
                var resName = (op.Operands[0] as PdfName)?.Value ?? "";
                _gs.FontName = fontMap.GetValueOrDefault(resName, resName);
                _gs.FontSize = Num(op, 1);

                break;
            }
            case "Tc" when op.Operands.Count >= 1:
            {
                _gs.CharSpace = Num(op, 0);
                break;
            }
            case "Tw" when op.Operands.Count >= 1:
            {
                _gs.WordSpace = Num(op, 0);
                break;
            }
            case "Tz" when op.Operands.Count >= 1:
            {
                _gs.HorizontalScale = Num(op, 0);
                break;
            }
            case "TL" when op.Operands.Count >= 1:
            {
                _gs.Leading = Num(op, 0);
                break;
            }

            // ── Text positioning ──────────────────────────────────────────────
            case "Tm" when op.Operands.Count >= 6:
            {
                _gs.TextMatrix = [Num(op, 0), Num(op, 1), Num(op, 2), Num(op, 3), Num(op, 4), Num(op, 5)];
                _gs.TextLineMatrix = (double[])_gs.TextMatrix.Clone();
                break;
            }
            case "Td" when op.Operands.Count >= 2:
            {
                MoveTextLine(Num(op, 0), Num(op, 1));
                break;
            }
            case "TD" when op.Operands.Count >= 2:
            {
                _gs.Leading = -Num(op, 1);
                MoveTextLine(Num(op, 0), Num(op, 1));
                break;
            }
            case "T*":
            {
                MoveTextLine(0, -_gs.Leading);
                break;
            }

            // ── Text showing ──────────────────────────────────────────────────
            case "Tj" when op.Operands.Count >= 1:
            {
                if (op.Operands[0] is PdfString tj) ShowString(tj.Bytes.Span);
                break;
            }
            case "'":
            {
                MoveTextLine(0, -_gs.Leading);
                if (op.Operands is [PdfString sq, ..])
                    ShowString(sq.Bytes.Span);
                break;
            }
            case "\"" when op.Operands.Count >= 3:
            {
                _gs.WordSpace = Num(op, 0);
                _gs.CharSpace = Num(op, 1);
                MoveTextLine(0, -_gs.Leading);
                if (op.Operands[2] is PdfString sdq)
                    ShowString(sdq.Bytes.Span);
                break;
            }
            case "TJ" when op.Operands.Count >= 1:
            {
                if (op.Operands[0] is PdfArray arr)
                    ShowArray(arr);
                break;
            }

            // ── XObject ───────────────────────────────────────────────────────
            case "Do" when op.Operands.Count >= 1:
            {
                // XObject rendering deferred — requires embedded-font subsystem (M6).
                break;
            }
        }
    }

    // ── Text rendering ────────────────────────────────────────────────────────

    private void MoveTextLine(double tx, double ty)
    {
        // Tlm' = [1 0 0 1 tx ty] × Tlm
        var newE = (tx * _gs.TextLineMatrix[0]) + (ty * _gs.TextLineMatrix[2]) + _gs.TextLineMatrix[4];
        var newF = (tx * _gs.TextLineMatrix[1]) + (ty * _gs.TextLineMatrix[3]) + _gs.TextLineMatrix[5];
        _gs.TextLineMatrix[4] = newE;
        _gs.TextLineMatrix[5] = newF;
        _gs.TextMatrix = (double[])_gs.TextLineMatrix.Clone();
    }

    private void ShowString(ReadOnlySpan<byte> bytes)
    {
        if (_gs.FontSize <= 0 || _gs.FontName.Length == 0)
            return;

        try
        {
            var face = fonts.GetFace(_gs.FontName);
            var pixelSize = (uint)Math.Max(1, Math.Round(_gs.FontSize * scale));
            face.SetPixelSizes(0, pixelSize);

            foreach (var c in bytes)
            {
                face.LoadChar(c, LoadFlags.Render, LoadTarget.Normal);
                var glyph = face.Glyph;
                var bm = glyph.Bitmap;

                // Origin in pixel space (bottom of baseline)
                var originX = _gs.TextMatrix[4];
                var originY = _gs.TextMatrix[5];
                var (px, py) = UToPixel(originX, originY);

                // FreeType bitmap top-left = origin + (bearingX, -bearingY)
                var bmpX = (int)(px + glyph.BitmapLeft);
                var bmpY = (int)(py - glyph.BitmapTop);

                buffer.BlitGlyphBitmap(
                    bmpX,
                    bmpY,
                    bm,
                    _gs.FillR,
                    _gs.FillG,
                    _gs.FillB
                );

                // Advance text position
                var advance = ((glyph.Advance.X.Value / 65536.0 / scale) + _gs.CharSpace) * (_gs.HorizontalScale / 100.0);
                if (c == 32)
                    advance += _gs.WordSpace * (_gs.HorizontalScale / 100.0);
                _gs.TextMatrix[4] += advance;
            }
        }
        // ReSharper disable once EmptyGeneralCatchClause
        catch { }
    }

    private void ShowArray(PdfArray arr)
    {
        foreach (var elem in arr.Elements)
        {
            switch (elem)
            {
                case PdfString s:
                {
                    ShowString(s.Bytes.Span);
                    break;
                }
                case PdfInteger n:
                {
                    _gs.TextMatrix[4] -= n.Value / 1000.0 * _gs.FontSize * (_gs.HorizontalScale / 100.0);
                    break;
                }
                case PdfReal r:
                {
                    _gs.TextMatrix[4] -= r.Value / 1000.0 * _gs.FontSize * (_gs.HorizontalScale / 100.0);
                    break;
                }
            }
        }
    }

    // ── Path rendering ────────────────────────────────────────────────────────

    private void PathMoveTo(double x, double y)
    {
        _currentPath.Clear();
        _pathStart = _currentPoint = (x, y);
        _inPath = true;
    }

    private void PathLineTo(double x, double y)
    {
        _currentPath.Add(_currentPoint);
        _currentPath.Add((x, y));
        _currentPoint = (x, y);
    }

    // ReSharper disable once BadListLineBreaks
    private void PathRect(double x, double y, double w, double h)
    {
        PathMoveTo(x, y);
        PathLineTo(x + w, y);
        PathLineTo(x + w, y + h);
        PathLineTo(x, y + h);
        _currentPath.Add(_pathStart);
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
        // De Casteljau subdivision: append ~8 line segments per curve.
        var p0 = _currentPoint;
        for (var t = 1; t <= 8; t++)
        {
            var s = t / 8.0;
            var u = 1 - s;
            var bx = (u * u * u * p0.X) + (3 * u * u * s * x1) + (3 * u * s * s * x2) + (s * s * s * x3);
            var by = (u * u * u * p0.Y) + (3 * u * u * s * y1) + (3 * u * s * s * y2) + (s * s * s * y3);
            _currentPath.Add(_currentPoint);
            _currentPath.Add((bx, by));
            _currentPoint = (bx, by);
        }
    }

    private void FillPath()
    {
        // Optimised path: if current path is exactly a rectangle, use FillRect.
        if (_currentPath.Count == 8 && IsRectanglePath())
        {
            var minX = _currentPath.Min(static p => p.X);
            var minY = _currentPath.Min(static p => p.Y);
            var maxX = _currentPath.Max(static p => p.X);
            var maxY = _currentPath.Max(static p => p.Y);
            var (px1, py1) = UToPixel(minX, maxY); // top-left in pixel space
            var (px2, py2) = UToPixel(maxX, minY); // bottom-right
            buffer.FillRect(
                (int)px1,
                (int)py1,
                (int)(px2 - px1 + 1),
                (int)(py2 - py1 + 1),
                _gs.FillR,
                _gs.FillG,
                _gs.FillB
            );
        }

        ClearPath();
    }

    private void StrokePath()
    {
        var thickPx = Math.Max(1, (int)Math.Round(_gs.LineWidth * scale));
        for (var i = 0; i + 1 < _currentPath.Count; i += 2)
        {
            var (x0, y0) = UToPixel(_currentPath[i].X, _currentPath[i].Y);
            var (x1, y1) = UToPixel(_currentPath[i + 1].X, _currentPath[i + 1].Y);
            buffer.DrawLine(
                (int)x0,
                (int)y0,
                (int)x1,
                (int)y1,
                _gs.StrokeR,
                _gs.StrokeG,
                _gs.StrokeB,
                thickPx
            );
        }

        ClearPath();
    }

    private void ClearPath()
    {
        _currentPath.Clear();
        _inPath = false;
    }

    private bool IsRectanglePath()
    {
        // A rectangle path from PathRect produces pairs: (x,y)→(x+w,y)→(x+w,y+h)→(x,y+h)→back
        if (_currentPath.Count < 4)
            return false;

        var ys = _currentPath.Select(static p => p.Y).Distinct().Count();
        var xs = _currentPath.Select(static p => p.X).Distinct().Count();

        return xs == 2 && ys == 2;
    }

    // ── XObject / image ───────────────────────────────────────────────────────

    // PaintXObject is a no-op stub until the embedded-font subsystem (M6) is in place.

    // ── Coordinate helpers ────────────────────────────────────────────────────

    // Convert PDF user-space (x,y) to pixel (px, py) applying CTM and Y-flip.
    private (double Px, double Py) UToPixel(double x, double y)
    {
        var (ux, uy) = _gs.Transform(x, y);
        return (ux * scale, (pageHeightPt - uy) * scale);
    }

    private static double Num(ContentOperator op, int i) => op.Operands[i] switch
    {
        PdfInteger n => n.Value,
        PdfReal r => r.Value,
        _ => 0
    };

    // ReSharper disable once BadListLineBreaks
    private static (double R, double G, double B) CmykToRgb(double c, double m, double y, double k) =>
        ((1 - c) * (1 - k), (1 - m) * (1 - k), (1 - y) * (1 - k));

    private void SetFillGray(double gray)
    {
        var v = (byte)Math.Clamp((int)(gray * 255), 0, 255);
        _gs.FillR = _gs.FillG = _gs.FillB = v;
        _gs.FillA = 255;
    }

    private void SetStrokeGray(double gray)
    {
        var v = (byte)Math.Clamp((int)(gray * 255), 0, 255);
        _gs.StrokeR = _gs.StrokeG = _gs.StrokeB = v;
        _gs.StrokeA = 255;
    }

    private void SetFillRgb(double r, double g, double b)
    {
        _gs.FillR = (byte)Math.Clamp((int)(r * 255), 0, 255);
        _gs.FillG = (byte)Math.Clamp((int)(g * 255), 0, 255);
        _gs.FillB = (byte)Math.Clamp((int)(b * 255), 0, 255);
        _gs.FillA = 255;
    }

    private void SetStrokeRgb(double r, double g, double b)
    {
        _gs.StrokeR = (byte)Math.Clamp((int)(r * 255), 0, 255);
        _gs.StrokeG = (byte)Math.Clamp((int)(g * 255), 0, 255);
        _gs.StrokeB = (byte)Math.Clamp((int)(b * 255), 0, 255);
        _gs.StrokeA = 255;
    }
}
