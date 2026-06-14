using Unchained.Pdf.Core;
using Unchained.Pdf.Document;

namespace Unchained.Pdf.Engine;

/// <summary>
///     Shared serialisation step for all in-place document mutation operations:
///     builds a new trailer, calls <see cref="ObjectGraphBuilder.SerializeToDocument" />,
///     then swaps the adapter's core via <see cref="PdfDocumentAdapter.ReplaceCore" />.
/// </summary>
internal static class MutationHelper
{
    internal static void SerializeAndReplace(
        PdfDocumentAdapter adapter,
        List<PdfIndirectObject> objects
    )
    {
        var totalMax = objects.Max(static o => o.ObjectNumber);
        var rootRef = adapter.Core.Trailer[PdfName.Root] as PdfIndirectReference ?? throw new PdfException("Trailer missing /Root.");
        var trailer = new PdfDictionary(
            new Dictionary<string, PdfObject>
            {
                [PdfName.Size.Value] = new PdfInteger(totalMax + 1),
                [PdfName.Root.Value] = rootRef
            }
        );
        var newDoc = (PdfDocumentAdapter)ObjectGraphBuilder.SerializeToDocument(objects, trailer);
        adapter.ReplaceCore(newDoc.Core);
    }

    internal static PdfDocumentAdapter Cast(string paramName, object document) =>
        document as PdfDocumentAdapter
        ?? throw new ArgumentException(
            $"Document was not created by Unchained. Expected {nameof(PdfDocumentAdapter)}.",
            paramName
        );
}
