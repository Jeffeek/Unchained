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
    ) => Task.Run(() => MergeDocuments(documents, options, false), ct);

    /// <inheritdoc />
    public Task<IPdfDocument> MergeAsync(
        IReadOnlyList<Stream> streams,
        MergeOptions options,
        CancellationToken ct = default
    ) => MergeStreamsAsync(streams, options, ct);

    // ── Merge from pre-loaded IPdfDocument list ───────────────────────────────

    private static IPdfDocument MergeDocuments(
        IReadOnlyList<IPdfDocument> documents,
        MergeOptions options,
        bool copyStreamData
    )
    {
        if (documents.Count == 0)
            throw new ArgumentException("At least one source document is required.", nameof(documents));

        var globalObjects = new List<PdfIndirectObject>();
        var pageRefs = new List<PdfIndirectReference>();
        var globalMax = 0;

        foreach (var doc in documents)
        {
            var adapter = doc as PdfDocumentAdapter
                          ?? throw new ArgumentException(
                              $"Document was not created by Unchained. Expected {nameof(PdfDocumentAdapter)}, got {doc.GetType().Name}.",
                              nameof(documents)
                          );

            var objects = adapter.Core.CollectObjects();
            var sourceMax = objects.Count > 0 ? objects.Max(static o => o.ObjectNumber) : 0;
            var offset = globalMax;

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

            globalMax += sourceMax;
        }

        return BuildMergedDocument(globalObjects, pageRefs, globalMax, options);
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

            var core = PdfDocumentCore.Parse(bytes);
            try
            {
                var objects = core.CollectObjects();
                var sourceMax = objects.Count > 0 ? objects.Max(static o => o.ObjectNumber) : 0;
                var offset = globalMax;

                foreach (var remapped in from obj in objects
                                         where !IsStructural(obj)
                                         select (PdfIndirectObject)PdfObjectRemapper.Remap(obj, offset)
                                         into remapped
                                         select CopyStreamData(remapped))
                {
                    globalObjects.Add(remapped);

                    if (IsPageLeaf(remapped))
                        pageRefs.Add(remapped.ToReference());
                }

                globalMax += sourceMax;
            }
            finally
            {
                core.Dispose();
            }
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
        obj.Value is PdfDictionary d && d.GetName(PdfName.Type.Value) is "Catalog" or "Pages";

    private static bool IsPageLeaf(PdfIndirectObject obj) =>
        obj.Value is PdfDictionary d && d.IsPage();

    // Returns a new PdfIndirectObject where every PdfStream's Data is an independent
    // byte array, severing the reference to the source document's backing buffer.
    private static PdfIndirectObject CopyStreamData(PdfIndirectObject obj) =>
        obj.Value is not PdfStream stream
            ? obj
            : new PdfIndirectObject(obj.ObjectNumber, obj.Generation, new PdfStream(stream.Dictionary, stream.Data.ToArray()));
}
