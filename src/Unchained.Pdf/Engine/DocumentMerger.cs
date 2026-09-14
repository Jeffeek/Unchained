using Unchained.Pdf.Abstractions;
using Unchained.Pdf.Core;
using Unchained.Pdf.Document;
using Unchained.Pdf.Models;

namespace Unchained.Pdf.Engine;

/// <summary>
///     Default <see cref="IDocumentMerger" /> implementation.
///     Source documents are processed sequentially so that peak memory is proportional
///     to the largest single source file, not the total size of all inputs.
/// </summary>
// ReSharper disable once MemberCanBeInternal
public sealed class DocumentMerger : IDocumentMerger
{
    /// <inheritdoc />
    public Task<IPdfDocument> MergeAsync(
        IReadOnlyList<IPdfDocument> documents,
        MergeOptions options,
        CancellationToken ct = default
    ) => Task.Run(
        () => MergeSources(documents.Select(static d => new MergeSource(d)).ToList(), options, false),
        ct
    );

    /// <inheritdoc />
    public Task<IPdfDocument> MergeAsync(
        IReadOnlyList<Stream> streams,
        MergeOptions options,
        CancellationToken ct = default
    ) => MergeStreamsAsync(streams, options, ct);

    /// <inheritdoc />
    public Task<IPdfDocument> MergeAsync(
        IReadOnlyList<MergeSource> sources,
        MergeOptions options,
        CancellationToken ct = default
    ) => Task.Run(() => MergeSources(sources, options, false), ct);

    // ── Merge from pre-loaded source list ─────────────────────────────────────

    private static IPdfDocument MergeSources(
        IReadOnlyList<MergeSource> sources,
        MergeOptions options,
        bool copyStreamData
    )
    {
        if (sources.Count == 0)
            throw new ArgumentException("At least one source document is required.", nameof(sources));

        var globalObjects = new List<PdfIndirectObject>();
        var pageRefs = new List<PdfIndirectReference>();
        var globalMax = 0;

        foreach (var source in sources)
        {
            var adapter = MutationHelper.Cast(nameof(sources), source.Document);

            var objects = adapter.Core.CollectObjects();
            var sourceMax = objects.Count > 0 ? objects.Max(static o => o.ObjectNumber) : 0;
            var offset = globalMax;

            if (source.PageRanges is null)
                AppendAllPages(objects, offset, copyStreamData, globalObjects, pageRefs);
            else
                AppendSelectedPages(source, adapter.Core, objects, offset, copyStreamData, globalObjects, pageRefs);

            globalMax += sourceMax;
        }

        return BuildMergedDocument(globalObjects, pageRefs, globalMax, options);
    }

    // Copies every non-structural object from a source and records each page leaf, in
    // object-collection order. This is the whole-document merge path.
    private static void AppendAllPages(
        IEnumerable<PdfIndirectObject> objects,
        int offset,
        bool copyStreamData,
        ICollection<PdfIndirectObject> globalObjects,
        ICollection<PdfIndirectReference> pageRefs
    )
    {
        foreach (var remapped in from obj in objects
                                 where !IsStructural(obj)
                                 select (PdfIndirectObject)PdfObjectRemapper.Remap(obj, offset)
                                 into remapped
                                 select copyStreamData ? CopyStreamData(remapped) : remapped)
        {
            globalObjects.Add(remapped);

            if (IsPageLeaf(remapped))
                pageRefs.Add(remapped.ToReference());
        }
    }

    // Copies only the objects reachable from the selected page leaves and records the selected
    // pages in range order. Reachability is walked from /Parent-stripped copies so it cannot
    // climb the source page tree into unrelated pages; BuildMergedDocument re-points /Parent on
    // the leaves that remain in globalObjects.
    private static void AppendSelectedPages(
        MergeSource source,
        PdfDocumentCore core,
        IReadOnlyList<PdfIndirectObject> objects,
        int offset,
        bool copyStreamData,
        ICollection<PdfIndirectObject> globalObjects,
        ICollection<PdfIndirectReference> pageRefs
    )
    {
        var pageCount = source.Document.PageCount;
        var selectedLeaves = new List<int>();
        var leafDicts = new Dictionary<int, PdfDictionary>();

        foreach (var (start, end) in source.PageRanges!)
        {
            if (start < 1 || end > pageCount || start > end)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(source),
                    (start, end),
                    $"Page range [{start}, {end}] is outside the document bounds [1, {pageCount}]."
                );
            }

