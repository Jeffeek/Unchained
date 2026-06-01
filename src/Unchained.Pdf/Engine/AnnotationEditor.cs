using Unchained.Pdf.Abstractions;
using Unchained.Pdf.Core;
using Unchained.Pdf.Document;
using Unchained.Pdf.Models;

namespace Unchained.Pdf.Engine;

/// <summary>
/// Default <see cref="IAnnotationEditor"/> implementation.
/// Annotations are appended to the page's <c>/Annots</c> array and persisted via full-rewrite.
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
        var adapter = document as PdfDocumentAdapter
                      ?? throw new ArgumentException(
                          $"Document was not created by Unchained. Expected {nameof(PdfDocumentAdapter)}, got {document.GetType().Name}.",
                          nameof(document));

        var existing = adapter.Core.CollectObjects();
        var maxObjNum = existing.Count > 0 ? existing.Max(static o => o.ObjectNumber) : 0;
        var builder = new ObjectGraphBuilder(startAt: maxObjNum + 1);

        // Build annotation dict.
        var annotEntries = new Dictionary<string, PdfObject>
        {
            [PdfName.Type.Value] = PdfName.Get("Annot"),
            [PdfName.Subtype.Value] = PdfName.Get(annotation.Subtype.ToString()),
            [PdfName.Rect.Value] = new PdfArray([
                new PdfReal(annotation.X),
                new PdfReal(annotation.Y),
                new PdfReal(annotation.X + annotation.Width),
                new PdfReal(annotation.Y + annotation.Height)
            ])
        };
        if (annotation.Contents is not null)
            annotEntries[PdfName.Contents.Value] = PdfString.FromLatin1(annotation.Contents);
        if (annotation.Color is { Length: 3 } c)
            annotEntries["C"] = new PdfArray([new PdfReal(c[0]), new PdfReal(c[1]), new PdfReal(c[2])]);

        var annotObj = builder.Add(new PdfDictionary(annotEntries));

        // Find target page dict object.
        var targetDict = adapter.Core.GetPage(pageNumber);
        var swaps = new Dictionary<int, PdfIndirectObject>();

        foreach (var obj in existing)
        {
            if (!ReferenceEquals(obj.Value, targetDict)) continue;

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

        var totalMax = finalObjects.Max(static o => o.ObjectNumber);
        var rootRef = adapter.Core.Trailer[PdfName.Root] as PdfIndirectReference ?? throw new PdfException("Trailer missing /Root.");
        var trailer = new PdfDictionary(new Dictionary<string, PdfObject>
        {
            [PdfName.Size.Value] = new PdfInteger(totalMax + 1),
            [PdfName.Root.Value] = rootRef
        });

        var newDoc = (PdfDocumentAdapter)ObjectGraphBuilder.SerializeToDocument(finalObjects, trailer);
        adapter.ReplaceCore(newDoc.Core);
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
