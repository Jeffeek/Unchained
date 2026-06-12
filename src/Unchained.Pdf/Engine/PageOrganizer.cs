using Unchained.Pdf.Abstractions;
using Unchained.Pdf.Core;
using Unchained.Pdf.Document;

namespace Unchained.Pdf.Engine;

/// <summary>
///     Default <see cref="IPageOrganizer" /> implementation. Page operations rebuild the
///     document's <c>/Pages</c> tree as a single flat node so the resulting page order is
///     explicit and unambiguous.
///     <para>
///         In-place operations (rotate, delete, reorder) preserve every page leaf's object number,
///         so back-references (annotation <c>/P</c>, outline <c>/Dest</c>, structure-tree <c>/Pg</c>)
///         remain valid. Inheritable page attributes (<c>/MediaBox</c>, <c>/CropBox</c>,
///         <c>/Resources</c>, <c>/Rotate</c>) are baked onto each leaf before interior page-tree
///         nodes are dropped, per ISO 32000-1 §7.7.3.4.
///     </para>
/// </summary>
public sealed class PageOrganizer : IPageOrganizer
{
    private static readonly string[] InheritableKeys = ["MediaBox", "CropBox", "Resources", "Rotate"];

    /// <inheritdoc />
    public Task RotatePagesAsync(
        IPdfDocument document,
        IReadOnlyList<int> pageNumbers,
        int degrees,
        bool relative = true,
        CancellationToken ct = default
    ) => Task.Run(() => RotatePages(document, pageNumbers, degrees, relative), ct);

    /// <inheritdoc />
    public Task DeletePagesAsync(
        IPdfDocument document,
        IReadOnlyList<int> pageNumbers,
        CancellationToken ct = default
    ) => Task.Run(() => DeletePages(document, pageNumbers), ct);

    /// <inheritdoc />
    public Task ReorderPagesAsync(
        IPdfDocument document,
        IReadOnlyList<int> newOrder,
        CancellationToken ct = default
    ) => Task.Run(() => ReorderPages(document, newOrder), ct);

    /// <inheritdoc />
    public Task InsertPagesAsync(
        IPdfDocument document,
        int atPageNumber,
        IPdfDocument source,
        CancellationToken ct = default
    ) => Task.Run(() => InsertPages(document, atPageNumber, source), ct);

    /// <inheritdoc />
    public Task<IReadOnlyList<IPdfDocument>> SplitAsync(
        IPdfDocument document,
        IReadOnlyList<(int Start, int End)> ranges,
        CancellationToken ct = default
    ) => Task.Run(() => Split(document, ranges), ct);

    // ── Rotate ──────────────────────────────────────────────────────────────────

    private static void RotatePages(
        IPdfDocument document,
        IReadOnlyList<int> pageNumbers,
        int degrees,
        bool relative
    )
    {
        if (degrees % 90 != 0)
            throw new ArgumentException("Rotation must be a multiple of 90 degrees.", nameof(degrees));

        var adapter = MutationHelper.Cast(nameof(document), document);
        var pageCount = adapter.Core.PageCount;
        var targets = new HashSet<int>(pageNumbers);
        foreach (var n in targets.Where(n => n < 1 || n > pageCount))
            throw new ArgumentOutOfRangeException(nameof(pageNumbers), n, $"Page number must be between 1 and {pageCount}.");

        var leaves = CollectLeaves(adapter.Core);
        var ordered = new List<(int ObjNum, PdfDictionary Dict)>(leaves.Count);
        for (var i = 0; i < leaves.Count; i++)
        {
            var (objNum, dict) = leaves[i];
            if (targets.Contains(i + 1))
            {
                var current = (int)(dict.Get<PdfInteger>(PdfName.Get("Rotate"))?.Value ?? 0L);
                var next = Normalize(relative ? current + degrees : degrees);
                var entries = new Dictionary<string, PdfObject>(dict.Entries)
                {
                    ["Rotate"] = new PdfInteger(next)
                };
                dict = new PdfDictionary(entries);
            }

            ordered.Add((objNum, dict));
        }

        RebuildFlatTree(adapter, ordered);
    }

    private static int Normalize(int deg) => ((deg % 360) + 360) % 360;

    // ── Delete ──────────────────────────────────────────────────────────────────

