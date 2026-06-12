using System.Globalization;
using System.Text;
using Unchained.Pdf.Abstractions;
using Unchained.Pdf.Core;
using Unchained.Pdf.Document;
using Unchained.Pdf.Models;

namespace Unchained.Pdf.Engine;

/// <summary>
///     Default <see cref="IRedactor" /> implementation. For each page it walks the content
///     operators while tracking the CTM and text matrix, drops text-show and image-paint
///     operators whose drawing origin falls inside a redaction region, re-serializes the
///     remaining operators as the page's new (single) content stream, and appends an opaque
///     fill over each region so the area is both removed from the data and visually covered.
/// </summary>
public sealed class Redactor : IRedactor
{
    /// <inheritdoc />
    public Task RedactAsync(
        IPdfDocument document,
        IReadOnlyList<RedactionRegion> regions,
        CancellationToken ct = default
    ) => Task.Run(() => Redact(document, regions), ct);

    private static void Redact(IPdfDocument document, IReadOnlyList<RedactionRegion> regions)
    {
        if (regions.Count == 0) return;
        var adapter = MutationHelper.Cast(nameof(document), document);
        var pageCount = adapter.Core.PageCount;
        foreach (var r in regions)
        {
            if (r.PageNumber < 1 || r.PageNumber > pageCount)
            {
                throw new ArgumentOutOfRangeException(nameof(regions),
                    r.PageNumber,
                    $"Region page number must be between 1 and {pageCount}.");
            }
        }

        var existing = adapter.Core.CollectObjects();
        var maxObjNum = existing.Count > 0 ? existing.Max(static o => o.ObjectNumber) : 0;
        var builder = new ObjectGraphBuilder(maxObjNum + 1);
        var swaps = new Dictionary<int, PdfIndirectObject>();

        var byPage = regions.GroupBy(static r => r.PageNumber).ToDictionary(static g => g.Key, static g => g.ToList());

        foreach (var (pageNumber, pageRegions) in byPage)
        {
            var pageDict = adapter.Core.GetPage(pageNumber);
            var page = document.Pages[pageNumber];
            var ops = page.GetContentOperators();

            var newContent = BuildRedactedContent(ops, pageRegions);
            var contentBytes = Encoding.Latin1.GetBytes(newContent);

            var streamObj = builder.Add(new PdfStream(
                new PdfDictionary(new Dictionary<string, PdfObject>
                {
                    [PdfName.Length.Value] = new PdfInteger(contentBytes.Length)
                }),
                contentBytes));

            // Replace /Contents with the single rebuilt stream (resources are preserved).
            foreach (var obj in existing)
            {
                if (!ReferenceEquals(obj.Value, pageDict)) continue;
                var entries = new Dictionary<string, PdfObject>(((PdfDictionary)obj.Value).Entries)
                {
                    [PdfName.Contents.Value] = streamObj.ToReference()
                };
                swaps[obj.ObjectNumber] = new PdfIndirectObject(obj.ObjectNumber, obj.Generation, new PdfDictionary(entries));
                break;
            }
        }

        var finalObjects = existing
            .Select(o => swaps.GetValueOrDefault(o.ObjectNumber, o))
            .Concat(builder.Objects)
            .ToList();
        MutationHelper.SerializeAndReplace(adapter, finalObjects);
    }

    // Re-serializes operators, dropping text-show / image-paint ops positioned inside any
    // region, then appends opaque cover rectangles for each region.
    private static string BuildRedactedContent(IReadOnlyList<ContentOperator> ops, List<RedactionRegion> regions)
    {
        var sb = new StringBuilder();

        // Graphics state: CTM stack.
        var ctm = Identity();
        var ctmStack = new Stack<double[]>();

        // Text state.
        var tm = Identity();
        var tlm = Identity();
        var fontSize = 0.0;
        var leading = 0.0;

        foreach (var op in ops)
        {
            switch (op.Name)
            {
                case "q":
                    ctmStack.Push((double[])ctm.Clone());
                break;
                case "Q":
                    if (ctmStack.Count > 0) ctm = ctmStack.Pop();
                break;
                case "cm" when op.Operands.Count >= 6:
                    ctm = Mul(ReadMatrix(op), ctm);
                break;
                case "BT":
                    tm = Identity();
                    tlm = Identity();
                break;
                case "Tf" when op.Operands.Count >= 2:
                    fontSize = Num(op.Operands[1]);
                break;
                case "TL" when op.Operands.Count >= 1:
                    leading = Num(op.Operands[0]);
                break;
                case "Td" when op.Operands.Count >= 2:
                    tlm = Mul(Translate(Num(op.Operands[0]), Num(op.Operands[1])), tlm);
                    tm = (double[])tlm.Clone();
                break;
                case "TD" when op.Operands.Count >= 2:
                    leading = -Num(op.Operands[1]);
                    tlm = Mul(Translate(Num(op.Operands[0]), Num(op.Operands[1])), tlm);
                    tm = (double[])tlm.Clone();
                break;
                case "Tm" when op.Operands.Count >= 6:
                    tm = ReadMatrix(op);
                    tlm = (double[])tm.Clone();
                break;
                case "T*":
                    tlm = Mul(Translate(0, -leading), tlm);
                    tm = (double[])tlm.Clone();
                break;
            }

            // Decide whether to drop this operator.
            var drop = false;
            switch (op.Name)
            {
                case "Tj" or "TJ" or "'" or "\"":
                {
                    // Text-show origin = textMatrix × CTM applied to (0,0).
                    var (x, y) = Apply(Mul(tm, ctm), 0, 0);
                    if (InAnyRegion(regions, x, y)) drop = true;
                    break;
                }
                case "Do":
                {
                    // Image/form placement origin = CTM applied to the unit square's centre.
                    var (x, y) = Apply(ctm, 0.5, 0.5);
                    if (InAnyRegion(regions, x, y)) drop = true;
                    break;
                }
            }

            if (!drop)
                WriteOperator(sb, op);

            // Advance the text line for the show-with-newline operators even if dropped,
            // so subsequent text keeps its position.
            if (op.Name is "'" or "\"")
            {
                tlm = Mul(Translate(0, -leading), tlm);
                tm = (double[])tlm.Clone();
            }
        }

        // Append opaque cover rectangles (reset to base coordinate space with q/Q).
        foreach (var r in regions)
        {
            var (cr, cg, cb) = r.FillColor;
            sb.Append("q ").Append(F(cr)).Append(' ').Append(F(cg)).Append(' ').Append(F(cb)).Append(" rg ");
            sb.Append(F(r.X)).Append(' ').Append(F(r.Y)).Append(' ')
                .Append(F(r.Width)).Append(' ').Append(F(r.Height)).Append(" re f Q\n");
        }

        return sb.ToString();
    }

