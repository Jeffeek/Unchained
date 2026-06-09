using Unchained.Pdf.Core;
using Unchained.Pdf.Models;

namespace Unchained.Pdf.Content;

/// <summary>
/// Walks a list of <see cref="ContentOperator"/> instances and extracts positioned text
/// spans according to the PDF text object state machine (ISO 32000-1 §9.3–9.4).
/// Tracks the CTM (via <c>q</c>/<c>Q</c>/<c>cm</c>) and maps each text origin through it, so
/// translated / rotated / scaled coordinate systems position text correctly. Span widths and
/// font sizes are reported in device space (scaled by the CTM's average linear scale).
/// </summary>
internal static class TextExtractor
{
    // Spans whose Y coordinates differ by less than this threshold are merged into one line.
    private const double LineThreshold = 2.0;

    /// <summary>
    /// Extracts text spans from <paramref name="operators"/>, resolving font resource names
    /// from <paramref name="fontNameMap"/> (resource name → base font name).
    /// Returns spans sorted by reading order (Y descending, then X ascending).
    /// </summary>
    internal static IReadOnlyList<TextSpan> Extract(IEnumerable<ContentOperator> operators, IReadOnlyDictionary<string, string> fontNameMap)
    {
        var spans = new List<TextSpan>();

        // Text state
        var tmA = 1.0;
        var tmB = 0.0;
        var tmC = 0.0;
        var tmD = 1.0;
        var tmE = 0.0;
        var tmF = 0.0; // text matrix (a b c d e f)
        var tlmA = 1.0;
        var tlmB = 0.0;
        var tlmC = 0.0;
        var tlmD = 1.0;
        var tlmE = 0.0;
        var tlmF = 0.0; // text line matrix

        var fontSize = 0.0;
        var fontName = string.Empty;
        var tc = 0.0; // character spacing
        var tw = 0.0; // word spacing
        var th = 100.0; // horizontal scaling (%)
        var tl = 0.0; // leading
        var inText = false;

        // Current transformation matrix [a b c d e f] and its save/restore stack.
        // Text is positioned by Trm = TextMatrix × CTM, so the CTM must be tracked to place
        // text correctly on rotated/scaled/translated pages (ISO 32000-1 §9.4.4).
        var ctm = new[] { 1.0, 0, 0, 1, 0, 0 };
        var ctmStack = new Stack<double[]>();

        foreach (var op in operators)
        {
            switch (op.Name)
            {
                // ── Graphics state save/restore — Q restores the CTM (and clip, colour…),
                // but NOT the text state, which we track independently.
                case "q":
                    ctmStack.Push((double[])ctm.Clone());
                    break;
                case "Q":
                    if (ctmStack.Count > 0) ctm = ctmStack.Pop();
                    break;
                case "cm" when op.Operands.Count >= 6:
                {
                    double[] m =
                    [
                        ReadNumber(op.Operands[0]), ReadNumber(op.Operands[1]),
                        ReadNumber(op.Operands[2]), ReadNumber(op.Operands[3]),
                        ReadNumber(op.Operands[4]), ReadNumber(op.Operands[5])
                    ];
                    ctm = MultiplyMatrix(m, ctm); // cm pre-concatenates: CTM = m × CTM
                    break;
                }

                // ── Text object ──────────────────────────────────────────────
                case "BT":
                {
                    inText = true;
                    // Reset text matrix to identity at start of each text object.
                    tmA = 1;
                    tmB = 0;
                    tmC = 0;
                    tmD = 1;
                    tmE = 0;
                    tmF = 0;
                    tlmA = 1;
                    tlmB = 0;
                    tlmC = 0;
                    tlmD = 1;
                    tlmE = 0;
                    tlmF = 0;
                    break;
                }
                case "ET":
                {
                    inText = false;
                    break;
                }

                // ── Font selection ────────────────────────────────────────────
                case "Tf" when inText && op.Operands.Count >= 2:
                {
                    var resName = (op.Operands[0] as PdfName)?.Value ?? string.Empty;
                    fontName = fontNameMap.GetValueOrDefault(resName, resName);
                    fontSize = ReadNumber(op.Operands[1]);
                    break;
                }

                // ── Text state parameters ─────────────────────────────────────
                case "Tc" when op.Operands.Count >= 1:
                {
                    tc = ReadNumber(op.Operands[0]);
                    break;
                }
                case "Tw" when op.Operands.Count >= 1:
                {
                    tw = ReadNumber(op.Operands[0]);
                    break;
                }
                case "Tz" when op.Operands.Count >= 1:
                {
                    th = ReadNumber(op.Operands[0]);
                    break;
                }
                case "TL" when op.Operands.Count >= 1:
                {
                    tl = ReadNumber(op.Operands[0]);
                    break;
                }

                // ── Text positioning ──────────────────────────────────────────
                case "Td" when inText && op.Operands.Count >= 2:
                {
                    var tx = ReadNumber(op.Operands[0]);
                    var ty = ReadNumber(op.Operands[1]);
                    MoveLine(
                        tx,
                        ty,
                        ref tlmA,
                        ref tlmB,
                        ref tlmC,
                        ref tlmD,
                        ref tlmE,
                        ref tlmF,
                        ref tmA,
                        ref tmB,
                        ref tmC,
                        ref tmD,
                        ref tmE,
                        ref tmF
                    );
                    break;
                }
                case "TD" when inText && op.Operands.Count >= 2:
                {
                    var tx = ReadNumber(op.Operands[0]);
                    var ty = ReadNumber(op.Operands[1]);
                    tl = -ty;
                    MoveLine(
                        tx,
                        ty,
                        ref tlmA,
                        ref tlmB,
                        ref tlmC,
                        ref tlmD,
                        ref tlmE,
                        ref tlmF,
                        ref tmA,
                        ref tmB,
                        ref tmC,
                        ref tmD,
                        ref tmE,
                        ref tmF
                    );
                    break;
                }
                case "T*" when inText:
                {
                    MoveLine(
                        0,
                        -tl,
                        ref tlmA,
                        ref tlmB,
                        ref tlmC,
                        ref tlmD,
                        ref tlmE,
                        ref tlmF,
                        ref tmA,
                        ref tmB,
                        ref tmC,
                        ref tmD,
                        ref tmE,
                        ref tmF
                    );
                    break;
                }
                case "Tm" when inText && op.Operands.Count >= 6:
                {
                    // Tm a b c d e f — set both text and line matrix directly.
                    tmA = ReadNumber(op.Operands[0]);
                    tmB = ReadNumber(op.Operands[1]);
                    tmC = ReadNumber(op.Operands[2]);
                    tmD = ReadNumber(op.Operands[3]);
                    tmE = ReadNumber(op.Operands[4]);
                    tmF = ReadNumber(op.Operands[5]);
                    tlmA = tmA;
                    tlmB = tmB;
                    tlmC = tmC;
                    tlmD = tmD;
                    tlmE = tmE;
                    tlmF = tmF;
                    break;
                }

                // ── Text showing ──────────────────────────────────────────────
                case "Tj" when inText && op.Operands.Count >= 1:
                {
                    if (op.Operands[0] is PdfString s)
                    {
                        ShowString(
                            s.Bytes.Span,
                            fontName,
                            fontSize,
                            tc,
                            tw,
                            th,
                            ref tmE,
                            ref tmF,
                            ctm,
                            spans
                        );
                    }

                    break;
                }
                case "'" when inText && op.Operands.Count >= 1:
                {
                    MoveLine(
                        0,
                        -tl,
                        ref tlmA,
                        ref tlmB,
                        ref tlmC,
                        ref tlmD,
                        ref tlmE,
                        ref tlmF,
                        ref tmA,
                        ref tmB,
                        ref tmC,
                        ref tmD,
                        ref tmE,
                        ref tmF
                    );
                    if (op.Operands[0] is PdfString s2)
                    {
                        ShowString(
                            s2.Bytes.Span,
                            fontName,
                            fontSize,
                            tc,
                            tw,
                            th,
                            ref tmE,
                            ref tmF,
                            ctm,
                            spans
                        );
                    }

                    break;
                }
                case "\"" when inText && op.Operands.Count >= 3:
                {
                    tw = ReadNumber(op.Operands[0]);
                    tc = ReadNumber(op.Operands[1]);
                    MoveLine(
                        0,
                        -tl,
                        ref tlmA,
                        ref tlmB,
                        ref tlmC,
                        ref tlmD,
                        ref tlmE,
                        ref tlmF,
                        ref tmA,
                        ref tmB,
                        ref tmC,
                        ref tmD,
                        ref tmE,
                        ref tmF
                    );
                    if (op.Operands[2] is PdfString s3)
                    {
                        ShowString(
                            s3.Bytes.Span,
                            fontName,
                            fontSize,
                            tc,
                            tw,
                            th,
                            ref tmE,
                            ref tmF,
                            ctm,
                            spans
                        );
                    }

                    break;
                }
                case "TJ" when inText && op.Operands.Count >= 1:
                {
                    if (op.Operands[0] is PdfArray arr)
                    {
                        ShowArray(
                            arr,
                            fontName,
                            fontSize,
                            tc,
                            tw,
                            th,
                            ref tmE,
                            ref tmF,
                            ctm,
                            spans
                        );
                    }

                    break;
                }
            }
        }

        return SortByReadingOrder(spans);
    }

