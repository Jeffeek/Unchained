using Unchained.Pdf.Abstractions;
using Unchained.Pdf.Core;
using Unchained.Pdf.Document;
using Unchained.Pdf.Models;

namespace Unchained.Pdf.Engine;

/// <summary>
///     Default <see cref="IAnnotationEditor" /> implementation.
///     Annotations are appended to the page's <c>/Annots</c> array and persisted via full-rewrite.
/// </summary>
public sealed class AnnotationEditor : IAnnotationEditor
{
    /// <inheritdoc />
    public Task AddAnnotationAsync(
        IPdfDocument document,
        int pageNumber,
        Annotation annotation,
        CancellationToken ct = default
    ) => Task.Run(() => AddAnnotation(document, pageNumber, annotation), ct);

    private static void AddAnnotation(IPdfDocument document, int pageNumber, Annotation annotation)
    {
        var adapter = MutationHelper.Cast(nameof(document), document);

        var (existing, builder) = MutationHelper.CollectWithBuilder(adapter);

        // Build annotation dict.
        var annotEntries = new Dictionary<string, PdfObject>
        {
            [PdfName.Type.Value] = PdfName.Annot,
            [PdfName.Subtype.Value] = PdfName.Get(annotation.Subtype.ToString()),
            [PdfName.Rect.Value] = new PdfArray(
                [
                    new PdfReal(annotation.X),
                    new PdfReal(annotation.Y),
                    new PdfReal(annotation.X + annotation.Width),
                    new PdfReal(annotation.Y + annotation.Height)
                ]
            )
        };
        if (annotation.Contents is not null)
            annotEntries[PdfName.Contents.Value] = PdfString.FromLatin1(annotation.Contents);
        if (annotation.Color is { Length: 3 } c)
            annotEntries["C"] = new PdfArray([new PdfReal(c[0]), new PdfReal(c[1]), new PdfReal(c[2])]);

        var annotObj = builder.Add(new PdfDictionary(annotEntries));

        // Find target page dict object.
        var targetDict = adapter.Core.GetPage(pageNumber);
        var swaps = new Dictionary<int, PdfIndirectObject>();

        foreach (var obj in existing.Where(obj => ReferenceEquals(obj.Value, targetDict)))
        {
            var pd = (PdfDictionary)obj.Value;
            var existingAnnotations = ResolveAnnotArray(pd[PdfName.Annots], adapter.Core);
            var allAnnotations = existingAnnotations.Append(annotObj.ToReference()).ToArray();

            var entries = new Dictionary<string, PdfObject>(pd.Entries)
            {
                [PdfName.Annots.Value] = new PdfArray(allAnnotations)
            };
            swaps[obj.ObjectNumber] = new PdfIndirectObject(
                obj.ObjectNumber,
                obj.Generation,
                new PdfDictionary(entries)
            );
        }

        var finalObjects = existing
            .Select(o => swaps.GetValueOrDefault(o.ObjectNumber, o))
            .Concat(builder.Objects)
            .ToList();

        MutationHelper.SerializeAndReplace(adapter, finalObjects);
    }

    private static IEnumerable<PdfObject> ResolveAnnotArray(PdfObject? obj, PdfDocumentCore core)
    {
        var arr = obj switch
        {
            PdfArray a => a,
            PdfIndirectReference r => core.ResolveIndirect(r.ObjectNumber).Value as PdfArray,
            _ => null
        };
        return arr?.Elements ?? [];
    }
}