    private static void DeletePages(IPdfDocument document, IReadOnlyList<int> pageNumbers)
    {
        var adapter = MutationHelper.Cast(nameof(document), document);
        var pageCount = adapter.Core.PageCount;
        var remove = new HashSet<int>(pageNumbers);

        foreach (var n in remove.Where(n => n < 1 || n > pageCount))
            throw new ArgumentOutOfRangeException(nameof(pageNumbers), n, $"Page number must be between 1 and {pageCount}.");

        if (remove.Count >= pageCount)
            throw new ArgumentException("Cannot delete all pages; at least one page must remain.", nameof(pageNumbers));

        var leaves = CollectLeaves(adapter.Core);
        var ordered = leaves.Where((_, i) => !remove.Contains(i + 1)).ToList();
        RebuildFlatTree(adapter, ordered);
    }

    // ── Reorder ─────────────────────────────────────────────────────────────────

    private static void ReorderPages(IPdfDocument document, IReadOnlyList<int> newOrder)
    {
        var adapter = MutationHelper.Cast(nameof(document), document);
        var pageCount = adapter.Core.PageCount;
        if (newOrder.Count != pageCount || newOrder.Distinct().Count() != pageCount || newOrder.Any(n => n < 1 || n > pageCount))
        {
            throw new ArgumentException(
                $"newOrder must be a permutation of 1..{pageCount}.",
                nameof(newOrder));
        }

        var leaves = CollectLeaves(adapter.Core);
        var ordered = newOrder.Select(n => leaves[n - 1]).ToList();
        RebuildFlatTree(adapter, ordered);
    }

    // ── Insert ──────────────────────────────────────────────────────────────────

    private static void InsertPages(IPdfDocument document, int atPageNumber, IPdfDocument source)
    {
        var adapter = MutationHelper.Cast(nameof(document), document);
        var srcAdapter = MutationHelper.Cast(nameof(source), source);
        var destCount = adapter.Core.PageCount;
        if (atPageNumber < 1 || atPageNumber > destCount + 1)
        {
            throw new ArgumentOutOfRangeException(nameof(atPageNumber),
                atPageNumber,
                $"Insert position must be between 1 and {destCount + 1}.");
        }

        var destObjects = adapter.Core.CollectObjects();
        var destMax = destObjects.Count > 0 ? destObjects.Max(static o => o.ObjectNumber) : 0;

        // Remap every source object above the destination's number space (like the merger),
        // copying stream data so the result is independent of the source document.
        var srcObjects = srcAdapter.Core.CollectObjects();
        var srcLeafOrder = OrderedLeafObjectNumbers(srcAdapter.Core);
        var remapped = srcObjects
            .Where(static o => !IsStructural(o))
            .Select(o => CopyStreamData((PdfIndirectObject)PdfObjectRemapper.Remap(o, destMax)))
            .ToList();

        // Map of remapped source page-leaf object number → its baked dict (inheritance from
        // the source tree resolved before its interior nodes were dropped).
        var srcBaked = BakeLeaves(srcAdapter.Core);
        var srcInserted = srcLeafOrder
            .Select(n => n + destMax)
            .Select(n => (ObjNum: n, Dict: srcBaked[n - destMax]))
            .ToList();

        // Destination leaves (baked) keyed by their own object numbers, kept as-is.
        var destLeaves = CollectLeaves(adapter.Core);

        // Compose the new ordered page list: dest[0..insert) + source + dest[insert..].
        var ordered = new List<(int ObjNum, PdfDictionary Dict)>(destLeaves.Count + srcInserted.Count);
        ordered.AddRange(destLeaves.Take(atPageNumber - 1));
        ordered.AddRange(srcInserted);
        ordered.AddRange(destLeaves.Skip(atPageNumber - 1));

        // Carry the remapped, non-leaf source objects into the destination object set.
        var extraSourceObjects = remapped
            .Where(o => srcInserted.All(s => s.ObjNum != o.ObjectNumber))
            .ToList();

        RebuildFlatTree(adapter, ordered, extraSourceObjects);
    }

    // ── Split ───────────────────────────────────────────────────────────────────