    // ── Text matrix helpers ───────────────────────────────────────────────────

    // Td tx ty: Tlm = [1 0 0 1 tx ty] × Tlm; Tm = Tlm
    private static void MoveLine(
        double tx,
        double ty,
        ref double tlmA,
        ref double tlmB,
        ref double tlmC,
        ref double tlmD,
        ref double tlmE,
        ref double tlmF,
        // ReSharper disable RedundantAssignment
        ref double tmA,
        ref double tmB,
        ref double tmC,
        ref double tmD,
        ref double tmE,
        ref double tmF
        // ReSharper restore RedundantAssignment
    )
    {
        // Multiply [1 0 0 1 tx ty] × Tlm (column-major 3×3)
        // Result: a'=tlmA, b'=tlmB, c'=tlmC, d'=tlmD
        //         e' = tx*tlmA + ty*tlmC + tlmE
        //         f' = tx*tlmB + ty*tlmD + tlmF
        var newE = (tx * tlmA) + (ty * tlmC) + tlmE;
        var newF = (tx * tlmB) + (ty * tlmD) + tlmF;
        tlmE = newE;
        tlmF = newF;
        tmA = tlmA;
        tmB = tlmB;
        tmC = tlmC;
        tmD = tlmD;
        tmE = tlmE;
        tmF = tlmF;
    }

