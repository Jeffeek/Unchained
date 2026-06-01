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

    // Stroke colour
    internal byte StrokeR { get; set; }
    internal byte StrokeG { get; set; }
    internal byte StrokeB { get; set; }
    internal byte StrokeA { get; set; } = 255;

    internal double LineWidth { get; set; } = 1.0;

    // Text state (reset to identity on BT)
    internal double[] TextMatrix { get; set; } = [1, 0, 0, 1, 0, 0];
    internal double[] TextLineMatrix { get; set; } = [1, 0, 0, 1, 0, 0];
    internal string FontName { get; set; } = "";
    internal double FontSize { get; set; }
    internal double CharSpace { get; set; }
    internal double WordSpace { get; set; }
    internal double HorizScale { get; set; } = 100;
    internal double Leading { get; set; }

    internal GraphicsState Clone() =>
        new()
        {
            Ctm = (double[])Ctm.Clone(),
            FillR = FillR, FillG = FillG, FillB = FillB, FillA = FillA,
            StrokeR = StrokeR, StrokeG = StrokeG, StrokeB = StrokeB, StrokeA = StrokeA,
            LineWidth = LineWidth,
            TextMatrix = (double[])TextMatrix.Clone(),
            TextLineMatrix = (double[])TextLineMatrix.Clone(),
            FontName = FontName,
            FontSize = FontSize,
            CharSpace = CharSpace,
            WordSpace = WordSpace,
            HorizScale = HorizScale,
            Leading = Leading
        };

    // Transform a PDF user-space point through the current CTM.
    internal (double X, double Y) Transform(double x, double y)
    {
        var a = Ctm[0]; var b = Ctm[1]; var c = Ctm[2];
        var d = Ctm[3]; var e = Ctm[4]; var f = Ctm[5];
        return (a * x + c * y + e, b * x + d * y + f);
    }

    internal static double[] MultiplyMatrix(double[] m1, double[] m2)
    {
        // [a1 b1 0]   [a2 b2 0]
        // [c1 d1 0] × [c2 d2 0]
        // [e1 f1 1]   [e2 f2 1]
        return [
            m1[0] * m2[0] + m1[1] * m2[2],
            m1[0] * m2[1] + m1[1] * m2[3],
            m1[2] * m2[0] + m1[3] * m2[2],
            m1[2] * m2[1] + m1[3] * m2[3],
            m1[4] * m2[0] + m1[5] * m2[2] + m2[4],
            m1[4] * m2[1] + m1[5] * m2[3] + m2[5]
        ];
    }
}
