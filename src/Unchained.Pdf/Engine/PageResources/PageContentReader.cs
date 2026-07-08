using Unchained.Pdf.Content;
using Unchained.Pdf.Core;
using Unchained.Pdf.Document;
using Unchained.Pdf.Models;
using Unchained.Pdf.Parsing.Filters;

namespace Unchained.Pdf.Engine.PageResources;

/// <summary>
///     Decodes a page's <c>/Contents</c> stream(s) and recursively inlines Form XObjects
///     referenced by <c>Do</c> operators (§7.8.1). Extracted from <see cref="PdfPageAdapter" />;
///     stateless, operating on the page dictionary and document core passed in.
/// </summary>
internal static class PageContentReader
{
    // 100 000 operators per page is a generous ceiling that covers any real PDF.
    // An expanded count above this indicates recursive or excessively large form XObjects
    // that would take too long to render; we stop expanding beyond the limit.
    private const int MaxExpandedOperatorsPerPage = 100_000;

    internal static IReadOnlyList<ContentOperator> GetContentOperators(PdfDictionary page, PdfDocumentCore core)
    {
        var contents = page[PdfName.Contents];
        if (contents is null) return [];

        var decoded = DecodeContents(core, contents);
        if (decoded.Length == 0) return [];

        var operators = ContentStreamParser.Parse(decoded);
        var resources = core.ResolveDict(page[PdfName.Resources]);
        var budget = MaxExpandedOperatorsPerPage;
        return ExpandFormXObjects(core, operators, resources, 0, ref budget);
    }

    // Recursively expands Do operators that reference /Subtype /Form XObjects.
    // Each form XObject is inlined as q [cm] <form content> Q.
    // Image Do operators are left in place for the renderer to handle.
    // `budget` is a shared counter tracking total operators emitted for this page;
    // expansion stops when it reaches MaxExpandedOperatorsPerPage.
    private static IReadOnlyList<ContentOperator> ExpandFormXObjects(
        PdfDocumentCore core,
        IReadOnlyList<ContentOperator> operators,
        PdfDictionary? resources,
        int depth,
        ref int budget
    )
    {
        if (depth >= PdfConstants.MaxFormXObjectDepth ||
            budget <= 0 || // ceiling reached — no further expansion
            !operators.Any(static op => op.Name == "Do"))
            return operators;

        var xObjDict = core.ResolveDict(resources?[PdfName.XObject]);
        var result = new List<ContentOperator>(operators.Count + 4);

        foreach (var op in operators)
        {
            if (budget <= 0)
            {
                result.Add(op); // ceiling hit — emit remaining ops unexpanded
                continue;
            }

            if (op.Name != "Do" || op.Operands.Count == 0 ||
                op.Operands[0] is not PdfName xName || xObjDict is null)
            {
                result.Add(op);
                budget--;
                continue;
            }

            var xObj = xObjDict[PdfName.Get(xName.Value)];
            var xStream = core.ResolveStream(xObj);

            if (xStream?.Dictionary.GetName(PdfName.Subtype.Value) != PdfConstants.XObjectForm)
            {
                result.Add(op); // image XObject or unresolved — leave Do intact
                budget--;
                continue;
            }

            result.Add(new ContentOperator("q", []));
            budget--;

            var matrixArr = xStream.Dictionary.Get<PdfArray>(PdfName.Matrix);
            if (matrixArr is { Count: 6 })
            {
                result.Add(new ContentOperator("cm", matrixArr.Elements.ToArray()));
                budget--;
            }

            ReadOnlyMemory<byte> formData;
            try
            {
                formData = StreamFilters.Decode(xStream);
            }
            catch
            {
                result.Add(new ContentOperator("Q", []));
                budget--;
                continue;
            }

            if (formData.Length > 0)
            {
                var formResources = core.ResolveDict(xStream.Dictionary[PdfName.Resources]) ?? resources;
                var formOps = ContentStreamParser.Parse(formData);
                result.AddRange(ExpandFormXObjects(core, formOps, formResources, depth + 1, ref budget));
            }

            result.Add(new ContentOperator("Q", []));
            budget--;
        }

        return result;
    }

    // /Contents can be a single indirect reference to a stream, or an array of them.
    // Multiple streams are treated as one continuous stream (§7.8.1).
    private static ReadOnlyMemory<byte> DecodeContents(PdfDocumentCore core, PdfObject contents)
    {
        var streams = CollectStreams(core, contents);

        return streams switch
        {
            { Count: 0 } => ReadOnlyMemory<byte>.Empty,
            { Count: 1 } => StreamFilters.Decode(streams[0]),
            _ => ConcatenateStreams(streams)
        };
    }

    private static List<PdfStream> CollectStreams(PdfDocumentCore core, PdfObject contents) =>
        contents switch
        {
            PdfIndirectReference r => TryResolveStream(core, r),
            PdfArray array => array.Elements
                .OfType<PdfIndirectReference>()
                .SelectMany(r => TryResolveStream(core, r))
                .ToList(),
            PdfStream s => [s],
            _ => []
        };

    private static List<PdfStream> TryResolveStream(PdfDocumentCore core, PdfIndirectReference r) =>
        core.ResolveIndirect(r.ObjectNumber).Value is PdfStream s ? [s] : [];

    // Decodes and concatenates multiple content streams with a newline separator
    // so that operator sequences spanning stream boundaries parse correctly (§7.8.1).
    private static ReadOnlyMemory<byte> ConcatenateStreams(IEnumerable<PdfStream> streams)
    {
        using var ms = new MemoryStream();
        foreach (var stream in streams)
        {
            ms.Write(StreamFilters.Decode(stream).Span);
            ms.WriteByte((byte)'\n');
        }

        return ms.ToArray();
    }
}