    // ── String rendering ──────────────────────────────────────────────────────

    private static void ShowString(
        ReadOnlySpan<byte> bytes,
        string fontName,
        double fontSize,
        double tc,
        double tw,
        double th,
        ref double tmE,
        ref double tmF,
        double[] ctm,
        ICollection<TextSpan> spans
    )
    {
        if (bytes.IsEmpty || fontSize <= 0)
            return;

        var text = System.Text.Encoding.Latin1.GetString(bytes);
        // Text origin (tmE, tmF) is in user space; map through the CTM to device space so
        // translated / rotated / scaled coordinate systems position text correctly.
        var startX = (tmE * ctm[0]) + (tmF * ctm[2]) + ctm[4];
        var startY = (tmE * ctm[1]) + (tmF * ctm[3]) + ctm[5];
        var totalAdvance = 0.0;

        foreach (var ch in text)
        {
            var w = Standard14Widths.Get(fontName, ch);
            var advance = ((w / 1000.0 * fontSize) + tc) * (th / 100.0);
            if (ch == ' ') advance += tw * (th / 100.0);
            totalAdvance += advance;
        }

        tmE += totalAdvance;

        if (text.Length > 0)
        {
            // Scale the reported width and font size by the CTM's average linear scale so
            // downstream consumers see device-space magnitudes.
            var ctmScale = CtmScale(ctm);
            spans.Add(new TextSpan(
                text,
                startX,
                startY,
                totalAdvance * ctmScale,
                fontSize * ctmScale,
                fontName)
            );
        }
    }

