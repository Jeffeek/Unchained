using Unchained.Pdf.Abstractions;
using Unchained.Pdf.Core;
using Unchained.Pdf.Document;
using Unchained.Pdf.Models;

namespace Unchained.Pdf.Engine;

/// <summary>
///     Default <see cref="IBookmarkEditor" /> implementation.
///     Builds a flat or nested <c>/Outlines</c> tree and replaces the catalog entry.
/// </summary>
public sealed class BookmarkEditor : IBookmarkEditor
{
    /// <inheritdoc />
    public Task SetBookmarksAsync(
        IPdfDocument document,
        IReadOnlyList<Bookmark> bookmarks,
        CancellationToken ct = default
    ) => Task.Run(() => SetBookmarks(document, bookmarks), ct);

    private static void SetBookmarks(IPdfDocument document, IReadOnlyList<Bookmark> bookmarks)
    {
        var adapter = document as PdfDocumentAdapter
                      ?? throw new ArgumentException(
                          $"Document was not created by Unchained. Expected {nameof(PdfDocumentAdapter)}, got {document.GetType().Name}.",
                          nameof(document));

        var existing = adapter.Core.CollectObjects();
        var maxObjNum = existing.Count > 0 ? existing.Max(static o => o.ObjectNumber) : 0;
        var builder = new ObjectGraphBuilder(maxObjNum + 1);

        // Build the page object-number lookup: page N → object number.
        var pageObjNums = BuildPageObjectNumbers(existing, adapter.Core);

        // Build outline tree; get catalog reference.
        var catalogObj = existing.First(static o =>
            o.Value is PdfDictionary d && d.GetName(PdfName.Type.Value) == "Catalog");

        PdfObject outlinesEntry;
        if (bookmarks.Count == 0)
        {
            // Remove /Outlines — rebuild catalog without it.
            outlinesEntry = PdfNull.Instance; // placeholder; excluded below
        }
        else
        {
            var rootNum = builder.NextNumber();
            var rootRef = new PdfIndirectReference(rootNum, 0);
            var itemRefs = BuildOutlineItems(builder, bookmarks, rootRef, pageObjNums);
            builder.AddAt(
                rootNum,
                new PdfDictionary(new Dictionary<string, PdfObject>
                {
                    [PdfName.Type.Value] = PdfName.Outlines,
                    [PdfName.First.Value] = itemRefs[0],
                    [PdfName.Last.Value] = itemRefs[^1],
                    [PdfName.Count.Value] = new PdfInteger(CountAll(bookmarks))
                }));
            outlinesEntry = rootRef;
        }

        // Rebuild catalog with updated /Outlines.
        var catDict = (PdfDictionary)catalogObj.Value;
        var catEntries = new Dictionary<string, PdfObject>(catDict.Entries);
        if (outlinesEntry is PdfNull)
            catEntries.Remove(PdfName.Outlines.Value);
        else
            catEntries[PdfName.Outlines.Value] = outlinesEntry;

        var rebuiltCatalog = new PdfIndirectObject(catalogObj.ObjectNumber, catalogObj.Generation, new PdfDictionary(catEntries));

        var finalObjects = existing
            .Select(o => o.ObjectNumber == catalogObj.ObjectNumber ? rebuiltCatalog : o)
            .Concat(builder.Objects)
            .ToList();

        var totalMax = finalObjects.Max(static o => o.ObjectNumber);
        var rootRef2 = catalogObj.ToReference();
        var trailer = new PdfDictionary(new Dictionary<string, PdfObject>
        {
            [PdfName.Size.Value] = new PdfInteger(totalMax + 1),
            [PdfName.Root.Value] = rootRef2
        });

        var newDoc = (PdfDocumentAdapter)ObjectGraphBuilder.SerializeToDocument(finalObjects, trailer);
        adapter.ReplaceCore(newDoc.Core);
    }

    // ── Outline tree construction ─────────────────────────────────────────────

    private static IReadOnlyList<PdfIndirectReference> BuildOutlineItems(
        ObjectGraphBuilder builder,
        IReadOnlyList<Bookmark> items,
        PdfObject parentRef,
        IReadOnlyDictionary<int, int> pageObjNums
    )
    {
        // Two-pass: reserve all numbers first so siblings can reference each other.
        var nums = items.Select(_ => builder.NextNumber()).ToArray();
        var refs = nums.Select(static n => new PdfIndirectReference(n, 0)).ToArray();

        for (var i = 0; i < items.Count; i++)
        {
            var bm = items[i];
            var pageObjNum = pageObjNums.GetValueOrDefault(bm.PageNumber, 0);
            PdfObject destArr = pageObjNum > 0
                ? new PdfArray([new PdfIndirectReference(pageObjNum, 0), PdfName.Get("Fit")])
                : PdfNull.Instance;

            var dict = new Dictionary<string, PdfObject>
            {
                [PdfName.Title.Value] = PdfString.FromLatin1(bm.Title),
                [PdfName.Parent.Value] = parentRef,
                [PdfName.Dest.Value] = destArr
            };
            if (i > 0) dict[PdfName.Prev.Value] = refs[i - 1];
            if (i < items.Count - 1) dict[PdfName.Next.Value] = refs[i + 1];

            if (bm.Children is { Count: > 0 })
            {
                var childRefs = BuildOutlineItems(builder, bm.Children, refs[i], pageObjNums);
                dict[PdfName.First.Value] = childRefs[0];
                dict[PdfName.Last.Value] = childRefs[^1];
                dict[PdfName.Count.Value] = new PdfInteger(CountAll(bm.Children));
            }

            builder.AddAt(nums[i], new PdfDictionary(dict));
        }

        return refs;
    }

    private static int CountAll(IEnumerable<Bookmark> items) =>
        items.Sum(static b => 1 + (b.Children is not null ? CountAll(b.Children) : 0));

    // ── Page object number lookup ─────────────────────────────────────────────

    private static IReadOnlyDictionary<int, int> BuildPageObjectNumbers(
        IReadOnlyList<PdfIndirectObject> existing,
        PdfDocumentCore core
    )
    {
        var result = new Dictionary<int, int>();
        for (var page = 1; page <= core.PageCount; page++)
        {
            var pageDict = core.GetPage(page);
            var obj = existing.FirstOrDefault(o => ReferenceEquals(o.Value, pageDict));
            if (obj is not null)
                result[page] = obj.ObjectNumber;
        }

        return result;
    }
}