            for (var page = start; page <= end; page++)
            {
                var dict = core.GetPage(page);
                var objNum = FindObjectNumber(objects, dict);
                selectedLeaves.Add(objNum);
                leafDicts[objNum] = dict;
            }
        }

        var strippedLeaves = leafDicts
            .Select(static kvp => new PdfIndirectObject(kvp.Key, 0, WithoutParent(kvp.Value)));
        var reachable = PdfReachability.CollectReachableFromLeaves(strippedLeaves, core);

        foreach (var remapped in from obj in objects
                                 where reachable.Contains(obj.ObjectNumber)
                                 where !IsStructural(obj)
                                 select (PdfIndirectObject)PdfObjectRemapper.Remap(obj, offset)
                                 into remapped
                                 select copyStreamData ? CopyStreamData(remapped) : remapped)
            globalObjects.Add(remapped);

        foreach (var objNum in selectedLeaves)
            pageRefs.Add(new PdfIndirectReference(objNum + offset, 0));
    }

    // ── Merge from Stream list (sequential: parse, process, dispose) ──────────

    private static async Task<IPdfDocument> MergeStreamsAsync(
        IReadOnlyList<Stream> streams,
        MergeOptions options,
        CancellationToken ct
    )
    {
        if (streams.Count == 0)
            throw new ArgumentException("At least one source stream is required.", nameof(streams));

        var globalObjects = new List<PdfIndirectObject>();
        var pageRefs = new List<PdfIndirectReference>();
        var globalMax = 0;

        foreach (var stream in streams)
        {
            ct.ThrowIfCancellationRequested();

            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms, ct).ConfigureAwait(false);
            var bytes = ms.ToArray();

            using var core = PdfDocumentCore.Parse(bytes);
            var objects = core.CollectObjects();
            var sourceMax = objects.Count > 0 ? objects.Max(static o => o.ObjectNumber) : 0;
            var offset = globalMax;

            AppendAllPages(objects, offset, copyStreamData: true, globalObjects, pageRefs);

            globalMax += sourceMax;
        }

        return BuildMergedDocument(globalObjects, pageRefs, globalMax, options);
    }

    // ── Assembly ──────────────────────────────────────────────────────────────

    private static IPdfDocument BuildMergedDocument(
        IList<PdfIndirectObject> globalObjects,
        IReadOnlyCollection<PdfIndirectReference> pageRefs,
        int globalMax,
        MergeOptions options
    )
    {
        _ = options; // reserved for CopyOutlines / OptimizeResources in future milestones

        var pagesRootNum = globalMax + 1;
        var catalogNum = globalMax + 2;
        var pagesRef = new PdfIndirectReference(pagesRootNum, 0);

        // Patch /Parent in all page leaf dicts to point to the new /Pages root.
        for (var i = 0; i < globalObjects.Count; i++)
        {
            if (!IsPageLeaf(globalObjects[i]))
                continue;

            var pageDict = (PdfDictionary)globalObjects[i].Value;
            var entries = new Dictionary<string, PdfObject>(pageDict.Entries)
            {
                [PdfName.Parent.Value] = pagesRef
            };
            globalObjects[i] = new PdfIndirectObject(
                globalObjects[i].ObjectNumber,
                globalObjects[i].Generation,
                new PdfDictionary(entries)
            );
        }

        // New flat /Pages root and /Catalog.
        globalObjects.Add(
            new PdfIndirectObject(
                pagesRootNum,
                0,
                new PdfDictionary(
                    new Dictionary<string, PdfObject>
                    {
                        [PdfName.Type.Value] = PdfName.Pages,
                        [PdfName.Kids.Value] = new PdfArray(pageRefs.Cast<PdfObject>().ToArray()),
                        [PdfName.Count.Value] = new PdfInteger(pageRefs.Count)
                    }
                )
            )
        );

        var catalogRef = new PdfIndirectReference(catalogNum, 0);
        globalObjects.Add(
            new PdfIndirectObject(
                catalogNum,
                0,
                new PdfDictionary(
                    new Dictionary<string, PdfObject>
                    {
                        [PdfName.Type.Value] = PdfName.Catalog,
                        [PdfName.Pages.Value] = pagesRef
                    }
                )
            )
        );

        var trailer = new PdfDictionary(
            new Dictionary<string, PdfObject>
            {
                [PdfName.Size.Value] = new PdfInteger(catalogNum + 1),
                [PdfName.Root.Value] = catalogRef
            }
        );

        return ObjectGraphBuilder.SerializeToDocument(globalObjects, trailer);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static bool IsStructural(PdfIndirectObject obj) =>
        obj.Value is PdfDictionary d && (d.GetName(PdfName.Type.Value) == PdfName.Catalog.Value || d.GetName(PdfName.Type.Value) == PdfName.Pages.Value);

    private static bool IsPageLeaf(PdfIndirectObject obj) =>
        obj.Value is PdfDictionary d && d.IsPage();

    // Locates the object number of a page leaf by reference identity. GetPage returns the same
    // cached dictionary instance that CollectObjects wraps, so reference equality is reliable.
    private static int FindObjectNumber(IEnumerable<PdfIndirectObject> objects, PdfDictionary dict) =>
        objects.FirstOrDefault(o => ReferenceEquals(o.Value, dict))?.ObjectNumber
        ?? throw new PdfException("Selected page was not found among the document's objects.");

    // Returns a copy of a page dictionary with /Parent removed, so a reachability walk started
    // from it cannot climb back up the page tree into unrelated pages.
    private static PdfDictionary WithoutParent(PdfDictionary dict)
    {
        var entries = new Dictionary<string, PdfObject>(dict.Entries);
        entries.Remove(PdfName.Parent.Value);
        return new PdfDictionary(entries);
    }

    // Returns a new PdfIndirectObject where every PdfStream's Data is an independent
    // byte array, severing the reference to the source document's backing buffer.
    private static PdfIndirectObject CopyStreamData(PdfIndirectObject obj) =>
        obj.Value is not PdfStream stream
            ? obj
            : new PdfIndirectObject(obj.ObjectNumber, obj.Generation, new PdfStream(stream.Dictionary, stream.Data.ToArray()));
}
