using Unchained.Pdf.Abstractions;
using Unchained.Pdf.Core;
using Unchained.Pdf.Document;
using Unchained.Pdf.Engine.PageResources;
using Unchained.Pdf.Models;

namespace Unchained.Pdf.Engine;

/// <summary>
///     Default <see cref="IAnnotationEditor" /> implementation.
///     Annotations live in the page's <c>/Annots</c> array and are persisted via full-rewrite.
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

    /// <inheritdoc />
    public Task RemoveAnnotationAsync(
        IPdfDocument document,
        int pageNumber,
        int annotationIndex,
        CancellationToken ct = default
    ) => Task.Run(() => RemoveAnnotation(document, pageNumber, annotationIndex), ct);

    /// <inheritdoc />
    public Task UpdateAnnotationAsync(
        IPdfDocument document,
        int pageNumber,
        int annotationIndex,
        Annotation annotation,
        CancellationToken ct = default
    ) => Task.Run(() => UpdateAnnotation(document, pageNumber, annotationIndex, annotation), ct);

    private static void AddAnnotation(IPdfDocument document, int pageNumber, Annotation annotation)
    {
        var adapter = MutationHelper.Cast(nameof(document), document);
        var (existing, builder) = MutationHelper.CollectWithBuilder(adapter);

        var annotObj = builder.Add(BuildAnnotationDict(annotation));
        var targetDict = adapter.Core.GetPage(pageNumber);

        CommitPageAnnots(
            adapter,
            existing,
            targetDict,
            builder.Objects,
            current => [..current, annotObj.ToReference()]
        );
    }

    private static void RemoveAnnotation(IPdfDocument document, int pageNumber, int annotationIndex)
    {
        var adapter = MutationHelper.Cast(nameof(document), document);
        var existing = adapter.Core.CollectObjects().ToList();
        var targetDict = adapter.Core.GetPage(pageNumber);

        CommitPageAnnots(
            adapter,
            existing,
            targetDict,
            [],
            current =>
            {
                var arrayIndex = MapToArrayIndex(current, adapter.Core, annotationIndex);
                var updated = current.ToList();
                updated.RemoveAt(arrayIndex);
                return updated;
            }
        );
    }

    private static void UpdateAnnotation(IPdfDocument document, int pageNumber, int annotationIndex, Annotation annotation)
    {
        var adapter = MutationHelper.Cast(nameof(document), document);
        var (existing, builder) = MutationHelper.CollectWithBuilder(adapter);

        var annotObj = builder.Add(BuildAnnotationDict(annotation));
        var targetDict = adapter.Core.GetPage(pageNumber);

        CommitPageAnnots(
            adapter,
            existing,
            targetDict,
            builder.Objects,
            current =>
            {
                var arrayIndex = MapToArrayIndex(current, adapter.Core, annotationIndex);
                var updated = current.ToList();
                updated[arrayIndex] = annotObj.ToReference();
                return updated;
            }
        );
    }

    /// <summary>
    ///     Applies <paramref name="transform" /> to the target page's resolved <c>/Annots</c> element
    ///     list, rebuilds every object that shares the page dictionary, appends
    ///     <paramref name="extraObjects" />, and re-serialises. When the transform yields an empty
    ///     list the <c>/Annots</c> entry is removed entirely.
    /// </summary>
    private static void CommitPageAnnots(
        PdfDocumentAdapter adapter,
        IReadOnlyCollection<PdfIndirectObject> existing,
        PdfDictionary targetDict,
        IEnumerable<PdfIndirectObject> extraObjects,
        Func<List<PdfObject>, List<PdfObject>> transform
    )
    {
        var swaps = new Dictionary<int, PdfIndirectObject>();

        foreach (var obj in existing.Where(obj => ReferenceEquals(obj.Value, targetDict)))
        {
            var pd = (PdfDictionary)obj.Value;
            var current = ResolveAnnotArray(pd[PdfName.Annots], adapter.Core).ToList();
            var newAnnots = transform(current);

            var entries = new Dictionary<string, PdfObject>(pd.Entries);
            if (newAnnots.Count == 0)
                entries.Remove(PdfName.Annots.Value);
            else
                entries[PdfName.Annots.Value] = new PdfArray(newAnnots.ToArray());

            swaps[obj.ObjectNumber] = new PdfIndirectObject(
                obj.ObjectNumber,
                obj.Generation,
                new PdfDictionary(entries)
            );
        }

        var finalObjects = existing
            .Select(o => swaps.GetValueOrDefault(o.ObjectNumber, o))
            .Concat(extraObjects)
            .ToList();

        MutationHelper.SerializeAndReplace(adapter, finalObjects);
    }

    private static PdfDictionary BuildAnnotationDict(Annotation annotation)
    {
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

        return new PdfDictionary(annotEntries);
    }

    /// <summary>
    ///     Maps a caller-facing annotation index — the position among annotations returned by
    ///     <see cref="IPdfPage.GetAnnotations" /> (which skips elements that do not resolve to a
    ///     dictionary) — to the raw index within the <c>/Annots</c> element list.
    /// </summary>
    private static int MapToArrayIndex(IReadOnlyList<PdfObject> elements, PdfDocumentCore core, int annotationIndex)
    {
        if (annotationIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(annotationIndex), annotationIndex, "Annotation index must be non-negative.");

        var resolvable = -1;
        for (var i = 0; i < elements.Count; i++)
        {
            if (core.ResolveDict(elements[i]) is null) continue;

            resolvable++;
            if (resolvable == annotationIndex) return i;
        }

        throw new ArgumentOutOfRangeException(nameof(annotationIndex), annotationIndex, "Annotation index is out of range for the page.");
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