    private static IReadOnlyList<IPdfDocument> Split(IPdfDocument document, IReadOnlyList<(int Start, int End)> ranges)
    {
        var adapter = MutationHelper.Cast(nameof(document), document);
        var pageCount = adapter.Core.PageCount;
        if (ranges.Count == 0)
            throw new ArgumentException("At least one page range is required.", nameof(ranges));

        foreach (var (start, end) in ranges)
        {
            if (start < 1 || end > pageCount || start > end)
            {
                throw new ArgumentOutOfRangeException(nameof(ranges),
                    (start, end),
                    $"Each range must satisfy 1 <= Start <= End <= {pageCount}.");
            }
        }

        var result = new List<IPdfDocument>(ranges.Count);
        foreach (var (start, end) in ranges)
            result.Add(BuildSubsetDocument(adapter.Core, Enumerable.Range(start, end - start + 1).ToList()));
        return result;
    }

    // Builds a new self-contained document from the given 1-based page numbers, in order.
    private static IPdfDocument BuildSubsetDocument(PdfDocumentCore core, IEnumerable<int> pages)
    {
        var allObjects = core.CollectObjects().Where(static o => !IsStructural(o)).ToList();
        var baked = BakeLeaves(core);                   // objNum → baked leaf dict
        var leafOrder = OrderedLeafObjectNumbers(core); // page order → objNum
        var keptLeafNums = pages.Select(p => leafOrder[p - 1]).ToList();

        // Keep page-leaf objects (baked) for the selected pages, plus every non-leaf object
        // (Contents/Resources/fonts/images are shared and referenced by object number).
        var leafSet = new HashSet<int>(leafOrder);
        var objects = new List<PdfIndirectObject>();
        foreach (var o in allObjects)
        {
            if (leafSet.Contains(o.ObjectNumber))
            {
                if (keptLeafNums.Contains(o.ObjectNumber))
                    objects.Add(new PdfIndirectObject(o.ObjectNumber, o.Generation, baked[o.ObjectNumber]));
                // dropped leaves are simply omitted
            }
            else
                objects.Add(CopyStreamData(o));
        }

        var maxNum = objects.Count > 0 ? objects.Max(static o => o.ObjectNumber) : 0;
        var pagesRootNum = maxNum + 1;
        var catalogNum = maxNum + 2;
        var pagesRef = new PdfIndirectReference(pagesRootNum, 0);

        var orderedRefs = keptLeafNums.Select(static PdfObject (n) => new PdfIndirectReference(n, 0)).ToArray();

        // Re-point each kept leaf's /Parent at the new root.
        for (var i = 0; i < objects.Count; i++)
        {
            if (!keptLeafNums.Contains(objects[i].ObjectNumber))
                continue;

            var d = (PdfDictionary)objects[i].Value;
            objects[i] = new PdfIndirectObject(objects[i].ObjectNumber,
                objects[i].Generation,
                WithEntry(d, PdfName.Parent.Value, pagesRef));
        }

        objects.Add(new PdfIndirectObject(pagesRootNum,
            0,
            new PdfDictionary(new Dictionary<string, PdfObject>
            {
                [PdfName.Type.Value] = PdfName.Pages,
                [PdfName.Kids.Value] = new PdfArray(orderedRefs),
                [PdfName.Count.Value] = new PdfInteger(keptLeafNums.Count)
            })));
        var catalogRef = new PdfIndirectReference(catalogNum, 0);
        objects.Add(new PdfIndirectObject(catalogNum,
            0,
            new PdfDictionary(new Dictionary<string, PdfObject>
            {
                [PdfName.Type.Value] = PdfName.Catalog,
                [PdfName.Pages.Value] = pagesRef
            })));

        var trailer = new PdfDictionary(new Dictionary<string, PdfObject>
        {
            [PdfName.Size.Value] = new PdfInteger(catalogNum + 1),
            [PdfName.Root.Value] = catalogRef
        });
        return ObjectGraphBuilder.SerializeToDocument(objects, trailer);
    }

    // ── Shared rebuild ────────────────────────────────────────────────────────────