    private static bool InAnyRegion(List<RedactionRegion> regions, double x, double y)
    {
        foreach (var r in regions)
        {
            if (r.Contains(x, y))
                return true;
        }

        return false;
    }

    // ── Operator serialization ────────────────────────────────────────────────────

    private static void WriteOperator(StringBuilder sb, ContentOperator op)
    {
        foreach (var operand in op.Operands)
        {
            WriteOperand(sb, operand);
            sb.Append(' ');
        }

        sb.Append(op.Name).Append('\n');
    }

    private static void WriteOperand(StringBuilder sb, PdfObject o)
    {
        switch (o)
        {
            case PdfInteger i: sb.Append(i.Value.ToString(CultureInfo.InvariantCulture)); break;
            case PdfReal r: sb.Append(F(r.Value)); break;
            case PdfBoolean b: sb.Append(b.Value ? "true" : "false"); break;
            case PdfName n: sb.Append('/').Append(n.Value); break;
            case PdfNull: sb.Append("null"); break;
            case PdfString s: WriteString(sb, s); break;
            case PdfArray a:
                sb.Append('[');
                for (var i = 0; i < a.Count; i++)
                {
                    if (i > 0) sb.Append(' ');
                    WriteOperand(sb, a[i]);
                }

                sb.Append(']');
            break;
            case PdfDictionary d:
                sb.Append("<<");
                foreach (var (k, v) in d.Entries)
                {
                    sb.Append('/').Append(k).Append(' ');
                    WriteOperand(sb, v);
                    sb.Append(' ');
                }

                sb.Append(">>");
            break;
            default: sb.Append("null"); break;
        }
    }

    // Re-serialize text strings. Printable single-byte content is written as a literal
    // string (preserving how text extractors decode it); anything with bytes outside the
    // printable range falls back to a hex string to round-trip binary char codes safely.
    private static void WriteString(StringBuilder sb, PdfString s)
    {
        var bytes = s.GetBinaryBytes().Span;
        var printable = true;
        foreach (var b in bytes)
        {
            if (b < PdfConstants.PrintableAsciiMin || b > PdfConstants.PrintableAsciiMax)
            {
                printable = false;
                break;
            }
        }

        if (printable)
        {
            sb.Append('(');
            foreach (var b in bytes)
            {
                if (b is (byte)'(' or (byte)')' or (byte)'\\') sb.Append('\\');
                sb.Append((char)b);
            }

            sb.Append(')');
        }
        else
        {
            sb.Append('<');
            foreach (var b in bytes) sb.Append(b.ToString("X2", CultureInfo.InvariantCulture));
            sb.Append('>');
        }
    }

    private static string F(double v) => v.ToString("0.######", CultureInfo.InvariantCulture);

    // ── Matrix helpers (row-major [a b c d e f]) ──────────────────────────────────

    private static double[] Identity() => [1, 0, 0, 1, 0, 0];
    private static double[] Translate(double tx, double ty) => [1, 0, 0, 1, tx, ty];

    private static double[] ReadMatrix(ContentOperator op) =>
        [Num(op.Operands[0]), Num(op.Operands[1]), Num(op.Operands[2]), Num(op.Operands[3]), Num(op.Operands[4]), Num(op.Operands[5])];

    // m1 × m2 (apply m1 first, then m2).
    private static double[] Mul(double[] m1, double[] m2) =>
    [
        (m1[0] * m2[0]) + (m1[1] * m2[2]),
        (m1[0] * m2[1]) + (m1[1] * m2[3]),
        (m1[2] * m2[0]) + (m1[3] * m2[2]),
        (m1[2] * m2[1]) + (m1[3] * m2[3]),
        (m1[4] * m2[0]) + (m1[5] * m2[2]) + m2[4],
        (m1[4] * m2[1]) + (m1[5] * m2[3]) + m2[5]
    ];

    private static (double X, double Y) Apply(double[] m, double x, double y) =>
        ((m[0] * x) + (m[2] * y) + m[4], (m[1] * x) + (m[3] * y) + m[5]);

    private static double Num(PdfObject o) => o switch
    {
        PdfInteger i => i.Value,
        PdfReal r => r.Value,
        _ => 0
    };
}
