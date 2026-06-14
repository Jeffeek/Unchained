using Unchained.Drawing;
using Unchained.Drawing.Primitives;
using Unchained.Drawing.Text;
using Unchained.Pdf.Core;
using Unchained.Pdf.Engine.PageResources;
using Unchained.Pdf.Models;

namespace Unchained.Pdf.Rendering.Rendering;

/// <summary>
///     Walks a list of <see cref="ContentOperator" /> records and rasterizes them
///     into a <see cref="RasterBuffer" /> using the PDF graphics model (ISO 32000-1 §8–9).
/// </summary>
/// <remarks>
///     The renderer is a state machine centred on a single mutable <see cref="GraphicsState" />
///     (<see cref="_gs" />) that is threaded through every operator, so its responsibilities
///     cannot be cleanly separated into independent injected collaborators. Instead the
///     implementation is split across cohesive <c>partial</c> files that share that state:
///     <list type="bullet">
///         <item><c>PageRenderer.cs</c> — operator dispatch, graphics-state stack, colour state and coordinate helpers.</item>
///         <item>
///             <c>PageRenderer.Text.cs</c> — text-showing pipeline (HarfBuzz shaping, direct, composite and Type3
///             glyphs).
///         </item>
///         <item><c>PageRenderer.Paths.cs</c> — path construction, filling, stroking and clipping.</item>
///         <item><c>PageRenderer.Shading.cs</c> — axial/radial/mesh shadings and tiling patterns.</item>
///         <item><c>PageRenderer.Images.cs</c> — image XObject / inline-image blitting and soft masks.</item>
///     </list>
///     The genuinely state-free gradient mathematics live in <see cref="ShadingMath" />.
/// </remarks>
internal sealed partial class PageRenderer(
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
                    .Select(static o => o.ReadIntOrReal())
                    .ToArray();
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
                        var components = nums.Select(static o => o.ReadIntOrReal()).ToArray();
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
                                var v = nums[0].ReadIntOrReal();
                                if (op.Name == "scn") SetFillGray(v);
                                else SetStrokeGray(v);
                                break;
                            }
                            case 3:
                            {
                                var (r2, g2, b2) = (nums[0].ReadIntOrReal(), nums[1].ReadIntOrReal(), nums[2].ReadIntOrReal());
                                if (op.Name == "scn") SetFillRgb(r2, g2, b2);
                                else SetStrokeRgb(r2, g2, b2);
                                break;
                            }
                            case 4:
                            {
                                var (r2, g2, b2) = CmykToRgb(
                                    nums[0].ReadIntOrReal(),
                                    nums[1].ReadIntOrReal(),
                                    nums[2].ReadIntOrReal(),
                                    nums[3].ReadIntOrReal()
                                );
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
                    ? da.Elements.Select(static o => o.ReadIntOrReal()).Where(static v => v >= 0).ToArray()
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
                PathCurveTo(
                    Num(op, 0),
                    Num(op, 1),
                    Num(op, 2),
                    Num(op, 3),
                    Num(op, 4),
                    Num(op, 5)
                );
            break;
            case "v" when op.Operands.Count >= 4:
                PathCurveTo(
                    _currentPoint.X,
                    _currentPoint.Y,
                    Num(op, 0),
                    Num(op, 1),
                    Num(op, 2),
                    Num(op, 3)
                );
            break;
            case "y" when op.Operands.Count >= 4:
                PathCurveTo(
                    Num(op, 0),
                    Num(op, 1),
                    Num(op, 2),
                    Num(op, 3),
                    Num(op, 2),
                    Num(op, 3)
                );
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

    // ── Path lifecycle ──────────────────────────────────────────────────────────

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

    // Re-applies the current graphics-state clip to the buffer (after a Q restore).
    private void SyncClip() => buffer.RestoreClipMask(_gs.SavedClipMask);

    // ── Coordinate helpers ────────────────────────────────────────────────────

    private (double Px, double Py) UToPixel(double x, double y)
    {
        var (ux, uy) = _gs.Transform(x, y);
        return (ux * scale, (pageHeightPt - uy) * scale);
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

    private static double Num(ContentOperator op, int i) => op.Operands[i].ReadIntOrReal();

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

    // ── Colour state ──────────────────────────────────────────────────────────

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
}
