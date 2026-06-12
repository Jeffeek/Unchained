using System.Text;
using Unchained.Drawing;
using Unchained.Drawing.Text;
using Unchained.Drawing.Text.Extensions;
using Unchained.Pdf.Core;
using Unchained.Pdf.Engine;
using Unchained.Pdf.Models;
using Buffer = HarfBuzzSharp.Buffer;
using Encoding = System.Text.Encoding;

namespace Unchained.Pdf.Rendering.Rendering;

/// <summary>
///     Walks a list of <see cref="ContentOperator" /> records and rasterizes them
///     into a <see cref="RasterBuffer" /> using the PDF graphics model (ISO 32000-1 §8–9).
/// </summary>
internal sealed class PageRenderer(
    RasterBuffer buffer,
    FontCache fonts,
    double scale,
    double pageHeightPt,
    IReadOnlyDictionary<string, byte[]?>? embeddedFontBytes = null,
    IReadOnlyDictionary<string, ImageXObject>? imageXObjects = null,
    double[]? initialCtm = null,
    IReadOnlyDictionary<string, IReadOnlyDictionary<uint, string>>? toUnicodeMaps = null,
    IReadOnlyDictionary<string, CompositeFontInfo>? compositeFonts = null,
    IReadOnlyDictionary<string, (double Fill, double Stroke, string BlendMode, string? SoftMaskName)>? extGStateAlphas = null,
    IReadOnlyDictionary<string, ShadingInfo>? shadings = null,
    IReadOnlyDictionary<string, TilingPatternInfo>? tilingPatterns = null,
    IReadOnlyDictionary<string, SoftMaskInfo>? softMasks = null,
    IReadOnlyDictionary<string, ColorSpaceInfo>? colorSpaces = null,
    IReadOnlyDictionary<string, Type3FontInfo>? type3Fonts = null
)
{
    private static readonly IReadOnlyDictionary<string, string> EmptyFontMap = new Dictionary<string, string>();

    private readonly Stack<GraphicsState> _gsStack = new();
    // Current path as a list of subpaths, each a polyline of user-space points. A new `m`
    // (or `re`) starts a new subpath; `l`/`c`/`v`/`y` append to the current one. This
    // preserves multiple subpaths (needed for polygon fills with holes and for stroking
    // disjoint figures) — the previous segment-pair model kept only the last subpath.
    private readonly List<List<(double X, double Y)>> _subpaths = [];
    private (double X, double Y) _currentPoint;
    private List<(double X, double Y)>? _curSub;
    // Apply the initial CTM from the renderer (encodes page rotation + coordinate origin).
    private GraphicsState _gs = new()
    {
        Ctm = initialCtm ?? [1, 0, 0, 1, 0, 0]
    };
    private bool _inPath;
    private (double X, double Y) _pathStart;
    // Set by W/W*; the clip is applied when the current path is next cleared.
    // _pendingClipEvenOdd distinguishes W* (even-odd) from W (nonzero winding).
    private bool _pendingClip;
    private bool _pendingClipEvenOdd;
    // Nesting depth for tiling-pattern cell rendering; bounds pattern-in-pattern recursion.
    private int _tilingDepth;

    // Count of text operators that produced no glyphs due to font-loading errors.
    internal int TextErrorCount { get; private set; }

    // Total glyph bitmaps successfully passed to BlitGlyphBitmap (glyph not skipped by catch).
    internal int GlyphsAttempted { get; private set; }

    // Total glyph bitmaps whose LoadGlyph failed (inner catch { continue; }).
    internal int GlyphsSkipped { get; private set; }

    // Whether a soft mask is active — if so, fills must apply per-pixel alpha modulation.
    private bool HasSoftMask => _gs.SoftMask is not null;

    internal void Render(
        IEnumerable<ContentOperator> operators,
        IReadOnlyDictionary<string, string> fontMap
    )
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
            case "q":
            {
                // Save the current clip mask into the graphics state snapshot before pushing.
                _gs.SavedClipMask = buffer.SaveClipMask();
                _gsStack.Push(_gs.Clone());
                break;
            }
            case "Q":
            {
                if (_gsStack.Count > 0) _gs = _gsStack.Pop();
                // Restore the buffer clip to match the restored graphics state.
                SyncClip();
                break;
            }
            case "cm" when op.Operands.Count >= 6:
            {
                double[] m = [Num(op, 0), Num(op, 1), Num(op, 2), Num(op, 3), Num(op, 4), Num(op, 5)];
                // The cm operator pre-concatenates: CTM_new = m × CTM_old (ISO 32000-1
                // §8.3.4). The new matrix transforms points first, then the existing CTM.
                // Using the reverse order corrupts translation whenever the CTM is not the
                // identity (nested cm, or a rotated/cropped base CTM).
                _gs.Ctm = GraphicsState.MultiplyMatrix(m, _gs.Ctm);
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
            case "cs" when op.Operands.Count >= 1:
                _gs.FillColorSpace = (op.Operands[0] as PdfName)?.Value ?? RenderingConstants.DeviceGray;
            break;
            case "CS" when op.Operands.Count >= 1:
                _gs.StrokeColorSpace = (op.Operands[0] as PdfName)?.Value ?? RenderingConstants.DeviceGray;
            break;
            case "cs" or "CS": break; // no operand — consume

            case "sc" or "SC" when op.Operands.Count >= 1:
            {
                var nums = op.Operands.Where(static o => o is PdfInteger or PdfReal)
                    .Select(NumObj).ToArray();
                var csName = op.Name == "sc" ? _gs.FillColorSpace : _gs.StrokeColorSpace;
                var (r2, g2, b2) = ResolveColorComponents(nums, csName);
                if (op.Name == "sc") SetFillRgb(r2, g2, b2);
                else SetStrokeRgb(r2, g2, b2);
                break;
            }

            case "scn" or "SCN" when op.Operands.Count >= 1:
            {
                // scn/SCN may carry a trailing name operand naming a tiling/shading pattern.
                // We don't render patterns; flag fills so DrawFill can skip them rather than
                // painting a solid block (which appears as a large wrong dark area).
                var isPattern = op.Operands.Any(static o => o is PdfName);

                var nums = op.Operands.Where(static o => o is PdfInteger or PdfReal).ToList();
                switch (nums.Count)
                {
                    case > 0 when !isPattern:
                    {
                        var csName = op.Name == "scn" ? _gs.FillColorSpace : _gs.StrokeColorSpace;
                        var components = nums.Select(NumObj).ToArray();
                        var (r2, g2, b2) = ResolveColorComponents(components, csName);
                        if (op.Name == "scn") SetFillRgb(r2, g2, b2);
                        else SetStrokeRgb(r2, g2, b2);
                        break;
                    }
                    case > 0:
                        // Pattern with color components — fall back to heuristic.
                        switch (nums.Count)
                        {
                            case 1:
                            {
                                var v = NumObj(nums[0]);
                                if (op.Name == "scn") SetFillGray(v);
                                else SetStrokeGray(v);
                                break;
                            }
                            case 3:
                            {
                                var (r2, g2, b2) = (NumObj(nums[0]), NumObj(nums[1]), NumObj(nums[2]));
                                if (op.Name == "scn") SetFillRgb(r2, g2, b2);
                                else SetStrokeRgb(r2, g2, b2);
                                break;
                            }
                            case 4:
                            {
                                var (r2, g2, b2) = CmykToRgb(NumObj(nums[0]), NumObj(nums[1]), NumObj(nums[2]), NumObj(nums[3]));
                                if (op.Name == "scn") SetFillRgb(r2, g2, b2);
                                else SetStrokeRgb(r2, g2, b2);
                                break;
                            }
                        }

                    break;
                }

                // Set after the numeric setters above (which clear the flag).
                if (op.Name == "scn")
                {
                    _gs.FillIsPattern = isPattern;
                    // If the named pattern is a known axial/radial shading or tiling pattern,
                    // remember it so DrawFill renders it rather than the grey approximation.
                    var patName = op.Operands.OfType<PdfName>().LastOrDefault()?.Value;
                    _gs.FillShadingName = patName is not null && shadings is not null && shadings.ContainsKey(patName)
                        ? patName
                        : null;
                    _gs.FillTilingName = patName is not null && tilingPatterns is not null && tilingPatterns.ContainsKey(patName)
                        ? patName
                        : null;
                }

                break;
            }

            // ── Misc graphics state ───────────────────────────────────────────
            case "w" when op.Operands.Count >= 1: _gs.LineWidth = Num(op, 0); break;
            case "d" when op.Operands.Count >= 1:
            {
                // d [dashArray] dashPhase — store the on/off lengths (phase ignored).
                _gs.DashLengths = op.Operands[0] is PdfArray da
                    ? da.Elements.Select(NumObj).Where(static v => v >= 0).ToArray()
                    : [];
                break;
            }
            case "J" when op.Operands.Count >= 1: _gs.LineCap = (int)Num(op, 0); break;
            case "j" when op.Operands.Count >= 1: _gs.LineJoin = (int)Num(op, 0); break;
            case "M" when op.Operands.Count >= 1: _gs.MiterLimit = Num(op, 0); break;
            case "J" or "j" or "M" or "ri" or "i": break; // consume; not rendered
            case "gs" when op.Operands.Count >= 1:
            {
                // Apply the named /ExtGState's constant alpha (/ca fill, /CA stroke),
                // blend mode (/BM), and soft mask (/SMask).
                var name = (op.Operands[0] as PdfName)?.Value;
                if (name is not null && extGStateAlphas is not null
                                     && extGStateAlphas.TryGetValue(name, out var a))
                {
                    _gs.FillA = (byte)Math.Clamp((int)Math.Round(a.Fill * RenderingConstants.ByteMax), 0, RenderingConstants.ByteMax);
                    _gs.StrokeA = (byte)Math.Clamp((int)Math.Round(a.Stroke * RenderingConstants.ByteMax), 0, RenderingConstants.ByteMax);
                    _gs.BlendMode = a.BlendMode;
                    // Activate soft mask if present.
                    if (a.SoftMaskName is { } smName && softMasks is not null
                                                     && softMasks.TryGetValue(smName, out var smInfo))
                    {
                        _gs.SoftMask = RenderSoftMask(smInfo);
                        _gs.SoftMaskWidth = smInfo.WidthPx;
                        _gs.SoftMaskHeight = smInfo.HeightPx;
                    }
                    else
                        _gs.SoftMask = null;
                }

                break;
            }
            case "gs": break;

            // ── Path construction ─────────────────────────────────────────────
            case "m" when op.Operands.Count >= 2: PathMoveTo(Num(op, 0), Num(op, 1)); break;
            case "l" when op.Operands.Count >= 2: PathLineTo(Num(op, 0), Num(op, 1)); break;
            case "c" when op.Operands.Count >= 6:
                PathCurveTo(Num(op, 0),
                    Num(op, 1),
                    Num(op, 2),
                    Num(op, 3),
                    Num(op, 4),
                    Num(op, 5));
            break;
            case "v" when op.Operands.Count >= 4:
                PathCurveTo(_currentPoint.X,
                    _currentPoint.Y,
                    Num(op, 0),
                    Num(op, 1),
                    Num(op, 2),
                    Num(op, 3));
            break;
            case "y" when op.Operands.Count >= 4:
                PathCurveTo(Num(op, 0),
                    Num(op, 1),
                    Num(op, 2),
                    Num(op, 3),
                    Num(op, 2),
                    Num(op, 3));
            break;
            case "h":
            {
                PathClose();
                break;
            }
            case "re" when op.Operands.Count >= 4:
                PathRect(Num(op, 0), Num(op, 1), Num(op, 2), Num(op, 3));
            break;

            // ── Path painting ─────────────────────────────────────────────────
            case "S":
                DrawStroke();
                ClearPath();
            break;
            case "s":
                PathClose();
                DrawStroke();
                ClearPath();
            break;
            case "f" or "F":
                DrawFill(false);
                ClearPath();
            break;
            case "f*":
                DrawFill(true);
                ClearPath();
            break;
            case "B":
                DrawFill(false);
                DrawStroke();
                ClearPath();
            break;
            case "B*":
                DrawFill(true);
                DrawStroke();
                ClearPath();
            break;
            case "b":
                PathClose();
                DrawFill(false);
                DrawStroke();
                ClearPath();
            break;
            case "b*":
                PathClose();
                DrawFill(true);
                DrawStroke();
                ClearPath();
            break;
            case "n": ClearPath(); break;


            // ── Clip ──────────────────────────────────────────────────────────
            // W/W* set the clip to the current path; it takes effect AFTER the next
            // painting operator (ISO 32000-1 §8.5.4). We record a pending clip and apply it
            // when the path is cleared. W* uses the even-odd rule; W uses nonzero winding.
            case "W":
                _pendingClip = true;
                _pendingClipEvenOdd = false;
            break;
            case "W*":
                _pendingClip = true;
                _pendingClipEvenOdd = true;
            break;

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
            case "Ts" when op.Operands.Count >= 1: _gs.TextRise = Num(op, 0); break;

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
                if (op.Operands[0] is PdfString tj) ShowString(tj.GetBinaryBytes().Span);
            break;
            case "'":
                MoveTextLine(0, -_gs.Leading);
                if (op.Operands is [PdfString sq, ..]) ShowString(sq.GetBinaryBytes().Span);
            break;
            case "\"" when op.Operands.Count >= 3:
                _gs.WordSpace = Num(op, 0);
                _gs.CharSpace = Num(op, 1);
                MoveTextLine(0, -_gs.Leading);
                if (op.Operands[2] is PdfString sdq) ShowString(sdq.GetBinaryBytes().Span);
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

            // ── Shading (sh) — paints an axial/radial gradient over the current clip ──
            case "sh" when op.Operands.Count >= 1:
            {
                var name = (op.Operands[0] as PdfName)?.Value;
                if (name is not null && shadings is not null && shadings.TryGetValue(name, out var sh))
                    PaintShadingInClip(sh);
                break;
            }
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

    // Vertical scale magnitude of the text matrix combined with the CTM. Used to size
    // glyph rasterization when producers carry the visual size in the matrix rather than
    // in FontSize. Returns 1 for the common unit-scale case.
    private double TextMatrixVerticalScale()
    {
        // Combined vertical basis vector = TextMatrix · CTM applied to (0,1).
        var tb = _gs.TextMatrix[1];
        var td = _gs.TextMatrix[3];
        // Map the text-space vertical direction through the CTM's linear part.
        var cb = _gs.Ctm[1];
        var cd = _gs.Ctm[3];
        var ca = _gs.Ctm[0];
        var cc = _gs.Ctm[2];
        var vx = (tb * ca) + (td * cc);
        var vy = (tb * cb) + (td * cd);
        var mag = Vector2D.Magnitude(vx, vy);
        return mag > RenderingConstants.Epsilon ? mag : 1.0;
    }

    // Horizontal scale magnitude of the text matrix combined with the CTM's linear part.
    // Text advances are computed in glyph space and must be scaled by this to land in the
    // pre-CTM position space that UToPixel consumes. Returns 1 for unit-scale matrices.
    private double TextMatrixHorizontalScale()
    {
        var ta = _gs.TextMatrix[0];
        var tc = _gs.TextMatrix[2];
        // Horizontal text-space basis (1,0) through the text matrix linear part.
        var mag = Vector2D.Magnitude(ta, tc);
        return mag > RenderingConstants.Epsilon ? mag : 1.0;
    }

    private void ShowString(ReadOnlySpan<byte> bytes)
    {
        // Text rendering mode 3 = invisible; do not draw.
        if (_gs.TextRenderMode == RenderingConstants.TextModeInvisible) return;
        if (_gs.FontSize <= 0 || _gs.FontName.Length == 0 || bytes.IsEmpty) return;

        // Type3 font: glyphs are content streams, not binary font files.
        if (type3Fonts is not null && type3Fonts.TryGetValue(_gs.FontResourceName, out var t3))
        {
            ShowStringType3(bytes, t3);
            return;
        }

        // Look up embedded font bytes by resource name first (the dict is keyed by
        // resource name like "F1", not by base font name like "Helvetica").
        byte[]? embeddedBytes = null;
        if (embeddedFontBytes is not null)
        {
            if (!embeddedFontBytes.TryGetValue(_gs.FontResourceName, out embeddedBytes) || embeddedBytes is { Length: 0 })
                embeddedFontBytes.TryGetValue(_gs.FontName, out embeddedBytes);
        }

        var (ftFace, hbFont) = fonts.GetFonts(_gs.FontName, embeddedBytes);

        // Effective glyph size in device pixels. The on-page text size is FontSize scaled
        // by the vertical magnitude of the text matrix combined with the CTM, then by the
        // device scale (DPI/72). Some producers set FontSize to 1 and carry the real size
        // in the text/transformation matrix (e.g. `Tf /F 1` with `Tm 16 0 0 -16 …`), so
        // FontSize alone is not the rasterization size. When the matrices have unit scale
        // (the common case) this reduces exactly to FontSize * scale.
        var textVScale = TextMatrixVerticalScale();
        var pixelSize = (uint)Math.Max(1, Math.Round(_gs.FontSize * textVScale * scale));
        ftFace.SetPixelSize(pixelSize);

        var hbScale = (int)(pixelSize * 64);
        hbFont.SetScale(hbScale, hbScale);

        // Composite (Type0) fonts with an Identity encoding: each pair of bytes is a
        // big-endian code that equals the CID, which maps to a glyph index directly
        // (Identity /CIDToGIDMap) or via an explicit map. The PDF already holds the
        // final shaped glyph sequence, so we must NOT re-shape through HarfBuzz or
        // remap via /ToUnicode — that produces wrong glyphs and positions.
        CompositeFontInfo? composite = null;
        compositeFonts?.TryGetValue(_gs.FontResourceName, out composite);
        if (composite is { IdentityEncoding: true } && embeddedBytes is { Length: > 0 })
        {
            ShowStringComposite(bytes, ftFace, composite);
            return;
        }

        // Map char codes to Unicode. When the font has a /ToUnicode CMap, use it to
        // decode each char code to the correct Unicode string. Otherwise fall back to
        // Latin-1 (covers Standard 14 and most WinAnsi-encoded fonts correctly).
        IReadOnlyDictionary<uint, string>? toUnicodeMap = null;
        toUnicodeMaps?.TryGetValue(_gs.FontResourceName, out toUnicodeMap);
        if (toUnicodeMap is null)
            toUnicodeMaps?.TryGetValue(_gs.FontName, out toUnicodeMap);

        string unicodeText;
        if (toUnicodeMap is { Count: > 0 })
        {
            var sb = new StringBuilder();
            var span = bytes;
            while (!span.IsEmpty)
            {
                // Try 2-byte code first, then 1-byte
                var code2 = span.Length >= 2 ? (uint)((span[0] << 8) | span[1]) : 0;
                uint code1 = span[0];
                if (span.Length >= 2 && toUnicodeMap.TryGetValue(code2, out var u2))
                {
                    sb.Append(u2);
                    span = span[2..];
                }
                else if (toUnicodeMap.TryGetValue(code1, out var u1))
                {
                    sb.Append(u1);
                    span = span[1..];
                }
                else
                {
                    // Code not in ToUnicode — fall back to Latin-1 char so HarfBuzz shapes it;
                    // U+FFFD causes some fonts to return non-.notdef, suppressing ShowStringDirect.
                    sb.Append((char)code1);
                    span = span[1..];
                }
            }

            unicodeText = sb.ToString();
        }
        else
            unicodeText = Encoding.Latin1.GetString(bytes.ToArray());

        using var hbBuffer = new Buffer();
        hbBuffer.AddUtf8(unicodeText);
        hbBuffer.GuessSegmentProperties();
        hbFont.Shape(hbBuffer);

        var glyphInfos = hbBuffer.GlyphInfos;
        var glyphPositions = hbBuffer.GlyphPositions;

        // HarfBuzz resolves glyphs through the font's Unicode cmap. Embedded subset
        // Type1 fonts (e.g. Computer Modern) have a custom/builtin encoding and no
        // Unicode cmap, so HarfBuzz returns .notdef (glyph 0) for every character and
        // the text renders blank. Detect that case and fall back to resolving each raw
        // char code directly through FreeType's own charmap (FT_Get_Char_Index), which
        // honours the font's builtin encoding. Composite (Type0/CID) fonts and fonts
        // with a real Unicode cmap produce non-zero glyphs and keep the HarfBuzz path.
        var allNotDef = glyphInfos.Length > 0;
        if (glyphInfos.Any(static t => t.Codepoint != 0))
            allNotDef = false;

        if (allNotDef)
        {
            ShowStringDirect(bytes, ftFace, pixelSize);
            return;
        }

        for (var i = 0; i < glyphInfos.Length; i++)
        {
            var glyphId = glyphInfos[i].Codepoint;

            if (!ftFace.TryLoadGlyph(glyphId))
            {
                GlyphsSkipped++;
                continue;
            }

            GlyphsAttempted++;

            var originX = _gs.TextMatrix[4] + (glyphPositions[i].XOffset / RenderingConstants.HarfBuzzFixed / scale);
            var originY = _gs.TextMatrix[5] + (glyphPositions[i].YOffset / RenderingConstants.HarfBuzzFixed / scale) + _gs.TextRise;
            var (px, py) = UToPixel(originX, originY);

            // Mode 0 (fill) and 2/4/6 (fill variants): blit the bitmap.
            if (_gs.ShouldFillText)
            {
                buffer.BlitGlyphFromFace((int)px,
                    (int)py,
                    ftFace,
                    _gs.FillR,
                    _gs.FillG,
                    _gs.FillB,
                    _gs.BlendMode);
            }

            // Mode 1/2/5/6 (stroke variants): stroke the glyph outline.
            if (_gs.ShouldStrokeText)
                StrokeGlyphOutline(ftFace, (int)px, (int)py);

            // Mode 4/5/6/7 (clip variants): add glyph outline to clip mask.
            if (_gs.ShouldClipText)
                ClipGlyphOutline(ftFace, (int)px, (int)py);

            var advance = ((glyphPositions[i].XAdvance / RenderingConstants.HarfBuzzFixed / scale) + _gs.CharSpace)
                          * (_gs.HorizontalScale / RenderingConstants.HorizontalScalePercent);
            _gs.TextMatrix[4] += advance;
        }
    }

    // Fallback path for simple fonts whose embedded program has no usable Unicode cmap
    // (e.g. subset Computer Modern Type1, or symbolic subset TrueType with /FirstChar 0).
    // Each raw single-byte char code is resolved to a glyph index through FreeType's own
    // charmap (FT_Get_Char_Index). When that yields .notdef, the font is a glyph-indexed
    // subset where the char code IS the glyph index, so we fall back to loading the code
    // directly. Advances come from FreeType (FT_Get_Advance).
    private void ShowStringDirect(ReadOnlySpan<byte> bytes, GlyphFace ftFace, uint pixelSize)
    {
        foreach (var code in bytes)
        {
            var glyphId = ftFace.GetCharIndex(code);

            // Symbolic subset with no cmap entry: treat the char code as a direct glyph
            // index (FreeType rejects out-of-range indices in LoadGlyph below).
            if (glyphId == 0 && code > 0)
                glyphId = code;

            if (glyphId != 0 && !ftFace.TryLoadGlyph(glyphId))
            {
                GlyphsSkipped++;
                glyphId = 0;
            }

            if (glyphId != 0)
            {
                GlyphsAttempted++;
                var (px, py) = UToPixel(_gs.TextMatrix[4], _gs.TextMatrix[5] + _gs.TextRise);
                if (_gs.TextRenderMode != 1)
                {
                    buffer.BlitGlyphFromFace((int)px,
                        (int)py,
                        ftFace,
                        _gs.FillR,
                        _gs.FillG,
                        _gs.FillB,
                        _gs.BlendMode);
                }

                if (_gs.TextRenderMode is 1 or 2)
                    StrokeGlyphOutline(ftFace, (int)px, (int)py);
            }

            // Glyph advance (FT_Get_Advance returns 16.16 fixed-point pixels) → user-space points.
            var rawAdvance = glyphId != 0 ? ftFace.GetAdvance(glyphId) : 0;
            var advancePts = rawAdvance != 0 ? rawAdvance / RenderingConstants.FreeTypeFixed / scale : pixelSize / scale * 0.5;

            var advance = (advancePts + _gs.CharSpace + (code == 32 ? _gs.WordSpace : 0))
                          * (_gs.HorizontalScale / RenderingConstants.HorizontalScalePercent);
            _gs.TextMatrix[4] += advance;
        }
    }

    // Renders a string set in a composite (Type0) font with an Identity encoding.
    // Each pair of bytes is a big-endian 16-bit code that equals the CID; the CID maps
    // to a glyph index (Identity /CIDToGIDMap, or an explicit map). Glyphs are loaded by
    // index directly through FreeType — no cmap/HarfBuzz shaping. Advances come from the
    // CIDFont /W array (glyph-space 1000-unit em), falling back to /DW.
    private void ShowStringComposite(ReadOnlySpan<byte> bytes, GlyphFace ftFace, CompositeFontInfo info)
    {
        for (var i = 0; i + 1 < bytes.Length; i += 2)
        {
            var cid = (bytes[i] << 8) | bytes[i + 1];

            var gid = (uint)cid;
            if (info is { IdentityCidToGid: false, CidToGid: not null })
                gid = info.CidToGid.TryGetValue(cid, out var mapped) ? (uint)mapped : 0;

            if (gid != 0 && !ftFace.TryLoadGlyph(gid))
            {
                GlyphsSkipped++;
                gid = 0;
            }

            if (gid != 0)
            {
                GlyphsAttempted++;
                var (px, py) = UToPixel(_gs.TextMatrix[4], _gs.TextMatrix[5] + _gs.TextRise);
                if (_gs.ShouldFillText)
                {
                    buffer.BlitGlyphFromFace((int)px,
                        (int)py,
                        ftFace,
                        _gs.FillR,
                        _gs.FillG,
                        _gs.FillB,
                        _gs.BlendMode);
                }

                if (_gs.ShouldStrokeText)
                    StrokeGlyphOutline(ftFace, (int)px, (int)py);

                if (_gs.ShouldClipText)
                    ClipGlyphOutline(ftFace, (int)px, (int)py);
            }

            // Advance from /W (glyph-space units, 1000 per em) → text-space, then scaled
            // by the text-matrix horizontal magnitude into the pen-position space that
            // UToPixel consumes (handles producers that carry size in the matrix).
            var wGlyph = info.Widths.TryGetValue(cid, out var w) ? w : info.DefaultWidth;
            var hScale = TextMatrixHorizontalScale();
            var advance = ((wGlyph / RenderingConstants.CidEmUnits * _gs.FontSize) + _gs.CharSpace) * hScale
                                                                             * (_gs.HorizontalScale / RenderingConstants.HorizontalScalePercent);
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
                    ShowString(s.GetBinaryBytes().Span);
                break;
                case PdfInteger n:
                    _gs.TextMatrix[4] -= n.Value / RenderingConstants.CidEmUnits * _gs.FontSize * (_gs.HorizontalScale / RenderingConstants.HorizontalScalePercent);
                break;
                case PdfReal r:
                    _gs.TextMatrix[4] -= r.Value / RenderingConstants.CidEmUnits * _gs.FontSize * (_gs.HorizontalScale / RenderingConstants.HorizontalScalePercent);
                break;
            }
        }
    }

    // ── Path rendering ────────────────────────────────────────────────────────

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
                FillRectSoftMasked((int)px1,
                    (int)py1,
                    (int)(px2 - px1 + 1),
                    (int)(py2 - py1 + 1),
                    fr,
                    fg,
                    fb,
                    _gs.FillA,
                    _gs.BlendMode);
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
                    _gs.BlendMode);
            }

            return;
        }

        FillPolygon(evenOdd, fr, fg, fb);
    }

    // Paints a shading clipped to the current path's device bounding box (used for a
    // shading-pattern fill, where the path defines the painted region).
    private void PaintShadingInPathBounds(ShadingInfo sh)
    {
        var (minX, minY, maxX, maxY) = PathDeviceBounds();
        if (maxX < minX) return;

        PaintShadingRect(sh, (int)Math.Floor(minX), (int)Math.Floor(minY), (int)Math.Ceiling(maxX), (int)Math.Ceiling(maxY));
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
        var cell = new PageRenderer(tile,
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
                type3Fonts)
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
        var m = _gs.Ctm;
        // Build device-from-userPathPoint via UToPixel for two basis points to derive inverse.
        // Simpler: map each device pixel centre to user space directly.
        if (!TryInvertDeviceToUser(out var inv)) return;

        for (var py = dy0; py <= dy1; py++)
        for (var px = dx0; px <= dx1; px++)
        {
            var (ux, uy) = ApplyInv(inv, px + 0.5, py + 0.5);
            if (!ShadingT(sh, ux, uy, out var t)) continue;

            var (r, g, b) = sh.ColorAt(t);
            if (_gs.FillA >= 255) buffer.BlitImagePixel(px, py, r, g, b);
            else
            {
                buffer.BlendPixel(px,
                    py,
                    r,
                    g,
                    b,
                    _gs.FillA,
                    _gs.BlendMode);
            }
        }

        _ = m;
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
                    buffer.BlendPixel(px,
                        py,
                        r,
                        g,
                        b,
                        _gs.FillA);
                }
            }
        }
    }

    // Computes parametric t∈[0,1] for a user-space point under the shading, applying the
    // extend flags. Returns false when the point is outside a non-extended shading.
    private static bool ShadingT(
        ShadingInfo sh,
        double x,
        double y,
        out double t
    )
    {
        t = 0;
        if (sh.ShadingType == 2)
        {
            // Axial: project (x,y) onto the axis (x0,y0)->(x1,y1).
            var x0 = sh.Coords[0];
            var y0 = sh.Coords[1];
            var x1 = sh.Coords[2];
            var y1 = sh.Coords[3];
            var dx = x1 - x0;
            var dy = y1 - y0;
            var len2 = (dx * dx) + (dy * dy);
            if (len2 < RenderingConstants.DeterminantEpsilon)
            {
                t = 0;
                return true;
            }

            t = (((x - x0) * dx) + ((y - y0) * dy)) / len2;
        }
        else
        {
            // Radial: t such that the point lies on the interpolated circle. Approximate by
            // normalised distance from centre 0 to centre 1 (handles the common concentric case).
            var cx0 = sh.Coords[0];
            var cy0 = sh.Coords[1];
            var r0 = sh.Coords[2];
            var cx1 = sh.Coords[3];
            var cy1 = sh.Coords[4];
            var r1 = sh.Coords[5];
            var d = Vector2D.Distance(x, y, cx1, cy1);
            var denom = r1 - r0;
            t = Math.Abs(denom) > RenderingConstants.DeterminantEpsilon ? (d - r0) / denom : r1 > RenderingConstants.DeterminantEpsilon ? d / r1 : 0;
            _ = cx0;
            _ = cy0;
        }

        if (t < 0)
        {
            if (!sh.ExtendStart) return false;

            t = 0;
        }

        if (!(t > 1))
            return true;

        if (!sh.ExtendEnd)
            return false;

        t = 1;

        return true;
    }

    // Inverse of the device→user transform used by UToPixel (CTM + Y-flip + scale).
    private bool TryInvertDeviceToUser(out double[] inv)
    {
        // Forward: user (x,y) → ctm → (ux,uy) → device (ux*scale, (H-uy)*scale).
        // Compose forward affine D = [a b c d e f] mapping user→device:
        var a = _gs.Ctm[0] * scale;
        var b = -_gs.Ctm[1] * scale;
        var cc = _gs.Ctm[2] * scale;
        var dd = -_gs.Ctm[3] * scale;
        var e = _gs.Ctm[4] * scale;
        var f = (pageHeightPt - _gs.Ctm[5]) * scale;
        var det = (a * dd) - (b * cc);
        if (Math.Abs(det) < RenderingConstants.MatrixInverseEpsilon)
        {
            inv = [];
            return false;
        }

        var id = 1.0 / det;
        // Inverse affine.
        inv =
        [
            dd * id, -b * id,
            -cc * id, a * id,
            ((cc * f) - (dd * e)) * id, ((b * e) - (a * f)) * id
        ];
        return true;
    }

    private static (double X, double Y) ApplyInv(IReadOnlyList<double> m, double px, double py) =>
        ((m[0] * px) + (m[2] * py) + m[4], (m[1] * px) + (m[3] * py) + m[5]);

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
                    var (bx, by) = pts[(i + 1) % n];             // implicit close
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
                    FillSpanSoftMasked(y,
                        xStart,
                        xEnd - 1,
                        fr,
                        fg,
                        fb,
                        _gs.FillA,
                        _gs.BlendMode);
                }
                else
                {
                    buffer.FillSpan(y,
                        xStart,
                        xEnd - 1,
                        fr,
                        fg,
                        fb,
                        _gs.FillA,
                        _gs.BlendMode);
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
                    buffer.DrawLine((int)x0,
                        (int)y0,
                        (int)x1,
                        (int)y1,
                        _gs.StrokeR,
                        _gs.StrokeG,
                        _gs.StrokeB,
                        thickPx,
                        _gs.StrokeA,
                        _gs.BlendMode);
                }
                else
                {
                    DrawDashedLine(x0,
                        y0,
                        x1,
                        y1,
                        thickPx,
                        dashPx);
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
                        half);
                }
            }

            // Line caps on open subpaths (cap = 1 round, 2 projecting square).
            if (_gs.LineCap == 0 || sub.Count < 2) continue;

            var capR = Math.Max(1, thickPx / 2);
            var (ax, ay) = UToPixel(sub[0].X, sub[0].Y);
            var (bx, by) = UToPixel(sub[^1].X, sub[^1].Y);
            if (_gs.LineCap == 1)
            {
                buffer.FillCircle((int)ax,
                    (int)ay,
                    capR,
                    _gs.StrokeR,
                    _gs.StrokeG,
                    _gs.StrokeB,
                    _gs.StrokeA,
                    _gs.BlendMode);
                buffer.FillCircle((int)bx,
                    (int)by,
                    capR,
                    _gs.StrokeR,
                    _gs.StrokeG,
                    _gs.StrokeB,
                    _gs.StrokeA,
                    _gs.BlendMode);
            }
            else
            {
                buffer.FillRect((int)ax - capR,
                    (int)ay - capR,
                    thickPx,
                    thickPx,
                    _gs.StrokeR,
                    _gs.StrokeG,
                    _gs.StrokeB,
                    _gs.StrokeA,
                    _gs.BlendMode);
                buffer.FillRect((int)bx - capR,
                    (int)by - capR,
                    thickPx,
                    thickPx,
                    _gs.StrokeR,
                    _gs.StrokeG,
                    _gs.StrokeB,
                    _gs.StrokeA,
                    _gs.BlendMode);
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
                buffer.FillCircle(bx,
                    by,
                    half,
                    _gs.StrokeR,
                    _gs.StrokeG,
                    _gs.StrokeB,
                    _gs.StrokeA,
                    _gs.BlendMode);
            break;

            case 2: // Bevel — fill triangle between the two outer corners and the vertex
            {
                var ox1 = (int)(bx + (nxIn * half));
                var oy1 = (int)(by + (nyIn * half));
                var ox2 = (int)(bx + (nxOut * half));
                var oy2 = (int)(by + (nyOut * half));
                buffer.FillTriangle(bx,
                    by,
                    ox1,
                    oy1,
                    ox2,
                    oy2,
                    _gs.StrokeR,
                    _gs.StrokeG,
                    _gs.StrokeB,
                    _gs.StrokeA,
                    _gs.BlendMode);
                // Also fill the inner side.
                var ix1 = (int)(bx - (nxIn * half));
                var iy1 = (int)(by - (nyIn * half));
                var ix2 = (int)(bx - (nxOut * half));
                var iy2 = (int)(by - (nyOut * half));
                buffer.FillTriangle(bx,
                    by,
                    ix1,
                    iy1,
                    ix2,
                    iy2,
                    _gs.StrokeR,
                    _gs.StrokeG,
                    _gs.StrokeB,
                    _gs.StrokeA,
                    _gs.BlendMode);
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
                buffer.FillTriangle((int)mx,
                    (int)my,
                    ox1,
                    oy1,
                    ox2,
                    oy2,
                    _gs.StrokeR,
                    _gs.StrokeG,
                    _gs.StrokeB,
                    _gs.StrokeA,
                    _gs.BlendMode);
                // Inner side.
                var ix1 = (int)(bx - (nxIn * half));
                var iy1 = (int)(by - (nyIn * half));
                var ix2 = (int)(bx - (nxOut * half));
                var iy2 = (int)(by - (nyOut * half));
                var imx = bx - ((nxIn + nxOut) * half / 2.0 / Math.Max(RenderingConstants.Epsilon, Math.Abs(sinHalf)));
                var imy = by - ((nyIn + nyOut) * half / 2.0 / Math.Max(RenderingConstants.Epsilon, Math.Abs(sinHalf)));
                buffer.FillTriangle((int)imx,
                    (int)imy,
                    ix1,
                    iy1,
                    ix2,
                    iy2,
                    _gs.StrokeR,
                    _gs.StrokeG,
                    _gs.StrokeB,
                    _gs.StrokeA,
                    _gs.BlendMode);
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
                buffer.DrawLine((int)ax,
                    (int)ay,
                    (int)bx,
                    (int)by,
                    _gs.StrokeR,
                    _gs.StrokeG,
                    _gs.StrokeB,
                    thickPx,
                    _gs.StrokeA,
                    _gs.BlendMode);
            }

            pos = end;
            idx++;
            on = !on;
        }
    }

    // Average linear scale of the current CTM (geometric mean of the two basis-vector
    // magnitudes). Used to map user-space line widths into device space.
    private double CtmAverageScale()
    {
        var a = _gs.Ctm[0];
        var b = _gs.Ctm[1];
        var c = _gs.Ctm[2];
        var d = _gs.Ctm[3];
        var sx = Vector2D.Magnitude(a, b);
        var sy = Vector2D.Magnitude(c, d);
        var s = Math.Sqrt(sx * sy);
        return s > RenderingConstants.Epsilon ? s : 1.0;
    }

    private void ClearPath()
    {
        if (_pendingClip)
        {
            ApplyPendingClip(_pendingClipEvenOdd);
            _pendingClip = false;
            _pendingClipEvenOdd = false;
        }

        _subpaths.Clear();
        _curSub = null;
        _inPath = false;
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

    // Re-applies the current graphics-state clip to the buffer (after a Q restore).
    private void SyncClip() => buffer.RestoreClipMask(_gs.SavedClipMask);

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

    // ── XObject / image ───────────────────────────────────────────────────────

    private void PaintInlineImage(PdfInlineImage img)
    {
        // Inline images (BI…EI) fill the rectangle [0, 0, UserWidth, UserHeight]
        // in user space, unlike XObject images (Do) which fill the unit square.
        // The current CTM then maps this user-space rect to pixel space.
        var uw = img.UserWidth;
        var uh = img.UserHeight;

        var (x0, y0) = UToPixel(0, 0);
        var (x1, y1) = UToPixel(uw, uh);

        var dstX = (int)Math.Min(x0, x1);
        var dstY = (int)Math.Min(y0, y1);
        var dstW = (int)Math.Abs(x1 - x0);
        var dstH = (int)Math.Abs(y1 - y0);

        if (dstW <= 0 || dstH <= 0) return;

        BlitScaledImage(img.RgbData,
            img.Width,
            img.Height,
            dstX,
            dstY,
            dstW,
            dstH);
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

        BlitScaledImage(img.RgbData,
            img.Width,
            img.Height,
            dstX,
            dstY,
            dstW,
            dstH,
            img.Alpha);
    }

    // Scales an RGB image into the destination rectangle. When the image is downscaled
    // (more source than destination pixels) each destination pixel averages the source
    // box it covers, matching the area-averaging that Pdfium uses — nearest-neighbour
    // alone produces harsh aliasing and large pixel differences on small/scaled images.
    // When upscaling, falls back to nearest-neighbour sampling. When an alpha channel is
    // supplied (from an /SMask), pixels are composited over the background using it.
    private void BlitScaledImage(
        IReadOnlyList<byte> rgb,
        int srcW,
        int srcH,
        int dstX,
        int dstY,
        int dstW,
        int dstH,
        byte[]? alpha = null
    )
    {
        if (srcW <= 0 || srcH <= 0) return;

        var downscale = srcW > dstW || srcH > dstH;

        for (var py = 0; py < dstH; py++)
        for (var px = 0; px < dstW; px++)
        {
            byte r, g, b;
            int a;
            if (downscale)
            {
                // Average the source box [sx0,sx1)×[sy0,sy1) covered by this dest pixel.
                var sx0 = px * srcW / dstW;
                var sx1 = Math.Max(sx0 + 1, (px + 1) * srcW / dstW);
                var sy0 = py * srcH / dstH;
                var sy1 = Math.Max(sy0 + 1, (py + 1) * srcH / dstH);
                long sr = 0, sg = 0, sb = 0, sa = 0;
                var n = 0;
                for (var sy = sy0; sy < sy1 && sy < srcH; sy++)
                for (var sx = sx0; sx < sx1 && sx < srcW; sx++)
                {
                    var idx = (sy * srcW) + sx;
                    var o = idx * 3;
                    sr += rgb[o];
                    sg += rgb[o + 1];
                    sb += rgb[o + 2];
                    sa += alpha is not null ? alpha[idx] : 255;
                    n++;
                }

                if (n == 0) continue;

                r = (byte)(sr / n);
                g = (byte)(sg / n);
                b = (byte)(sb / n);
                a = (int)(sa / n);
            }
            else
            {
                var sx = px * srcW / dstW;
                var sy = py * srcH / dstH;
                var idx = (sy * srcW) + sx;
                var o = idx * 3;
                r = rgb[o];
                g = rgb[o + 1];
                b = rgb[o + 2];
                a = alpha is not null ? alpha[idx] : 255;
            }

            switch (a)
            {
                case <= 0:
                    continue;
                case >= 255:
                    buffer.BlitImagePixel(dstX + px, dstY + py, r, g, b);
                break;
                default:
                    buffer.BlendPixel(dstX + px,
                        dstY + py,
                        r,
                        g,
                        b,
                        (byte)a,
                        _gs.BlendMode);
                break;
            }
        }
    }

    // ── Coordinate helpers ────────────────────────────────────────────────────

    private (double Px, double Py) UToPixel(double x, double y)
    {
        var (ux, uy) = _gs.Transform(x, y);
        return (ux * scale, (pageHeightPt - uy) * scale);
    }

    private static double Num(ContentOperator op, int i) => op.Operands[i].ToDouble();

    private static double NumObj(PdfObject obj) => obj.ToDouble();

    // Maps a PDF colour component in [0,1] to an 8-bit channel value. Truncates (matches the
    // historic renderer behaviour) rather than rounding — do not change without re-baselining
    // the pixel-agreement tests.
    private static byte ToByteColor(double value) =>
        (byte)Math.Clamp((int)(value * RenderingConstants.ByteMax), 0, RenderingConstants.ByteMax);

    // ReSharper disable once BadListLineBreaks
    private static (double R, double G, double B) CmykToRgb(
        double c,
        double m,
        double y,
        double k
    ) =>
        ColorMath.CmykToRgb(c, m, y, k);

    // Seeds the initial fill colour — used when rendering an uncoloured (PaintType 2)
    // tiling pattern cell, which paints in the parent's current fill colour.
    internal void SetInitialFillColor(byte r, byte g, byte b)
    {
        _gs.FillR = r;
        _gs.FillG = g;
        _gs.FillB = b;
        _gs.StrokeR = r;
        _gs.StrokeG = g;
        _gs.StrokeB = b;
    }

    // Adds the glyph outline as a clip path contribution (text rendering modes 4–7).
    // Each glyph contour is accumulated into the buffer's clip mask via intersection (AND),
    // so subsequent drawing is clipped to the text shape.
    // ISO 32000-1 §9.3.6 — the clip accumulates across all glyphs in the text object.
    private void ClipGlyphOutline(GlyphFace ftFace, int penX, int penY)
    {
        // Use the outline of the already-loaded glyph (FT_LOAD_RENDER keeps it for vector fonts).
        var contours = ftFace.GetGlyphContours();
        if (contours.Count == 0) return;

        // Build polygon list, mapping font pixels (Y up) to device pixels (Y down).
        var polys = new List<(double X, double Y)[]>();
        foreach (var contour in contours)
        {
            if (contour.Length < 3) continue;

            var poly = new (double X, double Y)[contour.Length];
            for (var j = 0; j < contour.Length; j++)
                poly[j] = (penX + contour[j].X, penY - contour[j].Y);

            polys.Add(poly);
        }

        if (polys.Count == 0) return;

        // Intersect the glyph outline into the buffer's clip mask.
        // Even-odd rule matches the PDF spec for glyph outlines.
        buffer.SetClipPolygons(polys, true);
    }

    // Modulates a source alpha by the active soft mask at device pixel (x, y).
    // Returns the original alpha when no soft mask is active or (x,y) is out of range.
    private byte SoftMaskAlpha(int x, int y, byte a)
    {
        if (_gs.SoftMask is not { } mask) return a;
        if ((uint)x >= (uint)_gs.SoftMaskWidth || (uint)y >= (uint)_gs.SoftMaskHeight) return 0;

        var maskA = mask[(y * _gs.SoftMaskWidth) + x];
        return (byte)(a * maskA / 255);
    }

    // Fills a rectangle applying the soft mask per-pixel (used when HasSoftMask is true).
    private void FillRectSoftMasked(
        int px,
        int py,
        int pw,
        int ph,
        byte r,
        byte g,
        byte b,
        byte baseAlpha,
        string blendMode
    )
    {
        var x2 = px + pw;
        var y2 = py + ph;
        for (var y = py; y < y2; y++)
        for (var x = px; x < x2; x++)
        {
            buffer.SetPixel(x,
                y,
                r,
                g,
                b,
                SoftMaskAlpha(x, y, baseAlpha),
                blendMode);
        }
    }

    // Fills a scanline span applying the soft mask per-pixel.
    private void FillSpanSoftMasked(
        int y,
        int x0,
        int x1,
        byte r,
        byte g,
        byte b,
        byte baseAlpha,
        string blendMode
    )
    {
        for (var x = x0; x <= x1; x++)
        {
            buffer.SetPixel(x,
                y,
                r,
                g,
                b,
                SoftMaskAlpha(x, y, baseAlpha),
                blendMode);
        }
    }

    // Strokes the outline of the last-loaded glyph in FreeType using the current stroke
    // colour. Used for text rendering modes 1 (stroke only) and 2 (fill + stroke).
    // Glyph contour points are in font pixels relative to the glyph origin (Y up); we
    // flip Y and add the pen position to map them to device pixels.
    private void StrokeGlyphOutline(
        GlyphFace ftFace,
        int penX,
        int penY
    )
    {
        var contours = ftFace.GetGlyphContours();
        if (contours.Count == 0) return;

        // Stroke width for text: use LineWidth scaled by the text size.
        var ctmScale = CtmAverageScale();
        var thickPx = Math.Max(1, (int)Math.Round(_gs.LineWidth * ctmScale * scale));

        foreach (var contour in contours)
        {
            // Walk each contour as a polyline of on-curve points, approximating
            // off-curve (conic/cubic) control points with line segments.
            var prevX = 0.0;
            var prevY = 0.0;
            var first = true;
            foreach (var (cx, cy) in contour)
            {
                var ptX = penX + cx;
                // Font Y is upward (baseline = 0), buffer Y is downward.
                var ptY = penY - cy;

                if (!first)
                {
                    buffer.DrawLine(
                        (int)prevX,
                        (int)prevY,
                        (int)ptX,
                        (int)ptY,
                        _gs.StrokeR,
                        _gs.StrokeG,
                        _gs.StrokeB,
                        thickPx,
                        _gs.StrokeA,
                        _gs.BlendMode);
                }

                prevX = ptX;
                prevY = ptY;
                first = false;
            }

            // Close the contour back to the first point.
            if (!first && contour.Length > 0)
            {
                var firstX = penX + contour[0].X;
                var firstY = penY - contour[0].Y;
                buffer.DrawLine(
                    (int)prevX,
                    (int)prevY,
                    (int)firstX,
                    (int)firstY,
                    _gs.StrokeR,
                    _gs.StrokeG,
                    _gs.StrokeB,
                    thickPx,
                    _gs.StrokeA,
                    _gs.BlendMode);
            }
        }
    }

    // Converts color component values (0–1 range) using the named color space.
    // Falls back gracefully for unknown or unresolvable spaces.
    private (double R, double G, double B) ResolveColorComponents(double[] components, string csName)
    {
        // Device spaces — fast path, no lookup needed.
        switch (csName)
        {
            case RenderingConstants.DeviceGray:
            {
                var v = components.Length > 0 ? components[0] : 0;
                return (v, v, v);
            }
            case RenderingConstants.DeviceRgb:
                return components.Length >= 3
                    ? (components[0], components[1], components[2])
                    : (0, 0, 0);
            case RenderingConstants.DeviceCmyk:
            {
                if (components.Length < 4) return (0, 0, 0);

                var (r, g, b) = CmykToRgb(components[0], components[1], components[2], components[3]);
                return (r, g, b);
            }
        }

        // Named color space from /Resources /ColorSpace.
        if (colorSpaces is null || !colorSpaces.TryGetValue(csName, out var info))
        {
            return components.Length switch
            {
                1 => (components[0], components[0], components[0]),
                >= 4 => CmykToRgb(components[0], components[1], components[2], components[3]),
                >= 3 => (components[0], components[1], components[2]),
                _ => (0, 0, 0)
            };
        }

        var (r2, g2, b2) = info.ToRgb(components);
        return (r2 / 255.0, g2 / 255.0, b2 / 255.0);

        // Unknown space — fall back to component-count heuristic.
    }

    // Renders a string set in a Type3 font. Each glyph is a mini content stream
    // (PDF operators) stored in the font's /CharProcs dictionary. The stream is
    // rendered into the main buffer by creating a child PageRenderer with a CTM
    // composed from: FontMatrix × current text+CTM, translated to the glyph origin.
    // ISO 32000-1 §9.6.5.
    private void ShowStringType3(ReadOnlySpan<byte> bytes, Type3FontInfo t3)
    {
        var fm = t3.FontMatrix; // [a b c d e f] glyph→text space
        foreach (var code in bytes)
        {
            var glyphName = t3.Encoding.Length > code ? t3.Encoding[code] : null;
            if (glyphName is null || !t3.CharProcs.TryGetValue(glyphName, out var ops))
            {
                // No glyph — advance by width if available.
                AdvanceType3(t3, code);
                continue;
            }

            // Build the glyph CTM: FontMatrix × TextMatrix × CTM, then translate to
            // the current text-space origin (TextMatrix[4], TextMatrix[5]).
            // PDF §9.4.4: glyph origin in text space = current text matrix position.
            var tm = _gs.TextMatrix;
            // Compose: glyphCtm = FontMatrix × TextMatrix × CTM
            // First concatenate FontMatrix into the current CTM chain.
            // T = TextMatrix, C = CTM, F = FontMatrix
            // Device = F × T × C (right-to-left: C applied first)
            // Combined = F × T, then apply C.
            var ftm = GraphicsState.MultiplyMatrix(fm, tm);       // F × T
            var ctm = GraphicsState.MultiplyMatrix(ftm, _gs.Ctm); // F × T × C

            // Scale the glyph: FontSize is applied via the text matrix magnitude.
            var textScale = _gs.FontSize * TextMatrixVerticalScale();

            // Build the initial CTM for the child renderer:
            // scale glyph space by FontSize × device scale, apply page flip.
            double[] glyphCtm =
            [
                ctm[0] * textScale, ctm[1] * textScale,
                ctm[2] * textScale, ctm[3] * textScale,
                ctm[4], ctm[5]
            ];

            // Render the glyph's content stream into the main buffer.
            var glyphRenderer = new PageRenderer(
                buffer,
                fonts,
                scale,
                pageHeightPt,
                embeddedFontBytes,
                imageXObjects,
                glyphCtm,
                toUnicodeMaps,
                compositeFonts,
                extGStateAlphas,
                shadings,
                tilingPatterns,
                null,
                colorSpaces,
                type3Fonts);

            glyphRenderer.Render(ops, EmptyFontMap);

            AdvanceType3(t3, code);
        }
    }

    private void AdvanceType3(Type3FontInfo t3, byte code)
    {
        // Advance width from /Widths array (glyph-space units).
        var idx = code - t3.FirstChar;
        var wGlyph = idx >= 0 && idx < t3.Widths.Length ? t3.Widths[idx] : 0.0;
        // Convert glyph space → text space via FontMatrix[0] (x scale), then apply FontSize.
        var fm = t3.FontMatrix;
        var advance = ((wGlyph * fm[0] * _gs.FontSize) + _gs.CharSpace)
                      * (_gs.HorizontalScale / RenderingConstants.HorizontalScalePercent);
        _gs.TextMatrix[4] += advance;
    }

    private void SetFillGray(double gray)
    {
        var v = ToByteColor(gray);
        _gs.FillR = _gs.FillG = _gs.FillB = v;
        _gs.FillIsPattern = false;
        _gs.FillShadingName = null;
        _gs.FillTilingName = null;
    }

    private void SetStrokeGray(double gray)
    {
        var v = ToByteColor(gray);
        _gs.StrokeR = _gs.StrokeG = _gs.StrokeB = v;
    }

    private void SetFillRgb(double r, double g, double b)
    {
        _gs.FillR = ToByteColor(r);
        _gs.FillG = ToByteColor(g);
        _gs.FillB = ToByteColor(b);
        _gs.FillIsPattern = false;
        _gs.FillShadingName = null;
        _gs.FillTilingName = null;
    }

    private void SetStrokeRgb(double r, double g, double b)
    {
        _gs.StrokeR = ToByteColor(r);
        _gs.StrokeG = ToByteColor(g);
        _gs.StrokeB = ToByteColor(b);
    }

    // Renders a soft mask Form XObject into a per-pixel alpha array (device space).
    // The mask form is rendered into a temporary black-backdrop RasterBuffer at the same
    // device dimensions as the main page buffer. For /Alpha masks the rendered brightness
    // is taken as opacity (black=transparent, white=opaque). For /Luminosity masks the
    // standard luminance coefficients are applied. ISO 32000-1 §11.6.5.
    private byte[] RenderSoftMask(SoftMaskInfo smInfo)
    {
        try
        {
            var maskBuf = new RasterBuffer(smInfo.WidthPx, smInfo.HeightPx);
            maskBuf.Clear(0, 0, 0);

            var bbox = smInfo.BBox;
            var m = smInfo.Matrix;
            var formW = bbox[2] - bbox[0];
            var formH = bbox[3] - bbox[1];
            var sx = formW > 0 ? smInfo.WidthPx / formW : 1.0;
            var sy = formH > 0 ? smInfo.HeightPx / formH : 1.0;
            var s = Math.Min(sx, sy);

            double[] ctm =
            [
                m[0] * s, m[1] * s,
                m[2] * s, m[3] * s,
                (m[4] - bbox[0]) * s,
                (m[5] - bbox[1]) * s
            ];

            var formPage = smInfo.FormPage;
            var maskRenderer = new PageRenderer(
                maskBuf,
                fonts,
                s,
                formH > 0 ? formH : pageHeightPt,
                formPage.GetEmbeddedFontBytes(),
                formPage.GetImageXObjects(),
                ctm,
                formPage.GetToUnicodeMaps(),
                formPage.GetCompositeFonts(),
                formPage.GetExtGStateAlphas(),
                formPage.GetShadings(),
                formPage.GetTilingPatterns(),
                null,
                (formPage as PdfPageAdapter)?.GetColorSpaces());

            maskRenderer.Render(smInfo.Operators, formPage.GetFontNameMap());

            var pixels = maskBuf.ToArgbBytes();
            var alpha = new byte[smInfo.WidthPx * smInfo.HeightPx];
            for (var y = 0; y < smInfo.HeightPx; y++)
            for (var x = 0; x < smInfo.WidthPx; x++)
            {
                var o = ((y * smInfo.WidthPx) + x) * 4;
                alpha[(y * smInfo.WidthPx) + x] = smInfo.MaskType == RenderingConstants.SoftMaskLuminosity
                    ? (byte)(((pixels[o] * RenderingConstants.LumaR)
                              + (pixels[o + 1] * RenderingConstants.LumaG)
                              + (pixels[o + 2] * RenderingConstants.LumaB)) >> RenderingConstants.LumaShift)
                    : pixels[o];
            }

            return alpha;
        }
        // ReSharper disable once EmptyGeneralCatchClause
        catch
        {
            // On failure, return a fully-opaque mask so content is not incorrectly hidden.
            var fallback = new byte[smInfo.WidthPx * smInfo.HeightPx];
            Array.Fill(fallback, RenderingConstants.OpaqueAlpha);
            return fallback;
        }
    }
}