    private static void ShowArray(
        PdfArray arr,
        string fontName,
        double fontSize,
        double tc,
        double tw,
        double th,
        ref double tmE,
        ref double tmF,
        double[] ctm,
        ICollection<TextSpan> spans
    )
    {
        foreach (var elem in arr.Elements)
        {
            switch (elem)
            {
                case PdfString s:
                    ShowString(
                        s.Bytes.Span,
                        fontName,
                        fontSize,
                        tc,
                        tw,
                        th,
                        ref tmE,
                        ref tmF,
                        ctm,
                        spans
                    );
                break;
                case PdfInteger n:
                    // Negative = kern (move text position); positive = gap.
                    // Displacement is in 1/1000 em, moves in opposite direction to text flow.
                    tmE -= n.Value / 1000.0 * fontSize * (th / 100.0);
                break;
                case PdfReal r:
                    tmE -= r.Value / 1000.0 * fontSize * (th / 100.0);
                break;
            }
        }
    }

    // ── Reading order sort ────────────────────────────────────────────────────

    private static IReadOnlyList<TextSpan> SortByReadingOrder(IEnumerable<TextSpan> spans) =>
        spans
            .OrderByDescending(static s => s.Y)
            .ThenBy(static s => s.X)
            .ToList();

    // ── Utilities ─────────────────────────────────────────────────────────────

    private static double ReadNumber(PdfObject obj) => obj switch
    {
        PdfInteger i => i.Value,
        PdfReal r => r.Value,
        _ => 0
    };

    // Row-major [a b c d e f] affine matrix multiply: result = m1 × m2 (apply m1 first).
    private static double[] MultiplyMatrix(double[] m1, double[] m2) =>
    [
        (m1[0] * m2[0]) + (m1[1] * m2[2]),
        (m1[0] * m2[1]) + (m1[1] * m2[3]),
        (m1[2] * m2[0]) + (m1[3] * m2[2]),
        (m1[2] * m2[1]) + (m1[3] * m2[3]),
        (m1[4] * m2[0]) + (m1[5] * m2[2]) + m2[4],
        (m1[4] * m2[1]) + (m1[5] * m2[3]) + m2[5]
    ];

    // Average linear scale of a CTM (geometric mean of the two basis-vector magnitudes).
    private static double CtmScale(double[] m)
    {
        var sx = Math.Sqrt((m[0] * m[0]) + (m[1] * m[1]));
        var sy = Math.Sqrt((m[2] * m[2]) + (m[3] * m[3]));
        var s = Math.Sqrt(sx * sy);
        return s > 1e-6 ? s : 1.0;
    }

    // ── Plain text reconstruction from sorted spans ───────────────────────────

    /// <summary>
    /// Joins sorted <paramref name="spans"/> into a plain string, inserting
    /// newlines between distinct lines and spaces between spans on the same line.
    /// </summary>
    internal static string SpansToText(IReadOnlyList<TextSpan> spans)
    {
        if (spans.Count == 0) return string.Empty;

        var sb = new System.Text.StringBuilder();
        var prevY = spans[0].Y;
        var prevEndX = spans[0].X + spans[0].Width;

        for (var i = 0; i < spans.Count; i++)
        {
            var span = spans[i];

            if (i > 0)
            {
                if (Math.Abs(span.Y - prevY) > LineThreshold)
                    sb.Append('\n');
                else if (span.X > prevEndX + 1.0)
                    sb.Append(' ');
            }

            sb.Append(span.Text);
            prevY = span.Y;
            prevEndX = span.X + span.Width;
        }

        return sb.ToString();
    }
}
