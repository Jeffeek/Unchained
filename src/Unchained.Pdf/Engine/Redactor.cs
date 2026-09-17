using System.Globalization;
using System.Text;
using Unchained.Drawing.Primitives;
using Unchained.Drawing.Primitives.Extensions;
using Unchained.Pdf.Abstractions;
using Unchained.Pdf.Core;
using Unchained.Pdf.Document;
using Unchained.Pdf.Engine.PageResources;
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
        if (regions.Count == 0)
            return;

        var adapter = MutationHelper.Cast(nameof(document), document);
        var pageCount = adapter.Core.PageCount;

        foreach (var r in regions.Where(r => r.PageNumber < 1 || r.PageNumber > pageCount))
        {
            throw new ArgumentOutOfRangeException(
                nameof(regions),
                r.PageNumber,
                $"Region page number must be between 1 and {pageCount}."
            );
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

            var streamObj = builder.Add(
                new PdfStream(
                    new PdfDictionary(
                        new Dictionary<string, PdfObject>
                        {
                            [PdfName.Length.Value] = new PdfInteger(contentBytes.Length)
                        }
                    ),
                    contentBytes
                )
            );

            // Replace /Contents with the single rebuilt stream (resources are preserved).
            var target = existing.FirstOrDefault(obj => ReferenceEquals(obj.Value, pageDict));
            if (target is null)
                continue;

            var entries = new Dictionary<string, PdfObject>(((PdfDictionary)target.Value).Entries)
            {
                [PdfName.Contents.Value] = streamObj.ToReference()
            };
            swaps[target.ObjectNumber] = new PdfIndirectObject(target.ObjectNumber, target.Generation, new PdfDictionary(entries));
        }

        var finalObjects = existing
            .Select(o => swaps.GetValueOrDefault(o.ObjectNumber, o))
            .Concat(builder.Objects)
            .ToList();
        MutationHelper.SerializeAndReplace(adapter, finalObjects);
    }

    // Re-serializes operators, dropping text-show / image-paint ops positioned inside any
    // region, then appends opaque cover rectangles for each region.
    private static string BuildRedactedContent(IEnumerable<ContentOperator> ops, List<RedactionRegion> regions)
    {
        var sb = new StringBuilder();

        // Graphics state: CTM stack.
        var ctm = Matrix2D.Identity();
        var ctmStack = new Stack<double[]>();

        // Text state.
        var tm = Matrix2D.Identity();
        var tlm = Matrix2D.Identity();
        var leading = 0.0;

        foreach (var op in ops)
        {
            switch (op.Name)
            {
                case "q":
                    ctmStack.Push((double[])ctm.Clone());
                break;
                case "Q":
                    if (ctmStack.Count > 0)
                        ctm = ctmStack.Pop();
                break;
                case "cm" when op.Operands.Count >= 6:
                    ctm = Matrix2D.Multiply(ReadMatrix(op), ctm);
                break;
                case "BT":
                    tm = Matrix2D.Identity();
                    tlm = Matrix2D.Identity();
                break;
                case "Tf" when op.Operands.Count >= 2:
                    _ = op.Operands[1].ReadIntOrReal();
                break;
                case "TL" when op.Operands.Count >= 1:
                    leading = op.Operands[0].ReadIntOrReal();
                break;
                case "Td" when op.Operands.Count >= 2:
                    tlm = Matrix2D.Multiply(Matrix2D.Translate(op.Operands[0].ReadIntOrReal(), op.Operands[1].ReadIntOrReal()), tlm);
                    tm = (double[])tlm.Clone();
                break;
                case "TD" when op.Operands.Count >= 2:
                    leading = -op.Operands[1].ReadIntOrReal();
                    tlm = Matrix2D.Multiply(Matrix2D.Translate(op.Operands[0].ReadIntOrReal(), op.Operands[1].ReadIntOrReal()), tlm);
                    tm = (double[])tlm.Clone();
                break;
                case "Tm" when op.Operands.Count >= 6:
                    tm = ReadMatrix(op);
                    tlm = (double[])tm.Clone();
                break;
                case "T*":
                    tlm = Matrix2D.Multiply(Matrix2D.Translate(0, -leading), tlm);
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
                    var (x, y) = Matrix2D.Transform(Matrix2D.Multiply(tm, ctm), 0, 0);
                    if (InAnyRegion(regions, x, y))
                        drop = true;
                    break;
                }
                case "Do":
                {
                    // Image/form placement origin = CTM applied to the unit square's centre.
                    var (x, y) = Matrix2D.Transform(ctm, 0.5, 0.5);
                    if (InAnyRegion(regions, x, y))
                        drop = true;
                    break;
                }
            }

            if (!drop)
                WriteOperator(sb, op);

            // Advance the text line for the show-with-newline operators even if dropped,
            // so subsequent text keeps its position.
            if (op.Name is not ("'" or "\""))
                continue;

            tlm = Matrix2D.Multiply(Matrix2D.Translate(0, -leading), tlm);
            tm = (double[])tlm.Clone();
        }

        // Append opaque cover rectangles (reset to base coordinate space with q/Q).
        foreach (var r in regions)
        {
            var (cr, cg, cb) = r.FillColor;
            _ = sb.Append("q ").Append(F(cr)).Append(' ').Append(F(cg)).Append(' ').Append(F(cb)).Append(" rg ");
            _ = sb.Append(F(r.X))
                .Append(' ')
                .Append(F(r.Y))
                .Append(' ')
                .Append(F(r.Width))
                .Append(' ')
                .Append(F(r.Height))
                .Append(" re f Q\n");
        }

        return sb.ToString();
    }

    private static bool InAnyRegion(IEnumerable<RedactionRegion> regions, double x, double y) =>
        regions.Any(r => r.Contains(x, y));

    // ── Operator serialization ────────────────────────────────────────────────────

    private static void WriteOperator(StringBuilder sb, ContentOperator op)
    {
        foreach (var operand in op.Operands)
        {
            WriteOperand(sb, operand);
            _ = sb.Append(' ');
        }

        _ = sb.Append(op.Name).Append('\n');
    }

    private static void WriteOperand(StringBuilder sb, PdfObject o)
    {
        switch (o)
        {
            case PdfInteger i:
                _ = sb.Append(i.Value.ToString(CultureInfo.InvariantCulture));
            break;
            case PdfReal r:
                _ = sb.Append(F(r.Value));
            break;
            case PdfBoolean b:
                _ = sb.Append(b.Value ? "true" : "false");
            break;
            case PdfName n:
                _ = sb.Append('/').Append(n.Value);
            break;
            case PdfNull:
                _ = sb.Append("null");
            break;
            case PdfString s:
                WriteString(sb, s);
            break;
            case PdfArray a:
                _ = sb.Append('[');
                for (var i = 0; i < a.Count; i++)
                {
                    if (i > 0)
                        _ = sb.Append(' ');
                    WriteOperand(sb, a[i]);
                }

                _ = sb.Append(']');
            break;
            case PdfDictionary d:
                _ = sb.Append("<<");
                foreach (var (k, v) in d.Entries)
                {
                    _ = sb.Append('/').Append(k).Append(' ');
                    WriteOperand(sb, v);
                    _ = sb.Append(' ');
                }

                _ = sb.Append(">>");
            break;
            default:
                _ = sb.Append("null");
            break;
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
            if (b is >= PdfConstants.PrintableAsciiMin and <= PdfConstants.PrintableAsciiMax)
                continue;

            printable = false;
            break;
        }

        if (printable)
        {
            _ = sb.Append('(');
            foreach (var b in bytes)
            {
                if (b is (byte)'(' or (byte)')' or (byte)'\\')
                    _ = sb.Append('\\');
                _ = sb.Append((char)b);
            }

            _ = sb.Append(')');
        }
        else
        {
            _ = sb.Append('<');
            foreach (var b in bytes)
                _ = sb.Append(b.ToHex2());
            _ = sb.Append('>');
        }
    }

    private static string F(double v) => v.ToString("0.######", CultureInfo.InvariantCulture);

    // ── Matrix helpers (row-major [a b c d e f]) ──────────────────────────────────

    private static double[] ReadMatrix(ContentOperator op) =>
    [
        op.Operands[0].ReadIntOrReal(), op.Operands[1].ReadIntOrReal(), op.Operands[2].ReadIntOrReal(),
        op.Operands[3].ReadIntOrReal(), op.Operands[4].ReadIntOrReal(), op.Operands[5].ReadIntOrReal()
    ];
}