    // Rebuilds the document in place with a flat /Pages tree containing exactly the supplied
    // ordered page leaves. Page-leaf object numbers are preserved; interior page-tree nodes
    // are dropped; the catalog keeps all its entries except /Pages, which points at the new
    // flat root. extraObjects are additional objects to include (e.g. remapped source pages).
    private static void RebuildFlatTree(
        PdfDocumentAdapter adapter,
        IReadOnlyCollection<(int ObjNum, PdfDictionary Dict)> ordered,
        IReadOnlyList<PdfIndirectObject>? extraObjects = null
    )
    {
        var existing = adapter.Core.CollectObjects();
        var catalog = adapter.Core.Catalog;
        var catalogNum = (adapter.Core.Trailer[PdfName.Root] as PdfIndirectReference
                          ?? throw new PdfException("Trailer missing /Root.")).ObjectNumber;
        var pageTreeNums = PageTreeNodeNumbers(adapter.Core);

        var orderedNums = new HashSet<int>(ordered.Select(static p => p.ObjNum));
        var combinedMax = existing.Max(static o => o.ObjectNumber);
        if (extraObjects is { Count: > 0 })
            combinedMax = Math.Max(combinedMax, extraObjects.Max(static o => o.ObjectNumber));
        var pagesRootNum = combinedMax + 1;
        var pagesRef = new PdfIndirectReference(pagesRootNum, 0);

        var objects = (from o in existing
                       where !pageTreeNums.Contains(o.ObjectNumber)
                       where !orderedNums.Contains(o.ObjectNumber)
                       where o.ObjectNumber != catalogNum
                       select o).ToList();

        // Keep all existing objects that are not page-tree nodes, not the ordered leaves
        // (re-emitted below), and not the catalog (re-emitted below).

        if (extraObjects is not null)
            objects.AddRange(extraObjects.Where(o => !orderedNums.Contains(o.ObjectNumber)));

        // Re-emit ordered page leaves with /Parent set to the new flat root.
        foreach (var (objNum, dict) in ordered)
            objects.Add(new PdfIndirectObject(objNum, 0, WithEntry(dict, PdfName.Parent.Value, pagesRef)));

        // New flat /Pages root.
        objects.Add(new PdfIndirectObject(pagesRootNum,
            0,
            new PdfDictionary(new Dictionary<string, PdfObject>
            {
                [PdfName.Type.Value] = PdfName.Pages,
                [PdfName.Kids.Value] = new PdfArray(ordered.Select(static PdfObject (p) => new PdfIndirectReference(p.ObjNum, 0)).ToArray()),
                [PdfName.Count.Value] = new PdfInteger(ordered.Count)
            })));

        // Re-emit the catalog with /Pages pointing at the new root, keeping all other entries.
        objects.Add(new PdfIndirectObject(catalogNum, 0, WithEntry(catalog, PdfName.Pages.Value, pagesRef)));

        var totalMax = objects.Max(static o => o.ObjectNumber);
        var rootRef = new PdfIndirectReference(catalogNum, 0);
        var trailer = new PdfDictionary(new Dictionary<string, PdfObject>
        {
            [PdfName.Size.Value] = new PdfInteger(totalMax + 1),
            [PdfName.Root.Value] = rootRef
        });
        var newDoc = (PdfDocumentAdapter)ObjectGraphBuilder.SerializeToDocument(objects, trailer);
        adapter.ReplaceCore(newDoc.Core);
    }

    // ── Page-tree traversal helpers ───────────────────────────────────────────────

    // Ordered list of (object number, leaf dictionary with inheritance baked) for every page.
    private static List<(int ObjNum, PdfDictionary Dict)> CollectLeaves(PdfDocumentCore core)
    {
        var order = OrderedLeafObjectNumbers(core);
        var baked = BakeLeaves(core);
        return order.Select(n => (n, baked[n])).ToList();
    }

    // Walks the page tree and returns leaf object numbers in page order.
    private static List<int> OrderedLeafObjectNumbers(PdfDocumentCore core)
    {
        var result = new List<int>();
        var pagesRef = core.Catalog[PdfName.Pages] as PdfIndirectReference
                       ?? throw new PdfException("Catalog missing /Pages reference.");
        WalkTree(core, pagesRef, result, (HashSet<int>)[]);
        return result;
    }

    private static void WalkTree(
        PdfDocumentCore core,
        PdfIndirectReference nodeRef,
        ICollection<int> leaves,
        ISet<int> seen
    )
    {
        if (!seen.Add(nodeRef.ObjectNumber)) return; // cycle guard
        if (core.ResolveIndirect(nodeRef.ObjectNumber).Value is not PdfDictionary node) return;

        var type = node.GetName(PdfName.Type.Value);
        if (type == "Page")
        {
            leaves.Add(nodeRef.ObjectNumber);
            return;
        }

        if (node.Get<PdfArray>(PdfName.Kids) is not { } kids) return;

        foreach (var kid in kids.Elements)
        {
            if (kid is PdfIndirectReference kr)
                WalkTree(core, kr, leaves, seen);
        }
    }

