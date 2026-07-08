using System.Text;
using Unchained.Drawing.Primitives;
using Unchained.Drawing.Text;
using Unchained.Drawing.Text.Extensions;
using Unchained.Pdf.Core;
using Unchained.Pdf.Models;
using Buffer = HarfBuzzSharp.Buffer;
using Encoding = System.Text.Encoding;

namespace Unchained.Pdf.Rendering.Proprietary.Rendering;

// Text-showing pipeline: text-matrix advance, HarfBuzz shaping, the direct-charmap and
// composite (Type0/CID) fallbacks, Type3 glyph content streams, and glyph stroke/clip.
internal sealed partial class PageRenderer
{
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

    private void ApplyHorizontalAdvance(double advance)
    {
        _gs.TextMatrix[4] += advance * _gs.Ctm[0];
        _gs.TextMatrix[5] += advance * _gs.Ctm[2];
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
        if (embeddedFontBytes is not null &&
            ((embeddedFontBytes.TryGetValue(_gs.FontResourceName, out var b) && b is not { Length: 0 }) || embeddedFontBytes.TryGetValue(_gs.FontName, out b)))
            embeddedBytes = b;

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

            var originX = _gs.TextMatrix[4] + (glyphPositions[i].XOffset / (double)TextShapingConstants.HarfBuzzFixed / scale);
            var originY = _gs.TextMatrix[5] + (glyphPositions[i].YOffset / (double)TextShapingConstants.HarfBuzzFixed / scale) + _gs.TextRise;
            var (px, py) = UToPixel(originX, originY);

            // Mode 0 (fill) and 2/4/6 (fill variants): blit the bitmap.
            if (_gs.ShouldFillText)
            {
                buffer.BlitGlyphFromFace(
                    (int)px,
                    (int)py,
                    ftFace,
                    _gs.FillR,
                    _gs.FillG,
                    _gs.FillB,
                    _gs.BlendMode
                );
            }

            // Mode 1/2/5/6 (stroke variants): stroke the glyph outline.
            if (_gs.ShouldStrokeText)
                StrokeGlyphOutline(ftFace, (int)px, (int)py);

            // Mode 4/5/6/7 (clip variants): add glyph outline to clip mask.
            if (_gs.ShouldClipText)
                ClipGlyphOutline(ftFace, (int)px, (int)py);

            var advance = ((glyphPositions[i].XAdvance / (double)TextShapingConstants.HarfBuzzFixed / scale) + _gs.CharSpace)
                          * (_gs.HorizontalScale / RenderingConstants.HorizontalScalePercent);
            // Transform advance through CTM to account for page/local rotation.
            ApplyHorizontalAdvance(advance);
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
                    buffer.BlitGlyphFromFace(
                        (int)px,
                        (int)py,
                        ftFace,
                        _gs.FillR,
                        _gs.FillG,
                        _gs.FillB,
                        _gs.BlendMode
                    );
                }

                if (_gs.TextRenderMode is 1 or 2)
                    StrokeGlyphOutline(ftFace, (int)px, (int)py);
            }

            // Glyph advance (FT_Get_Advance returns 16.16 fixed-point pixels) → user-space points.
            var rawAdvance = glyphId != 0 ? ftFace.GetAdvance(glyphId) : 0;
            var advancePts = rawAdvance != 0 ? rawAdvance / RenderingConstants.FreeTypeFixed / scale : pixelSize / scale * 0.5;

            var advance = (advancePts + _gs.CharSpace + (code == 32 ? _gs.WordSpace : 0))
                          * (_gs.HorizontalScale / RenderingConstants.HorizontalScalePercent);
            // Transform advance through CTM to account for page/local rotation.
            ApplyHorizontalAdvance(advance);
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
                    buffer.BlitGlyphFromFace(
                        (int)px,
                        (int)py,
                        ftFace,
                        _gs.FillR,
                        _gs.FillG,
                        _gs.FillB,
                        _gs.BlendMode
                    );
                }

                if (_gs.ShouldStrokeText)
                    StrokeGlyphOutline(ftFace, (int)px, (int)py);

                if (_gs.ShouldClipText)
                    ClipGlyphOutline(ftFace, (int)px, (int)py);
            }
            else
                GlyphsSkipped++;

            // Advance from /W (glyph-space units, 1000 per em) → text-space.
            // CTM rotation is applied below via the 2D advance.
            var wGlyph = info.Widths.TryGetValue(cid, out var w) ? w : info.DefaultWidth;
            var advance = ((wGlyph / RenderingConstants.CidEmUnits * _gs.FontSize) + _gs.CharSpace)
                          * (_gs.HorizontalScale / RenderingConstants.HorizontalScalePercent);
            // Transform advance through CTM to account for page/local rotation.
            ApplyHorizontalAdvance(advance);
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
                {
                    var kern = n.Value / RenderingConstants.CidEmUnits * _gs.FontSize *
                               (_gs.HorizontalScale / RenderingConstants.HorizontalScalePercent);
                    // Kerning is in text-space; transform through CTM for rotation.
                    _gs.TextMatrix[4] -= kern * _gs.Ctm[0];
                    _gs.TextMatrix[5] -= kern * _gs.Ctm[2];
                    break;
                }
                case PdfReal r:
                {
                    var kern = r.Value / RenderingConstants.CidEmUnits * _gs.FontSize *
                               (_gs.HorizontalScale / RenderingConstants.HorizontalScalePercent);
                    _gs.TextMatrix[4] -= kern * _gs.Ctm[0];
                    _gs.TextMatrix[5] -= kern * _gs.Ctm[2];
                    break;
                }
            }
        }
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
                type3Fonts
            );

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
        // Transform advance through CTM to account for page/local rotation.
        ApplyHorizontalAdvance(advance);
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
                        _gs.BlendMode
                    );
                }

                prevX = ptX;
                prevY = ptY;
                first = false;
            }

            // Close the contour back to the first point.
            if (first || contour.Length <= 0) continue;

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
                _gs.BlendMode
            );
        }
    }
}
