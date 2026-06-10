namespace Unchained.Pdf.Rendering.Rendering;

/// <summary>
/// Mutable snapshot of the PDF graphics state (ISO 32000-1 §8.4).
/// Cloned on every <c>q</c> (save) and restored on every <c>Q</c>.
/// </summary>
internal sealed class GraphicsState
{
    // Current transformation matrix [a b c d e f]
    internal double[] Ctm { get; set; } = [1, 0, 0, 1, 0, 0];

    // Fill colour (R=0, G=0, B=0 = black; A=255 = fully opaque)
    internal byte FillR { get; set; }
    internal byte FillG { get; set; }
    internal byte FillB { get; set; }
    internal byte FillA { get; set; } = 255;

    // True when the current fill colour is a tiling/shading pattern (scn/SCN with a name
    // operand). We do not render patterns yet; filling them as solid colour produces large
    // wrong dark blocks, so pattern fills are skipped instead.
    internal bool FillIsPattern { get; set; }

    // When the current fill pattern is a known axial/radial shading, its resource name;
    // null otherwise. Lets DrawFill paint the gradient instead of the grey approximation.
    internal string? FillShadingName { get; set; }

    // When the current fill pattern is a known tiling pattern (PatternType 1), its resource
    // name; null otherwise. Lets DrawFill tile the pattern cell instead of the grey fill.
    internal string? FillTilingName { get; set; }

    // Stroke colour
    internal byte StrokeR { get; set; }
    internal byte StrokeG { get; set; }
    internal byte StrokeB { get; set; }
    internal byte StrokeA { get; set; } = 255;

    internal double LineWidth { get; set; } = 1.0;

    // Line cap style (ISO 32000-1 §8.4.3.3):
    // 0 = Butt (no projection), 1 = Round, 2 = Projecting square.
    internal int LineCap { get; set; }

    // Line join style (ISO 32000-1 §8.4.3.4):
    // 0 = Miter, 1 = Round, 2 = Bevel.
    internal int LineJoin { get; set; }

    // Miter limit (ISO 32000-1 §8.4.3.5). Default 10.
    internal double MiterLimit { get; set; } = 10.0;

    // Blend mode for compositing (ISO 32000-1 §11.3.5). PDF name string, e.g. "Normal",
    // "Multiply", "Screen". Default is "Normal" (simple alpha compositing).
    internal string BlendMode { get; set; } = "Normal";

    // Per-pixel soft mask opacity (Width × Height bytes, row-major), or null when no
    // soft mask is active. Set by the `gs` operator when an ExtGState has /SMask.
    // Cleared by `gs` with /SMask /None. Cloned on `q`, restored on `Q`.
    internal byte[]? SoftMask { get; set; }

    // Dimensions of the SoftMask array (must match the RasterBuffer dimensions).
    internal int SoftMaskWidth { get; set; }
    internal int SoftMaskHeight { get; set; }

    // Dash pattern: on/off lengths in user-space units (empty = solid line). The dash phase
    // is not tracked separately; rendering approximates the pattern from segment start.
    internal double[] DashLengths { get; set; } = [];

    // Text state (reset to identity on BT)
    internal double[] TextMatrix { get; set; } = [1, 0, 0, 1, 0, 0];
    internal double[] TextLineMatrix { get; set; } = [1, 0, 0, 1, 0, 0];
    internal string FontName { get; set; } = string.Empty;
    internal double FontSize { get; set; }
    internal double CharSpace { get; set; }
    internal double WordSpace { get; set; }
    internal double HorizontalScale { get; set; } = 100;
    // Text rise (Ts): vertical shift of the text baseline in unscaled text-space units.
    internal double TextRise { get; set; }
    internal double Leading { get; set; }

    // Text rendering mode (Tr):
    // 0 = fill (default), 1 = stroke, 2 = fill+stroke, 3 = invisible,
    // 4–7 = clip variants. Mode 3 suppresses visible output.
    internal int TextRenderMode { get; set; }

    // Resource name of the current font (e.g. "F1") — distinct from FontName
    // (which is the base font name like "Helvetica").  Used to look up embedded
    // font bytes whose dictionary is keyed by resource name, not base font name.
    internal string FontResourceName { get; set; } = string.Empty;

    // Snapshot of the clip mask at the time of the last `q`. Stored here so that `Q` can
    // restore it. The actual clip lives in RasterBuffer._clipMask; this is just the saved copy.
    // Null means "no clip was active when this state was saved".
    internal byte[]? SavedClipMask { get; set; }

    internal GraphicsState Clone() =>
        new()
        {
            Ctm = (double[])Ctm.Clone(),
            FillR = FillR, FillG = FillG, FillB = FillB, FillA = FillA,
            FillIsPattern = FillIsPattern,
            FillShadingName = FillShadingName,
            FillTilingName = FillTilingName,
            StrokeR = StrokeR, StrokeG = StrokeG, StrokeB = StrokeB, StrokeA = StrokeA,
            LineWidth = LineWidth,
            LineCap = LineCap,
            LineJoin = LineJoin,
            MiterLimit = MiterLimit,
            SoftMask = SoftMask is null ? null : (byte[])SoftMask.Clone(),
            SoftMaskWidth = SoftMaskWidth,
            SoftMaskHeight = SoftMaskHeight,
            DashLengths = DashLengths,
            TextMatrix = (double[])TextMatrix.Clone(),
            TextLineMatrix = (double[])TextLineMatrix.Clone(),
            FontName = FontName,
            FontResourceName = FontResourceName,
            FontSize = FontSize,
            CharSpace = CharSpace,
            WordSpace = WordSpace,
            HorizontalScale = HorizontalScale,
            TextRise = TextRise,
            Leading = Leading,
            TextRenderMode = TextRenderMode,
            SavedClipMask = SavedClipMask is null ? null : (byte[])SavedClipMask.Clone()
        };

    // Transform a PDF user-space point through the current CTM.
    internal (double X, double Y) Transform(double x, double y)
    {
        var a = Ctm[0];
        var b = Ctm[1];
        var c = Ctm[2];
        var d = Ctm[3];
        var e = Ctm[4];
        var f = Ctm[5];

        return ((a * x) + (c * y) + e, (b * x) + (d * y) + f);
    }

    internal static double[] MultiplyMatrix(double[] m1, double[] m2) =>
        // [a1 b1 0]   [a2 b2 0]
        // [c1 d1 0] × [c2 d2 0]
        // [e1 f1 1]   [e2 f2 1]
        [
            (m1[0] * m2[0]) + (m1[1] * m2[2]),
            (m1[0] * m2[1]) + (m1[1] * m2[3]),
            (m1[2] * m2[0]) + (m1[3] * m2[2]),
            (m1[2] * m2[1]) + (m1[3] * m2[3]),
            (m1[4] * m2[0]) + (m1[5] * m2[2]) + m2[4],
            (m1[4] * m2[1]) + (m1[5] * m2[3]) + m2[5]
        ];
}
