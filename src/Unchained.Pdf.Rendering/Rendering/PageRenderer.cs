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
    double[]? initialCtm = null,
    IReadOnlyDictionary<string, IReadOnlyDictionary<uint, string>>? toUnicodeMaps = null,
    IReadOnlyDictionary<string, CompositeFontInfo>? compositeFonts = null,
    IReadOnlyDictionary<string, (double Fill, double Stroke)>? extGStateAlphas = null,
    IReadOnlyDictionary<string, ShadingInfo>? shadings = null,
    IReadOnlyDictionary<string, TilingPatternInfo>? tilingPatterns = null
)
{
    // Current path as a list of subpaths, each a polyline of user-space points. A new `m`
    // (or `re`) starts a new subpath; `l`/`c`/`v`/`y` append to the current one. This
    // preserves multiple subpaths (needed for polygon fills with holes and for stroking
    // disjoint figures) — the previous segment-pair model kept only the last subpath.
    private readonly List<List<(double X, double Y)>> _subpaths = [];
    private List<(double X, double Y)>? _curSub;
    private (double X, double Y) _pathStart;
    private (double X, double Y) _currentPoint;
    private bool _inPath;
    // Nesting depth for tiling-pattern cell rendering; bounds pattern-in-pattern recursion.
    internal int _tilingDepth;
    private static readonly Dictionary<string, string> EmptyFontMap = new();
    // Set by W/W*; the clip is applied (intersected into the graphics state) when the
    // current path is next cleared by a painting/no-op operator.
    private bool _pendingClip;

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
                // scn/SCN may carry a trailing name operand naming a tiling/shading pattern.
                // We don't render patterns; flag fills so DrawFill can skip them rather than
                // painting a solid block (which appears as a large wrong dark area).
                var isPattern = op.Operands.Any(static o => o is PdfName);

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

                // Set after the numeric setters above (which clear the flag).
                if (op.Name == "scn")
                {
                    _gs.FillIsPattern = isPattern;
                    // If the named pattern is a known axial/radial shading or tiling pattern,
                    // remember it so DrawFill renders it rather than the grey approximation.
                    var patName = op.Operands.OfType<PdfName>().LastOrDefault()?.Value;
                    _gs.FillShadingName = patName is not null && shadings is not null && shadings.ContainsKey(patName)
                        ? patName : null;
                    _gs.FillTilingName = patName is not null && tilingPatterns is not null && tilingPatterns.ContainsKey(patName)
                        ? patName : null;
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
            case "J" or "j" or "M" or "ri" or "i": break; // consume; not rendered
            case "gs" when op.Operands.Count >= 1:
            {
                // Apply the named /ExtGState's constant alpha (/ca fill, /CA stroke).
                var name = (op.Operands[0] as PdfName)?.Value;
                if (name is not null && extGStateAlphas is not null
                    && extGStateAlphas.TryGetValue(name, out var a))
                {
                    _gs.FillA = (byte)Math.Clamp((int)Math.Round(a.Fill * 255), 0, 255);
                    _gs.StrokeA = (byte)Math.Clamp((int)Math.Round(a.Stroke * 255), 0, 255);
                }
                break;
            }
            case "gs": break;

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
                PathClose();
                break;
            }
            case "re" when op.Operands.Count >= 4:
                PathRect(Num(op, 0), Num(op, 1), Num(op, 2), Num(op, 3));
                break;

            // ── Path painting ─────────────────────────────────────────────────
            case "S": DrawStroke(); ClearPath(); break;
            case "s":
                PathClose();
                DrawStroke();
                ClearPath();
                break;
            case "f" or "F": DrawFill(evenOdd: false); ClearPath(); break;
            case "f*": DrawFill(evenOdd: true); ClearPath(); break;
            case "B": DrawFill(evenOdd: false); DrawStroke(); ClearPath(); break;
            case "B*": DrawFill(evenOdd: true); DrawStroke(); ClearPath(); break;
            case "b":
                PathClose();
                DrawFill(evenOdd: false);
                DrawStroke();
                ClearPath();
                break;
            case "b*":
                PathClose();
                DrawFill(evenOdd: true);
                DrawStroke();
                ClearPath();
                break;
            case "n": ClearPath(); break;


            // ── Clip ──────────────────────────────────────────────────────────
            // W/W* set the clip to the current path; it takes effect AFTER the next
            // painting operator (ISO 32000-1 §8.5.4). We record a pending clip (the path's
            // device-space bounding box) and apply it when the path is cleared.
            case "W" or "W*": _pendingClip = true; break;

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
        var mag = Math.Sqrt((vx * vx) + (vy * vy));
        return mag > 1e-6 ? mag : 1.0;
    }

    // Horizontal scale magnitude of the text matrix combined with the CTM's linear part.
    // Text advances are computed in glyph space and must be scaled by this to land in the
    // pre-CTM position space that UToPixel consumes. Returns 1 for unit-scale matrices.
    private double TextMatrixHorizontalScale()
    {
        var ta = _gs.TextMatrix[0];
        var tc = _gs.TextMatrix[2];
        // Horizontal text-space basis (1,0) through the text matrix linear part.
        var hx = ta;
        var hy = tc;
        var mag = Math.Sqrt((hx * hx) + (hy * hy));
        return mag > 1e-6 ? mag : 1.0;
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

        // Effective glyph size in device pixels. The on-page text size is FontSize scaled
        // by the vertical magnitude of the text matrix combined with the CTM, then by the
        // device scale (DPI/72). Some producers set FontSize to 1 and carry the real size
        // in the text/transformation matrix (e.g. `Tf /F 1` with `Tm 16 0 0 -16 …`), so
        // FontSize alone is not the rasterization size. When the matrices have unit scale
        // (the common case) this reduces exactly to FontSize * scale.
        var textVScale = TextMatrixVerticalScale();
        var pixelSize = (uint)Math.Max(1, Math.Round(_gs.FontSize * textVScale * scale));
        ftFace.SetPixelSizes(0, pixelSize);

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
            var sb = new System.Text.StringBuilder();
            var span = bytes;
            while (!span.IsEmpty)
            {
                // Try 2-byte code first, then 1-byte
                uint code2 = span.Length >= 2 ? (uint)((span[0] << 8) | span[1]) : 0;
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
                    sb.Append('�'); // replacement char for unmapped codes
                    span = span[1..];
                }
            }
            unicodeText = sb.ToString();
        }
        else
        {
            unicodeText = System.Text.Encoding.Latin1.GetString(bytes.ToArray());
        }

        using var hbBuffer = new HarfBuzzSharp.Buffer();
        hbBuffer.AddUtf8(unicodeText);
        hbBuffer.GuessSegmentProperties();
        hbFont.Shape(hbBuffer);

        var glyphInfos    = hbBuffer.GlyphInfos;
        var glyphPositions = hbBuffer.GlyphPositions;

        // HarfBuzz resolves glyphs through the font's Unicode cmap. Embedded subset
        // Type1 fonts (e.g. Computer Modern) have a custom/builtin encoding and no
        // Unicode cmap, so HarfBuzz returns .notdef (glyph 0) for every character and
        // the text renders blank. Detect that case and fall back to resolving each raw
        // char code directly through FreeType's own charmap (FT_Get_Char_Index), which
        // honours the font's builtin encoding. Composite (Type0/CID) fonts and fonts
        // with a real Unicode cmap produce non-zero glyphs and keep the HarfBuzz path.
        var allNotdef = glyphInfos.Length > 0;
        for (var i = 0; i < glyphInfos.Length; i++)
            if (glyphInfos[i].Codepoint != 0) { allNotdef = false; break; }

        if (allNotdef)
        {
            ShowStringDirect(bytes, ftFace, pixelSize);
            return;
        }

        for (var i = 0; i < glyphInfos.Length; i++)
        {
            var glyphId = glyphInfos[i].Codepoint;

            // ReSharper disable once EmptyGeneralCatchClause
            try { ftFace.LoadGlyph(glyphId, LoadFlags.Render | LoadFlags.NoHinting, LoadTarget.Normal); }
            catch { GlyphsSkipped++; continue; }

            GlyphsAttempted++;

            // HarfBuzz XOffset/YOffset are in 26.6 pixel units; convert to user-space points.
            // Text rise (Ts) shifts the baseline up in text space.
            var originX = _gs.TextMatrix[4] + (glyphPositions[i].XOffset / 64.0 / scale);
            var originY = _gs.TextMatrix[5] + (glyphPositions[i].YOffset / 64.0 / scale) + _gs.TextRise;
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

    // Fallback path for simple fonts whose embedded program has no usable Unicode cmap
    // (e.g. subset Computer Modern Type1, or symbolic subset TrueType with /FirstChar 0).
    // Each raw single-byte char code is resolved to a glyph index through FreeType's own
    // charmap (FT_Get_Char_Index). When that yields .notdef, the font is a glyph-indexed
    // subset where the char code IS the glyph index, so we fall back to loading the code
    // directly. Advances come from FreeType (FT_Get_Advance).
    private void ShowStringDirect(ReadOnlySpan<byte> bytes, SharpFont.Face ftFace, uint pixelSize)
    {
        foreach (var code in bytes)
        {
            uint glyphId;
            try { glyphId = ftFace.GetCharIndex(code); }
            catch { GlyphsSkipped++; continue; }

            // Symbolic subset with no cmap entry: treat the char code as a direct glyph
            // index (FreeType rejects out-of-range indices in LoadGlyph below). SharpFont's
            // GlyphCount is unreliable on Windows x64, so we don't pre-check the range.
            if (glyphId == 0 && code > 0)
                glyphId = code;

            if (glyphId != 0)
            {
                try { ftFace.LoadGlyph(glyphId, LoadFlags.Render | LoadFlags.NoHinting, LoadTarget.Normal); }
                catch { GlyphsSkipped++; glyphId = 0; }
            }

            if (glyphId != 0)
            {
                GlyphsAttempted++;
                var (px, py) = UToPixel(_gs.TextMatrix[4], _gs.TextMatrix[5] + _gs.TextRise);
                buffer.BlitGlyphFromFace((int)px, (int)py, ftFace, _gs.FillR, _gs.FillG, _gs.FillB);
            }

            // Glyph advance (16.16 fixed-point pixels when scaled) → user-space points.
            double advancePts;
            try { advancePts = ftFace.GetAdvance(glyphId, LoadFlags.Default).ToDouble() / scale; }
            catch { advancePts = pixelSize / scale * 0.5; }

            var advance = (advancePts + _gs.CharSpace + (code == 32 ? _gs.WordSpace : 0))
                          * (_gs.HorizontalScale / 100.0);
            _gs.TextMatrix[4] += advance;
        }
    }

    // Renders a string set in a composite (Type0) font with an Identity encoding.
    // Each pair of bytes is a big-endian 16-bit code that equals the CID; the CID maps
    // to a glyph index (Identity /CIDToGIDMap, or an explicit map). Glyphs are loaded by
    // index directly through FreeType — no cmap/HarfBuzz shaping. Advances come from the
    // CIDFont /W array (glyph-space 1000-unit em), falling back to /DW.
    private void ShowStringComposite(ReadOnlySpan<byte> bytes, SharpFont.Face ftFace, CompositeFontInfo info)
    {
        for (var i = 0; i + 1 < bytes.Length; i += 2)
        {
            var cid = (bytes[i] << 8) | bytes[i + 1];

            var gid = (uint)cid;
            if (!info.IdentityCidToGid && info.CidToGid is not null)
                gid = info.CidToGid.TryGetValue(cid, out var mapped) ? (uint)mapped : 0;

            if (gid != 0)
            {
                try { ftFace.LoadGlyph(gid, LoadFlags.Render | LoadFlags.NoHinting, LoadTarget.Normal); }
                catch { GlyphsSkipped++; gid = 0; }
            }

            if (gid != 0)
            {
                GlyphsAttempted++;
                var (px, py) = UToPixel(_gs.TextMatrix[4], _gs.TextMatrix[5] + _gs.TextRise);
                buffer.BlitGlyphFromFace((int)px, (int)py, ftFace, _gs.FillR, _gs.FillG, _gs.FillB);
            }

            // Advance from /W (glyph-space units, 1000 per em) → text-space, then scaled
            // by the text-matrix horizontal magnitude into the pen-position space that
            // UToPixel consumes (handles producers that carry size in the matrix).
            var wGlyph = info.Widths.TryGetValue(cid, out var w) ? w : info.DefaultWidth;
            var hScale = TextMatrixHorizontalScale();
            var advance = (((wGlyph / 1000.0 * _gs.FontSize) + _gs.CharSpace) * hScale)
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
                    ShowString(s.GetBinaryBytes().Span);
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
        // Start a new subpath. Does NOT clear earlier subpaths (a path may contain several).
        _curSub = [(x, y)];
        _subpaths.Add(_curSub);
        _pathStart = _currentPoint = (x, y);
        _inPath = true;
    }

    private void PathLineTo(double x, double y)
    {
        if (_curSub is null) { PathMoveTo(x, y); return; }
        _curSub.Add((x, y));
        _currentPoint = (x, y);
    }

    // ReSharper disable once BadListLineBreaks
    private void PathRect(double x, double y, double w, double h)
    {
        // A rectangle is its own closed subpath (ISO 32000-1 §8.5.2.1).
        PathMoveTo(x, y);
        _curSub!.Add((x + w, y));
        _curSub.Add((x + w, y + h));
        _curSub.Add((x, y + h));
        _curSub.Add((x, y)); // close
        _currentPoint = (x, y);
    }

    private void PathCurveTo(double x1, double y1, double x2, double y2, double x3, double y3)
    {
        if (_curSub is null) PathMoveTo(_currentPoint.X, _currentPoint.Y);
        var p0 = _currentPoint;
        for (var t = 1; t <= 8; t++)
        {
            var s  = t / 8.0;
            var u  = 1 - s;
            var bx = (u * u * u * p0.X) + (3 * u * u * s * x1) + (3 * u * s * s * x2) + (s * s * s * x3);
            var by = (u * u * u * p0.Y) + (3 * u * u * s * y1) + (3 * u * s * s * y2) + (s * s * s * y3);
            _curSub!.Add((bx, by));
            _currentPoint = (bx, by);
        }
    }

    private void PathClose()
    {
        if (_inPath && _curSub is { Count: > 0 })
        {
            _curSub.Add(_pathStart);
            _currentPoint = _pathStart;
        }
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
            buffer.FillRect(
                (int)px1, (int)py1,
                (int)(px2 - px1 + 1), (int)(py2 - py1 + 1),
                fr, fg, fb, _gs.FillA);
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
        var minX = double.MaxValue; var minY = double.MaxValue;
        var maxX = double.MinValue; var maxY = double.MinValue;
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
        var sxv = Math.Sqrt((pm[0] * pm[0]) + (pm[1] * pm[1]));
        var syv = Math.Sqrt((pm[2] * pm[2]) + (pm[3] * pm[3]));
        var stepXpx = Math.Abs(tp.XStep) * sxv * scale;
        var stepYpx = Math.Abs(tp.YStep) * syv * scale;
        if (stepXpx < 0.5 || stepYpx < 0.5) return;

        // Cap tile pixel size and total tile count to keep this bounded.
        var tileW = Math.Clamp((int)Math.Ceiling(stepXpx), 1, 256);
        var tileH = Math.Clamp((int)Math.Ceiling(stepYpx), 1, 256);

        // Render one cell into its own buffer. The cell content draws in pattern space with
        // BBox origin; we scale pattern→tile pixels and flip Y to match the cell content.
        var tile = new RasterBuffer(tileW, tileH);
        tile.Clear(255, 255, 255);
        var cellScaleX = tileW / (tp.XStep == 0 ? 1 : Math.Abs(tp.XStep));
        var cellScaleY = tileH / (tp.YStep == 0 ? 1 : Math.Abs(tp.YStep));
        var cellScale = Math.Min(cellScaleX, cellScaleY);
        // Initial CTM translates the BBox lower-left to the tile origin.
        double[] cellCtm = [1, 0, 0, 1, -tp.BBox[0], -tp.BBox[1]];
        var cell = new PageRenderer(tile, fonts, cellScale, tp.YStep == 0 ? tileH / cellScale : Math.Abs(tp.YStep),
            embeddedFontBytes, imageXObjects, cellCtm, toUnicodeMaps, compositeFonts, extGStateAlphas, shadings, tilingPatterns)
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
        if (_gs.ClipRect is { } c)
        {
            x0 = Math.Max(x0, c.X0); y0 = Math.Max(y0, c.Y0);
            x1 = Math.Min(x1, c.X1 - 1); y1 = Math.Min(y1, c.Y1 - 1);
        }

        for (var py = y0; py <= y1; py++)
        for (var px = x0; px <= x1; px++)
        {
            var tx = ((px - x0) % tileW + tileW) % tileW;
            var ty = ((py - y0) % tileH + tileH) % tileH;
            var o = ((ty * tileW) + tx) * 4;
            var r = tileData[o]; var g = tileData[o + 1]; var b = tileData[o + 2];
            if (r >= 250 && g >= 250 && b >= 250) continue; // skip the cell's white background
            buffer.BlitImagePixel(px, py, r, g, b);
        }
    }
    private void PaintShadingInClip(ShadingInfo sh)
    {
        var (x0, y0, x1, y1) = _gs.ClipRect ?? (0, 0, buffer.Width, buffer.Height);
        PaintShadingRect(sh, x0, y0, x1 - 1, y1 - 1);
    }

    // Core gradient rasteriser: for each device pixel in [dx0..dx1]×[dy0..dy1], map back to
    // user space (inverse CTM), compute the shading's parametric t, and write the ramp colour.
    private void PaintShadingRect(ShadingInfo sh, int dx0, int dy0, int dx1, int dy1)
    {
        // Mesh shadings are painted as Gouraud-interpolated triangles, ignoring the rect.
        if (sh.IsMesh)
        {
            PaintMesh(sh);
            return;
        }

        dx0 = Math.Max(0, dx0); dy0 = Math.Max(0, dy0);
        dx1 = Math.Min(buffer.Width - 1, dx1); dy1 = Math.Min(buffer.Height - 1, dy1);
        if (dx1 < dx0 || dy1 < dy0) return;

        // Honour an active clip rectangle.
        if (_gs.ClipRect is { } c)
        {
            dx0 = Math.Max(dx0, c.X0); dy0 = Math.Max(dy0, c.Y0);
            dx1 = Math.Min(dx1, c.X1 - 1); dy1 = Math.Min(dy1, c.Y1 - 1);
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
            else buffer.BlendPixel(px, py, r, g, b, _gs.FillA);
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
            minX = Math.Max(minX, 0); minY = Math.Max(minY, 0);
            maxX = Math.Min(maxX, buffer.Width - 1); maxY = Math.Min(maxY, buffer.Height - 1);
            if (_gs.ClipRect is { } c)
            {
                minX = Math.Max(minX, c.X0); minY = Math.Max(minY, c.Y0);
                maxX = Math.Min(maxX, c.X1 - 1); maxY = Math.Min(maxY, c.Y1 - 1);
            }

            var denom = ((by - cy) * (ax - cx)) + ((cx - bx) * (ay - cy));
            if (Math.Abs(denom) < 1e-9) continue; // degenerate triangle

            for (var py = minY; py <= maxY; py++)
            for (var px = minX; px <= maxX; px++)
            {
                var fx = px + 0.5; var fy = py + 0.5;
                var w0 = (((by - cy) * (fx - cx)) + ((cx - bx) * (fy - cy))) / denom;
                var w1 = (((cy - ay) * (fx - cx)) + ((ax - cx) * (fy - cy))) / denom;
                var w2 = 1 - w0 - w1;
                if (w0 < -0.0001 || w1 < -0.0001 || w2 < -0.0001) continue; // outside triangle

                var r = (byte)Math.Clamp((w0 * t.R0) + (w1 * t.R1) + (w2 * t.R2), 0, 255);
                var g = (byte)Math.Clamp((w0 * t.G0) + (w1 * t.G1) + (w2 * t.G2), 0, 255);
                var b = (byte)Math.Clamp((w0 * t.B0) + (w1 * t.B1) + (w2 * t.B2), 0, 255);
                if (_gs.FillA >= 255) buffer.BlitImagePixel(px, py, r, g, b);
                else buffer.BlendPixel(px, py, r, g, b, _gs.FillA);
            }
        }
    }

    // Computes parametric t∈[0,1] for a user-space point under the shading, applying the
    // extend flags. Returns false when the point is outside a non-extended shading.
    private static bool ShadingT(ShadingInfo sh, double x, double y, out double t)
    {
        t = 0;
        if (sh.ShadingType == 2)
        {
            // Axial: project (x,y) onto the axis (x0,y0)->(x1,y1).
            var x0 = sh.Coords[0]; var y0 = sh.Coords[1];
            var x1 = sh.Coords[2]; var y1 = sh.Coords[3];
            var dx = x1 - x0; var dy = y1 - y0;
            var len2 = (dx * dx) + (dy * dy);
            if (len2 < 1e-9) { t = 0; return true; }
            t = (((x - x0) * dx) + ((y - y0) * dy)) / len2;
        }
        else
        {
            // Radial: t such that the point lies on the interpolated circle. Approximate by
            // normalised distance from centre 0 to centre 1 (handles the common concentric case).
            var cx0 = sh.Coords[0]; var cy0 = sh.Coords[1]; var r0 = sh.Coords[2];
            var cx1 = sh.Coords[3]; var cy1 = sh.Coords[4]; var r1 = sh.Coords[5];
            var d = Math.Sqrt(((x - cx1) * (x - cx1)) + ((y - cy1) * (y - cy1)));
            var denom = r1 - r0;
            t = Math.Abs(denom) > 1e-9 ? (d - r0) / denom : (r1 > 1e-9 ? d / r1 : 0);
            _ = cx0; _ = cy0;
        }

        if (t < 0) { if (!sh.ExtendStart) return false; t = 0; }
        if (t > 1) { if (!sh.ExtendEnd) return false; t = 1; }
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
        if (Math.Abs(det) < 1e-12) { inv = []; return false; }
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

    private static (double X, double Y) ApplyInv(double[] m, double px, double py) =>
        ((m[0] * px) + (m[2] * py) + m[4], (m[1] * px) + (m[3] * py) + m[5]);

    // Scan-converts all current subpaths to device pixels and fills using the given winding
    // rule. Each subpath is treated as implicitly closed (PDF fills close open subpaths).
    private void FillPolygon(bool evenOdd, byte fr, byte fg, byte fb)
    {
        // Flatten every subpath to device-space points and find the vertical extent.
        var polys = new List<(double X, double Y)[]>(_subpaths.Count);
        var minY = double.MaxValue; var maxY = double.MinValue;
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
                    var (bx, by) = pts[(i + 1) % n]; // implicit close
                    if (ay == by) continue; // horizontal edge contributes no crossing
                    // Half-open [min,max) so shared vertices aren't double-counted.
                    if (sy >= Math.Min(ay, by) && sy < Math.Max(ay, by))
                    {
                        var t = (sy - ay) / (by - ay);
                        var cx = ax + (t * (bx - ax));
                        xs.Add((cx, by > ay ? 1 : -1));
                    }
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
                if (xEnd > xStart)
                    buffer.FillSpan(y, xStart, xEnd - 1, fr, fg, fb, _gs.FillA);
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
                    buffer.DrawLine((int)x0, (int)y0, (int)x1, (int)y1,
                        _gs.StrokeR, _gs.StrokeG, _gs.StrokeB, thickPx, _gs.StrokeA);
                else
                    DrawDashedLine(x0, y0, x1, y1, thickPx, dashPx);
            }
        }
    }

    // Draws a line as a dash pattern by walking its length and emitting "on" sub-segments.
    // dashPx alternates on/off lengths starting with "on"; an odd-length array repeats to
    // form the full cycle (ISO 32000-1 §8.4.3.6). Phase is assumed 0 per segment.
    private void DrawDashedLine(double x0, double y0, double x1, double y1, int thickPx, double[] dashPx)
    {
        var dx = x1 - x0;
        var dy = y1 - y0;
        var len = Math.Sqrt((dx * dx) + (dy * dy));
        if (len < 1e-6) return;
        var ux = dx / len;
        var uy = dy / len;

        var pos = 0.0;
        var idx = 0;
        var on = true;
        while (pos < len)
        {
            var seg = dashPx[idx % dashPx.Length];
            if (seg <= 0) { idx++; on = !on; continue; }
            var end = Math.Min(len, pos + seg);
            if (on)
            {
                var ax = x0 + (ux * pos); var ay = y0 + (uy * pos);
                var bx = x0 + (ux * end); var by = y0 + (uy * end);
                buffer.DrawLine((int)ax, (int)ay, (int)bx, (int)by,
                    _gs.StrokeR, _gs.StrokeG, _gs.StrokeB, thickPx, _gs.StrokeA);
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
        var a = _gs.Ctm[0]; var b = _gs.Ctm[1];
        var c = _gs.Ctm[2]; var d = _gs.Ctm[3];
        var sx = Math.Sqrt((a * a) + (b * b));
        var sy = Math.Sqrt((c * c) + (d * d));
        var s = Math.Sqrt(sx * sy);
        return s > 1e-6 ? s : 1.0;
    }

    private void ClearPath()
    {
        if (_pendingClip)
        {
            ApplyPendingClip();
            _pendingClip = false;
        }
        _subpaths.Clear();
        _curSub = null;
        _inPath = false;
    }

    // Computes the device-space bounding box of the current path and intersects it into the
    // graphics-state clip rectangle. Called when a path with a pending W/W* is cleared.
    private void ApplyPendingClip()
    {
        var minX = double.MaxValue; var minY = double.MaxValue;
        var maxX = double.MinValue; var maxY = double.MinValue;
        var any = false;
        foreach (var sub in _subpaths)
        foreach (var (ux, uy) in sub)
        {
            var (px, py) = UToPixel(ux, uy);
            if (px < minX) minX = px;
            if (py < minY) minY = py;
            if (px > maxX) maxX = px;
            if (py > maxY) maxY = py;
            any = true;
        }
        if (!any) return;

        var x0 = (int)Math.Floor(minX);
        var y0 = (int)Math.Floor(minY);
        var x1 = (int)Math.Ceiling(maxX);
        var y1 = (int)Math.Ceiling(maxY);

        if (_gs.ClipRect is { } c)
        {
            x0 = Math.Max(x0, c.X0); y0 = Math.Max(y0, c.Y0);
            x1 = Math.Min(x1, c.X1); y1 = Math.Min(y1, c.Y1);
        }
        _gs.ClipRect = (x0, y0, x1, y1);
        buffer.SetClip(x0, y0, x1, y1);
    }

    // Re-applies the current graphics-state clip to the buffer (after a Q restore).
    private void SyncClip()
    {
        if (_gs.ClipRect is { } c)
            buffer.SetClip(c.X0, c.Y0, c.X1, c.Y1);
        else
            buffer.ClearClip();
    }

    // True when the path is a single axis-aligned rectangle (the common case: page
    // backgrounds, table cells, rules). Returns its user-space bounds. Such paths keep the
    // fast FillRect path and avoid the scanline rasteriser.
    private bool TryGetRectangle(out double minX, out double minY, out double maxX, out double maxY)
    {
        minX = minY = maxX = maxY = 0;
        if (_subpaths.Count != 1) return false;
        var sub = _subpaths[0];
        // 4 or 5 points (5th = explicit close back to start).
        if (sub.Count is < 4 or > 5) return false;
        var distinctX = sub.Select(static p => p.X).Distinct().Count();
        var distinctY = sub.Select(static p => p.Y).Distinct().Count();
        if (distinctX != 2 || distinctY != 2) return false;
        minX = sub.Min(static p => p.X); maxX = sub.Max(static p => p.X);
        minY = sub.Min(static p => p.Y); maxY = sub.Max(static p => p.Y);
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

        var (x0, y0) = UToPixel(0,  0);
        var (x1, y1) = UToPixel(uw, uh);

        var dstX = (int)Math.Min(x0, x1);
        var dstY = (int)Math.Min(y0, y1);
        var dstW = (int)Math.Abs(x1 - x0);
        var dstH = (int)Math.Abs(y1 - y0);

        if (dstW <= 0 || dstH <= 0) return;

        BlitScaledImage(img.RgbData, img.Width, img.Height, dstX, dstY, dstW, dstH);
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

        BlitScaledImage(img.RgbData, img.Width, img.Height, dstX, dstY, dstW, dstH, img.Alpha);
    }

    // Scales an RGB image into the destination rectangle. When the image is downscaled
    // (more source than destination pixels) each destination pixel averages the source
    // box it covers, matching the area-averaging that Pdfium uses — nearest-neighbour
    // alone produces harsh aliasing and large pixel differences on small/scaled images.
    // When upscaling, falls back to nearest-neighbour sampling. When an alpha channel is
    // supplied (from an /SMask), pixels are composited over the background using it.
    private void BlitScaledImage(byte[] rgb, int srcW, int srcH, int dstX, int dstY, int dstW, int dstH, byte[]? alpha = null)
    {
        if (srcW <= 0 || srcH <= 0) return;
        var downscale = srcW > dstW || srcH > dstH;

        for (var py = 0; py < dstH; py++)
        for (var px = 0; px < dstW; px++)
        {
            byte r, g, b; int a;
            if (downscale)
            {
                // Average the source box [sx0,sx1)×[sy0,sy1) covered by this dest pixel.
                var sx0 = px * srcW / dstW;
                var sx1 = Math.Max(sx0 + 1, (px + 1) * srcW / dstW);
                var sy0 = py * srcH / dstH;
                var sy1 = Math.Max(sy0 + 1, (py + 1) * srcH / dstH);
                long sr = 0, sg = 0, sb = 0, sa = 0; var n = 0;
                for (var sy = sy0; sy < sy1 && sy < srcH; sy++)
                for (var sx = sx0; sx < sx1 && sx < srcW; sx++)
                {
                    var idx = (sy * srcW) + sx;
                    var o = idx * 3;
                    sr += rgb[o]; sg += rgb[o + 1]; sb += rgb[o + 2];
                    sa += alpha is not null ? alpha[idx] : 255;
                    n++;
                }
                if (n == 0) continue;
                r = (byte)(sr / n); g = (byte)(sg / n); b = (byte)(sb / n); a = (int)(sa / n);
            }
            else
            {
                var sx = px * srcW / dstW;
                var sy = py * srcH / dstH;
                var idx = (sy * srcW) + sx;
                var o = idx * 3;
                r = rgb[o]; g = rgb[o + 1]; b = rgb[o + 2];
                a = alpha is not null ? alpha[idx] : 255;
            }

            if (a <= 0) continue;
            if (a >= 255)
                buffer.BlitImagePixel(dstX + px, dstY + py, r, g, b);
            else
                buffer.BlendPixel(dstX + px, dstY + py, r, g, b, (byte)a);
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

    // Seeds the initial fill colour — used when rendering an uncoloured (PaintType 2)
    // tiling pattern cell, which paints in the parent's current fill colour.
    internal void SetInitialFillColor(byte r, byte g, byte b)
    {
        _gs.FillR = r; _gs.FillG = g; _gs.FillB = b;
        _gs.StrokeR = r; _gs.StrokeG = g; _gs.StrokeB = b;
    }

    private void SetFillGray(double gray)
    {
        var v = (byte)Math.Clamp((int)(gray * 255), 0, 255);
        _gs.FillR = _gs.FillG = _gs.FillB = v;
        _gs.FillIsPattern = false;
        _gs.FillShadingName = null;
        _gs.FillTilingName = null;
    }

    private void SetStrokeGray(double gray)
    {
        var v = (byte)Math.Clamp((int)(gray * 255), 0, 255);
        _gs.StrokeR = _gs.StrokeG = _gs.StrokeB = v;
    }

    private void SetFillRgb(double r, double g, double b)
    {
        _gs.FillR = (byte)Math.Clamp((int)(r * 255), 0, 255);
        _gs.FillG = (byte)Math.Clamp((int)(g * 255), 0, 255);
        _gs.FillB = (byte)Math.Clamp((int)(b * 255), 0, 255);
        _gs.FillIsPattern = false;
        _gs.FillShadingName = null;
        _gs.FillTilingName = null;
    }

    private void SetStrokeRgb(double r, double g, double b)
    {
        _gs.StrokeR = (byte)Math.Clamp((int)(r * 255), 0, 255);
        _gs.StrokeG = (byte)Math.Clamp((int)(g * 255), 0, 255);
        _gs.StrokeB = (byte)Math.Clamp((int)(b * 255), 0, 255);
    }
}
