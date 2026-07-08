using Unchained.Pdf.Core;
using Unchained.Pdf.Document;
using Unchained.Pdf.Engine.PageResources;
using Unchained.Pdf.Models;

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
        var rootRef = GetCatalogRef(adapter);
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

    internal static (int Index, PdfDictionary Dict) GetCatalogDict(
        PdfDocumentAdapter adapter,
        List<PdfIndirectObject> existing
    )
    {
        var catalogRef = GetCatalogRef(adapter);
        var catalogIdx = existing.FindIndex(o => o.ObjectNumber == catalogRef.ObjectNumber);
        if (catalogIdx < 0)
            throw new PdfException("Catalog not found.");

        var catalogDict = existing[catalogIdx].Value as PdfDictionary ?? throw new PdfException("Catalog is not a dictionary.");
        return (catalogIdx, catalogDict);
    }

    internal static (bool Found, int Index, PdfDictionary Dict) TryGetCatalogDict(
        PdfDocumentAdapter adapter,
        List<PdfIndirectObject> existing
    )
    {
        var catalogRef = GetCatalogRef(adapter);
        var catalogIdx = existing.FindIndex(o => o.ObjectNumber == catalogRef.ObjectNumber);

        return catalogIdx < 0 || existing[catalogIdx].Value is not PdfDictionary catalogDict ? (false, -1, null!) : (true, catalogIdx, catalogDict);
    }

    internal static (List<PdfIndirectObject> Objects, int Max) CollectWithMax(PdfDocumentAdapter adapter)
    {
        var objects = adapter.Core.CollectObjects().ToList();
        return (objects, objects.Count > 0 ? objects.Max(static o => o.ObjectNumber) : 0);
    }

    internal static (List<PdfIndirectObject> Objects, int Max) CollectWithMax(PdfDocumentCore core)
    {
        var objects = core.CollectObjects().ToList();
        return (objects, objects.Count > 0 ? objects.Max(static o => o.ObjectNumber) : 0);
    }

    internal static PdfDocumentAdapter Cast(string paramName, object document) =>
        document as PdfDocumentAdapter
        ?? throw new ArgumentException(
            $"Document was not created by Unchained. Expected {nameof(PdfDocumentAdapter)}, got {document.GetType().Name}.",
            paramName
        );

    internal static List<PdfIndirectObject> ModifyCatalog(
        PdfDocumentAdapter adapter,
        IReadOnlyList<PdfIndirectObject> existing,
        Action<Dictionary<string, PdfObject>> modifyCatalog
    )
    {
        var catalogObj = existing.First(static o => o.Value is PdfDictionary d && d.IsCatalog());
        var catDict = (PdfDictionary)catalogObj.Value;
        var catEntries = new Dictionary<string, PdfObject>(catDict.Entries);
        modifyCatalog(catEntries);
        var rebuiltCatalog = new PdfIndirectObject(catalogObj.ObjectNumber, catalogObj.Generation, new PdfDictionary(catEntries));
        return existing
            .Select(o => o.ObjectNumber == catalogObj.ObjectNumber ? rebuiltCatalog : o)
            .ToList();
    }

    internal static void ApplyCatalogMutation(
        PdfDocumentAdapter adapter,
        Action<Dictionary<string, PdfObject>> mutation
    )
    {
        var existing = adapter.Core.CollectObjects().ToList();
        var (catalogIdx, catalogDict) = GetCatalogDict(adapter, existing);
        var entries = new Dictionary<string, PdfObject>(catalogDict.Entries);
        mutation(entries);
        existing[catalogIdx] = new PdfIndirectObject(existing[catalogIdx].ObjectNumber, 0, new PdfDictionary(entries));
        SerializeAndReplace(adapter, existing);
    }

    internal static bool TryApplyCatalogMutation(
        PdfDocumentAdapter adapter,
        Action<Dictionary<string, PdfObject>> mutation
    )
    {
        var existing = adapter.Core.CollectObjects().ToList();
        var (found, catalogIdx, catalogDict) = TryGetCatalogDict(adapter, existing);
        if (!found) return false;

        var entries = new Dictionary<string, PdfObject>(catalogDict.Entries);
        mutation(entries);
        existing[catalogIdx] = new PdfIndirectObject(existing[catalogIdx].ObjectNumber, 0, new PdfDictionary(entries));
        SerializeAndReplace(adapter, existing);
        return true;
    }

    internal static (List<PdfIndirectObject> Objects, ObjectGraphBuilder Builder) CollectWithBuilder(PdfDocumentAdapter adapter)
    {
        var (objects, max) = CollectWithMax(adapter);
        return (objects, new ObjectGraphBuilder(max + 1));
    }

    internal static PdfIndirectReference GetCatalogRef(PdfDocumentAdapter adapter) =>
        GetRootRef(adapter.Core.Trailer);

    internal static PdfIndirectReference GetRootRef(PdfDictionary trailer) =>
        trailer[PdfName.Root] as PdfIndirectReference
        ?? throw new PdfException("Trailer missing /Root.");

    /// <summary>
    ///     Generic recursive collector for PDF NameTree (<c>/Names</c>) and NumberTree (<c>/Nums</c>).
    ///     Walks the tree bottom-up: processes leaf entries, then recurses into <c>/Kids</c> nodes.
    /// </summary>
    /// <typeparam name="TResult">
    ///     The type of extracted result (e.g., <see cref="NamedDestination" />, <see cref="EmbeddedFile" />).
    /// </typeparam>
    /// <param name="node">The current tree node dictionary.</param>
    /// <param name="core">The document core for resolving indirect references.</param>
    /// <param name="result">The collection to add extracted results to.</param>
    /// <param name="leafKey">The name of the leaf array (<c>PdfName.Names</c> or <c>PdfName.Nums</c>).</param>
    /// <param name="extract">
    ///     Callback invoked for each key–value pair. leafArray holds the owning array
    ///     so the callback can resolve the value element at <c>i+1</c>. Returns <c>true</c> to add the
    ///     extracted value to <paramref name="result" />, <c>false</c> to skip.
    /// </param>
    internal static void CollectTree<TResult>(
        PdfDictionary node,
        PdfDocumentCore core,
        ICollection<TResult> result,
        string leafKey,
        Func<PdfDictionary, PdfArray, int, bool, TResult?> extract
    )
    {
        if (node.Get<PdfArray>(leafKey) is { } leafArray)
        {
            for (var i = 0; i + 1 < leafArray.Count; i += 2)
            {
                var keyIsString = leafArray[i] is PdfString;
                var value = extract(node, leafArray, i, keyIsString);
                if (value is not null) result.Add(value);
            }
        }

        if (node.Get<PdfArray>(PdfName.Kids) is not { } kids)
            return;

        foreach (var childDict in kids.Elements.Select(core.ResolveDict).Where(static x => x != null))
            CollectTree(childDict!, core, result, leafKey, extract);
    }
}
