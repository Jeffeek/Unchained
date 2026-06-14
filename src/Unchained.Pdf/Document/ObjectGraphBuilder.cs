using System.Buffers;
using Unchained.Pdf.Abstractions;
using Unchained.Pdf.Core;
using Unchained.Pdf.Engine;
using Unchained.Pdf.Writing;

namespace Unchained.Pdf.Document;

/// <summary>
///     Allocates sequential object numbers and accumulates a list of
///     <see cref="PdfIndirectObject" /> instances for use with <see cref="PdfWriter" />.
/// </summary>
internal sealed class ObjectGraphBuilder
{
    private readonly List<PdfIndirectObject> _objects = [];
    private int _next;

    internal ObjectGraphBuilder(int startAt = 1) => _next = startAt;

    // ReSharper disable once MemberCanBePrivate.Global
    internal IEnumerable<PdfIndirectObject> Objects => _objects;
    internal int MaxObjectNumber => _next - 1;

    /// <summary>Wraps <paramref name="value" /> with the next available object number.</summary>
    internal PdfIndirectObject Add(PdfObject value)
    {
        var num = _next++;
        var obj = new PdfIndirectObject(num, 0, value);
        _objects.Add(obj);

        return obj;
    }

    /// <summary>
    ///     Reserves and returns the next available object number without adding anything to the list.
    ///     Use <see cref="AddAt" /> to fill the reserved slot later.
    /// </summary>
    internal int NextNumber() => _next++;

    /// <summary>Fills a previously reserved slot without allocating a new number.</summary>
    internal PdfIndirectObject AddAt(int number, PdfObject value)
    {
        var obj = new PdfIndirectObject(number, 0, value);
        _objects.Add(obj);

        return obj;
    }

    /// <summary>
    ///     Builds a PDF byte stream from the builder's accumulated objects, parses it,
    ///     and returns a ready-to-use <see cref="Abstractions.IPdfDocument" />.
    /// </summary>
    internal static IPdfDocument Finalize(ObjectGraphBuilder builder, PdfIndirectReference rootRef)
    {
        var trailer = new PdfDictionary(new Dictionary<string, PdfObject>
        {
            [PdfName.Size.Value] = new PdfInteger(builder.MaxObjectNumber + 1),
            [PdfName.Root.Value] = rootRef
        });

        return SerializeToDocument(builder.Objects, trailer);
    }

    /// <summary>
    ///     Builds a PDF byte stream from a pre-assembled object list and trailer,
    ///     parses it, and returns a ready-to-use <see cref="Abstractions.IPdfDocument" />.
    /// </summary>
    internal static IPdfDocument SerializeToDocument(IEnumerable<PdfIndirectObject> objects, PdfDictionary trailer)
    {
        var sorted = objects.OrderBy(static o => o.ObjectNumber).ToList();
        var buffer = new ArrayBufferWriter<byte>();
        using var writer = new PdfWriter(buffer);
        writer.Write(sorted, trailer);
        var core = PdfDocumentCore.Parse(buffer.WrittenMemory);
        return new PdfDocumentAdapter(core);
    }
}
