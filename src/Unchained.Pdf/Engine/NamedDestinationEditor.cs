using Unchained.Pdf.Abstractions;
using Unchained.Pdf.Core;
using Unchained.Pdf.Document;

namespace Unchained.Pdf.Engine;

/// <summary>Default <see cref="INamedDestinationEditor"/> implementation.</summary>
// ReSharper disable once MemberCanBeInternal
public sealed class NamedDestinationEditor : INamedDestinationEditor
{
    /// <inheritdoc />
    public Task SetDestinationAsync(
        IPdfDocument document,
        string name,
        int pageNumber,
        CancellationToken ct = default
    ) => Task.Run(() => Mutate(document, name, pageNumber), ct);

    /// <inheritdoc />
    public Task RemoveDestinationAsync(
        IPdfDocument document,
        string name,
        CancellationToken ct = default
    ) => Task.Run(() => Mutate(document, name, pageNumber: 0), ct);

    private static void Mutate(IPdfDocument document, string name, int pageNumber)
    {
        var adapter = MutationHelper.Cast(nameof(document), document);
        var existing = adapter.Core.CollectObjects();

        // Find the page object number for the destination (page reference in /Dest array).
        PdfIndirectReference? pageRef = null;
        if (pageNumber > 0)
        {
            var pageDict = adapter.Core.GetPage(pageNumber);
            var pageObj = existing.FirstOrDefault(o => ReferenceEquals(o.Value, pageDict));
            if (pageObj is not null)
                pageRef = pageObj.ToReference();
        }

        var catalogObj = existing.First(static o =>
            o.Value is PdfDictionary d && d.GetName(PdfName.Type.Value) == "Catalog");
        var catDict = (PdfDictionary)catalogObj.Value;
        var catEntries = new Dictionary<string, PdfObject>(catDict.Entries);

        // Read existing /Names /Dests flat name list, then add/remove our entry.
        var existingDests = ReadFlatDests(catDict, adapter.Core);
        if (pageNumber <= 0)
            existingDests.Remove(name);
        else if (pageRef is not null)
            existingDests[name] = new PdfArray([pageRef, PdfName.Get("Fit")]);

        // Rebuild /Names /Dests as a flat name dict (simple structure).
        var namesEntries = new Dictionary<string, PdfObject>();
        if (existingDests.Count > 0)
        {
            var namesList = new List<PdfObject>();
            foreach (var (k, v) in existingDests.OrderBy(static kvp => kvp.Key, StringComparer.Ordinal))
            {
                namesList.Add(PdfString.FromLatin1(k));
                namesList.Add(v);
            }

            namesEntries[PdfName.Dests.Value] = new PdfDictionary(new Dictionary<string, PdfObject>
            {
                [PdfName.Get("Names").Value] = new PdfArray(namesList.ToArray())
            });
        }

        if (namesEntries.Count > 0)
            catEntries[PdfName.Names.Value] = new PdfDictionary(namesEntries);
        else
            catEntries.Remove(PdfName.Names.Value);

        var rebuiltCatalog = new PdfIndirectObject(catalogObj.ObjectNumber, catalogObj.Generation, new PdfDictionary(catEntries));
        var finalObjects = existing
            .Select(o => o.ObjectNumber == catalogObj.ObjectNumber ? rebuiltCatalog : o)
            .ToList();

        MutationHelper.SerializeAndReplace(adapter, finalObjects);
    }

    // Read the existing /Names /Dests entries into a mutable dictionary.
    private static Dictionary<string, PdfObject> ReadFlatDests(PdfDictionary catalog, PdfDocumentCore core)
    {
        var result = new Dictionary<string, PdfObject>(StringComparer.Ordinal);
        var namesObj = catalog[PdfName.Names];
        var namesDict = namesObj switch
        {
            PdfDictionary d => d,
            PdfIndirectReference r => core.ResolveIndirect(r.ObjectNumber).Value as PdfDictionary,
            _ => null
        };
        var destsObj = namesDict?[PdfName.Dests];
        var destsDict = destsObj switch
        {
            PdfDictionary d => d,
            PdfIndirectReference r => core.ResolveIndirect(r.ObjectNumber).Value as PdfDictionary,
            _ => null
        };
        var namesArr = destsDict?.Get<PdfArray>(PdfName.Get("Names"));
        if (namesArr is null) return result;

        for (var i = 0; i + 1 < namesArr.Count; i += 2)
        {
            var key = namesArr[i] is PdfString ks
                ? System.Text.Encoding.Latin1.GetString(ks.Bytes.Span)
                : (namesArr[i] as PdfName)?.Value ?? string.Empty;
            if (key.Length > 0)
                result[key] = namesArr[i + 1];
        }

        return result;
    }
}
