using Unchained.Pdf.Core;
using Unchained.Pdf.Models;
using Unchained.Drawing;
using Unchained.Drawing.Text;
using LoadFlags = SharpFont.LoadFlags;
using LoadTarget = SharpFont.LoadTarget;

namespace Unchained.Pdf.Rendering.Rendering;

/// <summary>
/// Walks a list of <see cref="ContentOperator"/> records and rasterizes them
/// into a <see cref="RasterBuffer"/> using the PDF graphics model (ISO 32000-1 §8–9).
/// </summary>
internal sealed class PageRenderer(
    RasterBuffer buffer,
    FontCache fonts,
    double scale,
    double pageHeightPt,
    IReadOnlyDictionary<string, byte[]?>? embeddedFontBytes = null,
    IReadOnlyDictionary<string, ImageXObject>? imageXObjects = null,
    double[]? initialCtm = null
)
{
    private readonly List<(double X, double Y)> _currentPath = [];
    private (double X, double Y) _pathStart;
    private (double X, double Y) _currentPoint;
    private bool _inPath;

    private readonly Stack<GraphicsState> _gsStack = new();
    // Apply the initial CTM from the renderer (encodes page rotation + coordinate origin).
    private GraphicsState _gs = new()
    {
        Ctm = initialCtm ?? [1, 0, 0, 1, 0, 0]
    };

    // Count of text operators that produced no glyphs due to font-loading errors.
    internal int TextErrorCount { get; private set; }

    // Total glyph bitmaps successfully passed to BlitGlyphBitmap (glyph not skipped by catch).
    internal int GlyphsAttempted { get; private set; }

    // Total glyph bitmaps whose LoadGlyph failed (inner catch { continue; }).
    internal int GlyphsSkipped { get; private set; }

    internal void Render(
        IEnumerable<ContentOperator> operators,
        IReadOnlyDictionary<string, string> fontMap)
    {
        foreach (var op in operators)
        {
            // Per-operator guard: one bad operator must not kill the whole page.
            try
            {
                Execute(op, fontMap);
            }
            catch (Exception ex)
            {
                // Count text-operator failures for diagnostics, then continue.
                if (op.Name is "Tj" or "TJ" or "'" or "\"")
                    TextErrorCount++;
                // Non-text operators: swallow silently; layout can still continue.
                _ = ex; // suppress unused-variable warning
            }
        }
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

            // ── Colour — DeviceGray ───────────────────────────────────────────
            case "g" when op.Operands.Count >= 1: SetFillGray(Num(op, 0)); break;
            case "G" when op.Operands.Count >= 1: SetStrokeGray(Num(op, 0)); break;

            // ── Colour — DeviceRGB ────────────────────────────────────────────
            case "rg" when op.Operands.Count >= 3:
                SetFillRgb(Num(op, 0), Num(op, 1), Num(op, 2)); break;
            case "RG" when op.Operands.Count >= 3:
                SetStrokeRgb(Num(op, 0), Num(op, 1), Num(op, 2)); break;

            // ── Colour — DeviceCMYK ───────────────────────────────────────────
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

            // ── Colour — colour-space selection (cs/CS) and setting (sc/SC/scn/SCN) ──
            // We only handle DeviceGray/DeviceRGB/DeviceCMYK here; others are ignored.
            // The operators still need to be consumed so the graphics state stays in sync.
            case "cs" or "CS":
                // Sets colour space: operand is a name. We track nothing complex here;
                // subsequent sc/SC calls will carry the actual channel values.
                break;

            case "sc" or "SC" when op.Operands.Count == 1:
            {
                // Single operand → DeviceGray
                var v = Num(op, 0);
                if (op.Name == "sc") SetFillGray(v); else SetStrokeGray(v);
                break;
            }
            case "sc" or "SC" when op.Operands.Count == 3:
            {
                var (r2, g2, b2) = (Num(op, 0), Num(op, 1), Num(op, 2));
                if (op.Name == "sc") SetFillRgb(r2, g2, b2); else SetStrokeRgb(r2, g2, b2);
                break;
            }
            case "sc" or "SC" when op.Operands.Count == 4:
            {
                var (r2, g2, b2) = CmykToRgb(Num(op, 0), Num(op, 1), Num(op, 2), Num(op, 3));
                if (op.Name == "sc") SetFillRgb(r2, g2, b2); else SetStrokeRgb(r2, g2, b2);
                break;
            }

            case "scn" or "SCN" when op.Operands.Count >= 1:
            {
                // scn/SCN can have a trailing name for pattern/ICC; ignore name operands.
                var nums = op.Operands.Where(static o => o is PdfInteger or PdfReal).ToList();
                switch (nums.Count)
                {
                    case 1:
                    {
                        var v = NumObj(nums[0]);
                        if (op.Name == "scn") SetFillGray(v); else SetStrokeGray(v);
                        break;
                    }
                    case 3:
                    {
                        var (r2, g2, b2) = (NumObj(nums[0]), NumObj(nums[1]), NumObj(nums[2]));
                        if (op.Name == "scn") SetFillRgb(r2, g2, b2); else SetStrokeRgb(r2, g2, b2);
                        break;
                    }
                    case 4:
                    {
                        var (r2, g2, b2) = CmykToRgb(NumObj(nums[0]), NumObj(nums[1]), NumObj(nums[2]), NumObj(nums[3]));
                        if (op.Name == "scn") SetFillRgb(r2, g2, b2); else SetStrokeRgb(r2, g2, b2);
                        break;
                    }
                }
                break;
            }

            // ── Misc graphics state ───────────────────────────────────────────
            case "w" when op.Operands.Count >= 1: _gs.LineWidth = Num(op, 0); break;
            case "J" or "j" or "M" or "d" or "ri" or "i" or "gs": break; // consume; not rendered

            // ── Path construction ─────────────────────────────────────────────
            case "m" when op.Operands.Count >= 2: PathMoveTo(Num(op, 0), Num(op, 1)); break;
            case "l" when op.Operands.Count >= 2: PathLineTo(Num(op, 0), Num(op, 1)); break;
            case "c" when op.Operands.Count >= 6:
                PathCurveTo(Num(op, 0), Num(op, 1), Num(op, 2), Num(op, 3), Num(op, 4), Num(op, 5));
                break;
            case "v" when op.Operands.Count >= 4:
                PathCurveTo(_currentPoint.X, _currentPoint.Y, Num(op, 0), Num(op, 1), Num(op, 2), Num(op, 3));
                break;
            case "y" when op.Operands.Count >= 4:
                PathCurveTo(Num(op, 0), Num(op, 1), Num(op, 2), Num(op, 3), Num(op, 2), Num(op, 3));
                break;
            case "h":
            {
                if (_inPath)
                {
                    _currentPath.Add(_currentPoint);
                    _currentPath.Add(_pathStart);
                    _currentPoint = _pathStart;
                }
                break;
            }
            case "re" when op.Operands.Count >= 4:
                PathRect(Num(op, 0), Num(op, 1), Num(op, 2), Num(op, 3));
                break;

            // ── Path painting ─────────────────────────────────────────────────
            case "S": DrawStroke(); ClearPath(); break;
            case "s":
                _currentPath.Add(_currentPoint);
                _currentPath.Add(_pathStart);
                DrawStroke();
                ClearPath();
                break;
            case "f" or "F" or "f*": DrawFill(); ClearPath(); break;
            case "B" or "B*": DrawFill(); DrawStroke(); ClearPath(); break;
            case "b" or "b*":
                _currentPath.Add(_currentPoint);
                _currentPath.Add(_pathStart);
                DrawFill();
                DrawStroke();
                ClearPath();
                break;
            case "n": ClearPath(); break;

            // ── Clip (consume; clipping not implemented yet) ──────────────────
            case "W" or "W*": ClearPath(); break;

            // ── Marked content (consume) ──────────────────────────────────────
            case "BMC" or "BDC" or "EMC" or "MP" or "DP": break;

            // ── Text object ───────────────────────────────────────────────────
            case "BT":
                _gs.TextMatrix = [1, 0, 0, 1, 0, 0];
                _gs.TextLineMatrix = [1, 0, 0, 1, 0, 0];
                break;
            case "ET": break;

            // ── Text state ────────────────────────────────────────────────────
            case "Tf" when op.Operands.Count >= 2:
            {
                var resName = (op.Operands[0] as PdfName)?.Value ?? string.Empty;
                _gs.FontResourceName = resName;
                _gs.FontName = fontMap.GetValueOrDefault(resName, resName);
                _gs.FontSize = Num(op, 1);
                break;
            }
            case "Tc" when op.Operands.Count >= 1: _gs.CharSpace = Num(op, 0); break;
            case "Tw" when op.Operands.Count >= 1: _gs.WordSpace = Num(op, 0); break;
            case "Tz" when op.Operands.Count >= 1: _gs.HorizontalScale = Num(op, 0); break;
            case "TL" when op.Operands.Count >= 1: _gs.Leading = Num(op, 0); break;
            case "Tr" when op.Operands.Count >= 1: _gs.TextRenderMode = (int)Num(op, 0); break;
            case "Ts": break; // text rise — not yet applied

            // ── Text positioning ──────────────────────────────────────────────
            case "Tm" when op.Operands.Count >= 6:
                _gs.TextMatrix = [Num(op, 0), Num(op, 1), Num(op, 2), Num(op, 3), Num(op, 4), Num(op, 5)];
                _gs.TextLineMatrix = (double[])_gs.TextMatrix.Clone();
                break;
            case "Td" when op.Operands.Count >= 2:
                MoveTextLine(Num(op, 0), Num(op, 1));
                break;
            case "TD" when op.Operands.Count >= 2:
                _gs.Leading = -Num(op, 1);
                MoveTextLine(Num(op, 0), Num(op, 1));
                break;
            case "T*": MoveTextLine(0, -_gs.Leading); break;

            // ── Text showing ──────────────────────────────────────────────────
            case "Tj" when op.Operands.Count >= 1:
                if (op.Operands[0] is PdfString tj) ShowString(tj.Bytes.Span);
                break;
            case "'":
                MoveTextLine(0, -_gs.Leading);
                if (op.Operands is [PdfString sq, ..]) ShowString(sq.Bytes.Span);
                break;
            case "\"" when op.Operands.Count >= 3:
                _gs.WordSpace = Num(op, 0);
                _gs.CharSpace = Num(op, 1);
                MoveTextLine(0, -_gs.Leading);
                if (op.Operands[2] is PdfString sdq) ShowString(sdq.Bytes.Span);
                break;
            case "TJ" when op.Operands.Count >= 1:
                if (op.Operands[0] is PdfArray arr) ShowArray(arr);
                break;

            // ── XObject ───────────────────────────────────────────────────────
            case "Do" when op.Operands.Count >= 1:
                if (op.Operands[0] is PdfName xName) PaintXObject(xName.Value);
                break;

            // ── Inline image — decoded at parse time into PdfInlineImage ─────
            case "BI" when op.Operands is [PdfInlineImage inlineImg, ..]:
                PaintInlineImage(inlineImg);
                break;
            case "BI": break; // parser produced no image (unsupported format)

            // ── Shading / form XObjects (stubs; no rendering) ─────────────────
            case "sh": break;
        }
    }

    // ── Text rendering ────────────────────────────────────────────────────────

    private void MoveTextLine(double tx, double ty)
    {
        var newE = (tx * _gs.TextLineMatrix[0]) + (ty * _gs.TextLineMatrix[2]) + _gs.TextLineMatrix[4];
        var newF = (tx * _gs.TextLineMatrix[1]) + (ty * _gs.TextLineMatrix[3]) + _gs.TextLineMatrix[5];
        _gs.TextLineMatrix[4] = newE;
        _gs.TextLineMatrix[5] = newF;
        _gs.TextMatrix = (double[])_gs.TextLineMatrix.Clone();
    }

    private void ShowString(ReadOnlySpan<byte> bytes)
    {
        // Text rendering mode 3 = invisible; do not draw.
        if (_gs.TextRenderMode == 3) return;
        if (_gs.FontSize <= 0 || _gs.FontName.Length == 0 || bytes.IsEmpty) return;

        // Look up embedded font bytes by resource name first (the dict is keyed by
        // resource name like "F1", not by base font name like "Helvetica").
        byte[]? embeddedBytes = null;
        if (embeddedFontBytes is not null)
        {
            if (!embeddedFontBytes.TryGetValue(_gs.FontResourceName, out embeddedBytes) || embeddedBytes is { Length: 0 })
                embeddedFontBytes.TryGetValue(_gs.FontName, out embeddedBytes);
        }

        var (ftFace, hbFont) = fonts.GetFonts(_gs.FontName, embeddedBytes);
        var pixelSize = (uint)Math.Max(1, Math.Round(_gs.FontSize * scale));
        ftFace.SetPixelSizes(0, pixelSize);

        var hbScale = (int)(pixelSize * 64);
        hbFont.SetScale(hbScale, hbScale);

        // Interpret PDF string bytes as Latin-1 (the most common PDF string encoding),
        // convert to Unicode codepoints so HarfBuzz can map them correctly to glyphs
        // in the TrueType substitute font.  For embedded CID fonts the mapping may still
        // be approximate, but for Standard 14 + Latin-1 this is correct.
        var unicodeText = System.Text.Encoding.Latin1.GetString(bytes.ToArray());

        using var hbBuffer = new HarfBuzzSharp.Buffer();
        hbBuffer.AddUtf8(unicodeText);
        hbBuffer.GuessSegmentProperties();
        hbFont.Shape(hbBuffer);

        var glyphInfos    = hbBuffer.GlyphInfos;
        var glyphPositions = hbBuffer.GlyphPositions;

        for (var i = 0; i < glyphInfos.Length; i++)
        {
            var glyphId = glyphInfos[i].Codepoint;

            // ReSharper disable once EmptyGeneralCatchClause
            try { ftFace.LoadGlyph(glyphId, LoadFlags.Render, LoadTarget.Normal); }
            catch { GlyphsSkipped++; continue; }

            GlyphsAttempted++;

            // HarfBuzz XOffset/YOffset are in 26.6 pixel units; convert to user-space points.
            var originX = _gs.TextMatrix[4] + (glyphPositions[i].XOffset / 64.0 / scale);
            var originY = _gs.TextMatrix[5] + (glyphPositions[i].YOffset / 64.0 / scale);
            var (px, py) = UToPixel(originX, originY);

            // Use BlitGlyphFromFace so we can read BitmapLeft/BitmapTop and the bitmap
            // itself directly from the FT_GlyphSlotRec at correct native struct offsets.
            // SharpFont's face->glyph offset is wrong on Windows x64 (NativeLong mismatch).
            buffer.BlitGlyphFromFace((int)px, (int)py, ftFace, _gs.FillR, _gs.FillG, _gs.FillB);

            // Advance in 26.6 px → convert to user-space points.
            var advance = ((glyphPositions[i].XAdvance / 64.0 / scale) + _gs.CharSpace)
                          * (_gs.HorizontalScale / 100.0);
            _gs.TextMatrix[4] += advance;
        }
    }

    private void ShowArray(PdfArray arr)
    {
        foreach (var elem in arr.Elements)
        {
            switch (elem)
            {
                case PdfString s:
                    ShowString(s.Bytes.Span);
                    break;
                case PdfInteger n:
                    _gs.TextMatrix[4] -= n.Value / 1000.0 * _gs.FontSize * (_gs.HorizontalScale / 100.0);
                    break;
                case PdfReal r:
                    _gs.TextMatrix[4] -= r.Value / 1000.0 * _gs.FontSize * (_gs.HorizontalScale / 100.0);
                    break;
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
        _currentPath.Add(_currentPoint);
        _currentPath.Add(_pathStart);
        _currentPoint = _pathStart;
    }

    private void PathCurveTo(double x1, double y1, double x2, double y2, double x3, double y3)
    {
        var p0 = _currentPoint;
        for (var t = 1; t <= 8; t++)
        {
            var s  = t / 8.0;
            var u  = 1 - s;
            var bx = (u * u * u * p0.X) + (3 * u * u * s * x1) + (3 * u * s * s * x2) + (s * s * s * x3);
            var by = (u * u * u * p0.Y) + (3 * u * u * s * y1) + (3 * u * s * s * y2) + (s * s * s * y3);
            _currentPath.Add(_currentPoint);
            _currentPath.Add((bx, by));
            _currentPoint = (bx, by);
        }
    }

    private void DrawFill()
    {
        if (!IsRectanglePath()) return;

        var minX = _currentPath.Min(static p => p.X);
        var minY = _currentPath.Min(static p => p.Y);
        var maxX = _currentPath.Max(static p => p.X);
        var maxY = _currentPath.Max(static p => p.Y);
        var (px1, py1) = UToPixel(minX, maxY);
        var (px2, py2) = UToPixel(maxX, minY);
        buffer.FillRect(
            (int)px1, (int)py1,
            (int)(px2 - px1 + 1), (int)(py2 - py1 + 1),
            _gs.FillR, _gs.FillG, _gs.FillB);
    }

    private void DrawStroke()
    {
        var thickPx = Math.Max(1, (int)Math.Round(_gs.LineWidth * scale));
        for (var i = 0; i + 1 < _currentPath.Count; i += 2)
        {
            var (x0, y0) = UToPixel(_currentPath[i].X, _currentPath[i].Y);
            var (x1, y1) = UToPixel(_currentPath[i + 1].X, _currentPath[i + 1].Y);
            buffer.DrawLine((int)x0, (int)y0, (int)x1, (int)y1,
                _gs.StrokeR, _gs.StrokeG, _gs.StrokeB, thickPx);
        }
    }

    private void ClearPath()
    {
        _currentPath.Clear();
        _inPath = false;
    }

    private bool IsRectanglePath()
    {
        if (_currentPath.Count < 4) return false;
        var xs = _currentPath.Select(static p => p.X).Distinct().Count();
        var ys = _currentPath.Select(static p => p.Y).Distinct().Count();
        return xs == 2 && ys == 2;
    }

    // ── XObject / image ───────────────────────────────────────────────────────

    private void PaintInlineImage(PdfInlineImage img)
    {
        // Inline images (BI…EI) fill the rectangle [0, 0, UserWidth, UserHeight]
        // in user space, unlike XObject images (Do) which fill the unit square.
        // The current CTM then maps this user-space rect to pixel space.
        var uw = img.UserWidth;
        var uh = img.UserHeight;

        var (x0, y0) = UToPixel(0,  0);
        var (x1, y1) = UToPixel(uw, uh);

        var dstX = (int)Math.Min(x0, x1);
        var dstY = (int)Math.Min(y0, y1);
        var dstW = (int)Math.Abs(x1 - x0);
        var dstH = (int)Math.Abs(y1 - y0);

        if (dstW <= 0 || dstH <= 0) return;

        for (var py = 0; py < dstH; py++)
        {
            var srcY = py * img.Height / dstH;
            for (var px = 0; px < dstW; px++)
            {
                var srcX   = px * img.Width / dstW;
                var srcOff = ((srcY * img.Width) + srcX) * 3;
                buffer.BlitImagePixel(
                    dstX + px, dstY + py,
                    img.RgbData[srcOff],
                    img.RgbData[srcOff + 1],
                    img.RgbData[srcOff + 2]);
            }
        }
    }

    private void PaintXObject(string resourceName)
    {
        if (imageXObjects is null) return;
        if (!imageXObjects.TryGetValue(resourceName, out var img)) return;

        // The Do operator places the image in the unit square [0,0]→[1,1] in user
        // space, transformed by the current CTM.
        var (x0, y0) = UToPixel(0, 0);
        var (x1, y1) = UToPixel(1, 1);

        var dstX = (int)Math.Min(x0, x1);
        var dstY = (int)Math.Min(y0, y1);
        var dstW = (int)Math.Abs(x1 - x0);
        var dstH = (int)Math.Abs(y1 - y0);

        if (dstW <= 0 || dstH <= 0) return;

        // Nearest-neighbour blit with scaling.
        for (var py = 0; py < dstH; py++)
        {
            var srcY = py * img.Height / dstH;
            for (var px = 0; px < dstW; px++)
            {
                var srcX   = px * img.Width / dstW;
                var srcOff = ((srcY * img.Width) + srcX) * 3;
                buffer.BlitImagePixel(
                    dstX + px,
                    dstY + py,
                    img.RgbData[srcOff],
                    img.RgbData[srcOff + 1],
                    img.RgbData[srcOff + 2]);
            }
        }
    }

    // ── Coordinate helpers ────────────────────────────────────────────────────

    private (double Px, double Py) UToPixel(double x, double y)
    {
        var (ux, uy) = _gs.Transform(x, y);
        return (ux * scale, (pageHeightPt - uy) * scale);
    }

    private static double Num(ContentOperator op, int i) => op.Operands[i] switch
    {
        PdfInteger n => n.Value,
        PdfReal r    => r.Value,
        _            => 0
    };

    private static double NumObj(PdfObject obj) => obj switch
    {
        PdfInteger n => n.Value,
        PdfReal r    => r.Value,
        _            => 0
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