    // Returns objNum → leaf dictionary with inheritable attributes (MediaBox/CropBox/
    // Resources/Rotate) resolved from ancestors and baked onto the leaf.
    private static Dictionary<int, PdfDictionary> BakeLeaves(PdfDocumentCore core)
    {
        var result = new Dictionary<int, PdfDictionary>();
        var pagesRef = core.Catalog[PdfName.Pages] as PdfIndirectReference
                       ?? throw new PdfException("Catalog missing /Pages reference.");
        BakeWalk(core, pagesRef, new Dictionary<string, PdfObject>(), result, (HashSet<int>)[]);
        return result;
    }

    private static void BakeWalk(
        PdfDocumentCore core,
        PdfIndirectReference nodeRef,
        IReadOnlyDictionary<string, PdfObject> inherited,
        IDictionary<int, PdfDictionary> output,
        ISet<int> seen
    )
    {
        if (!seen.Add(nodeRef.ObjectNumber)) return;
        if (core.ResolveIndirect(nodeRef.ObjectNumber).Value is not PdfDictionary node) return;

        // Accumulate inheritable attributes this node provides for its descendants.
        var nextInherited = new Dictionary<string, PdfObject>(inherited);
        foreach (var key in InheritableKeys)
        {
            if (node[key] is { } v)
                nextInherited[key] = v;
        }

        if (node.GetName(PdfName.Type.Value) == "Page")
        {
            var entries = new Dictionary<string, PdfObject>(node.Entries);
            foreach (var key in InheritableKeys)
            {
                if (!entries.ContainsKey(key) && inherited.TryGetValue(key, out var v))
                    entries[key] = v;
            }

            output[nodeRef.ObjectNumber] = new PdfDictionary(entries);
            return;
        }

        if (node.Get<PdfArray>(PdfName.Kids) is not { } kids) return;

        foreach (var kid in kids.Elements)
        {
            if (kid is PdfIndirectReference kr)
                BakeWalk(core, kr, nextInherited, output, seen);
        }
    }

    // Object numbers of all /Type /Pages nodes (interior + root) — dropped on rebuild.
    private static HashSet<int> PageTreeNodeNumbers(PdfDocumentCore core)
    {
        var result = new HashSet<int>();
        var pagesRef = core.Catalog[PdfName.Pages] as PdfIndirectReference
                       ?? throw new PdfException("Catalog missing /Pages reference.");
        CollectPagesNodes(core, pagesRef, result, (HashSet<int>)[]);
        return result;
    }

    private static void CollectPagesNodes(
        PdfDocumentCore core,
        PdfIndirectReference nodeRef,
        ISet<int> nodes,
        ISet<int> seen
    )
    {
        if (!seen.Add(nodeRef.ObjectNumber))
            return;
        if (core.ResolveIndirect(nodeRef.ObjectNumber).Value is not PdfDictionary node)
            return;
        if (node.GetName(PdfName.Type.Value) == "Page")
            return;

        nodes.Add(nodeRef.ObjectNumber);
        if (node.Get<PdfArray>(PdfName.Kids) is not { } kids)
            return;

        foreach (var kid in kids.Elements)
        {
            if (kid is PdfIndirectReference kr)
                CollectPagesNodes(core, kr, nodes, seen);
        }
    }

    // ── Misc helpers ──────────────────────────────────────────────────────────────

    private static PdfDictionary WithEntry(PdfDictionary dict, string key, PdfObject value)
    {
        var entries = new Dictionary<string, PdfObject>(dict.Entries) { [key] = value };
        return new PdfDictionary(entries);
    }

    private static bool IsStructural(PdfIndirectObject obj) =>
        obj.Value is PdfDictionary d && d.GetName(PdfName.Type.Value) is "Catalog" or "Pages";

    private static PdfIndirectObject CopyStreamData(PdfIndirectObject obj) =>
        obj.Value is not PdfStream stream
            ? obj
            : new PdfIndirectObject(obj.ObjectNumber, obj.Generation, new PdfStream(stream.Dictionary, stream.Data.ToArray()));
}
