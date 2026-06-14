using Unchained.Pdf.Abstractions;
using Unchained.Pdf.Core;
using Unchained.Pdf.Engine.PageResources;
using Unchained.Pdf.Models;

namespace Unchained.Pdf.Engine;

/// <summary>
///     Default <see cref="IOptionalContentEditor" /> implementation. Toggling a layer rewrites
///     the default configuration's <c>/OFF</c> array (ISO 32000-1 §8.11.4.3) and persists via
///     full-rewrite.
/// </summary>
public sealed class OptionalContentEditor : IOptionalContentEditor
{
    /// <inheritdoc />
    public Task<IReadOnlyList<OptionalContentGroup>> GetLayersAsync(
        IPdfDocument document,
        CancellationToken ct = default
    ) => Task.Run(document.GetLayers, ct);

    /// <inheritdoc />
    public Task SetLayerVisibilityAsync(
        IPdfDocument document,
        int ocgObjectNumber,
        bool visible,
        CancellationToken ct = default
    ) => Task.Run(() => SetVisibility(document, ocgObjectNumber, visible), ct);

    private static void SetVisibility(IPdfDocument document, int ocgObjectNumber, bool visible)
    {
        var adapter = MutationHelper.Cast(nameof(document), document);
        var existing = adapter.Core.CollectObjects();

        var catalog = adapter.Core.Catalog;
        var ocPropsRef = catalog[PdfName.OCProperties];
        var ocProps = adapter.Core.ResolveDict(ocPropsRef);
        if (ocProps is null)
            throw new InvalidOperationException("Document has no /OCProperties (no layers).");

        // The default config /D is usually a direct dict inside /OCProperties.
        var defaultCfg = adapter.Core.ResolveDict(ocProps[PdfName.D]);
        if (defaultCfg is null)
            throw new InvalidOperationException("Document /OCProperties has no default configuration /D.");

        var offList = (defaultCfg[PdfName.OFF] as PdfArray)?.Elements.ToList() ?? [];
        var ocgRef = new PdfIndirectReference(ocgObjectNumber, 0);

        var alreadyOff = offList.Any(e => e is PdfIndirectReference r && r.ObjectNumber == ocgObjectNumber);
        switch (visible)
        {
            case true when alreadyOff:
                offList.RemoveAll(e => e is PdfIndirectReference r && r.ObjectNumber == ocgObjectNumber);
            break;
            case false when !alreadyOff:
                offList.Add(ocgRef);
            break;
            default:
                return; // no change
        }

        var newDefault = new Dictionary<string, PdfObject>(defaultCfg.Entries)
        {
            [PdfName.OFF.Value] = new PdfArray(offList.ToArray())
        };
        var newOcProps = new Dictionary<string, PdfObject>(ocProps.Entries)
        {
            [PdfName.D.Value] = new PdfDictionary(newDefault)
        };

        // Rebuild the object holding /OCProperties. It may be the catalog inline, or its own
        // indirect object referenced from the catalog.
        var swaps = new Dictionary<int, PdfIndirectObject>();
        if (ocPropsRef is PdfIndirectReference opr)
        {
            var idx = existing.FirstOrDefault(o => o.ObjectNumber == opr.ObjectNumber);
            if (idx is not null)
                swaps[opr.ObjectNumber] = new PdfIndirectObject(opr.ObjectNumber, idx.Generation, new PdfDictionary(newOcProps));
        }
        else
        {
            // Inline in the catalog — rebuild the catalog object.
            var catNum = (adapter.Core.Trailer[PdfName.Root] as PdfIndirectReference
                          ?? throw new PdfException("Trailer missing /Root.")).ObjectNumber;
            var catObj = existing.First(o => o.ObjectNumber == catNum);
            var catEntries = new Dictionary<string, PdfObject>(((PdfDictionary)catObj.Value).Entries)
            {
                [PdfName.OCProperties.Value] = new PdfDictionary(newOcProps)
            };
            swaps[catNum] = new PdfIndirectObject(catNum, catObj.Generation, new PdfDictionary(catEntries));
        }

        var finalObjects = existing.Select(o => swaps.GetValueOrDefault(o.ObjectNumber, o)).ToList();
        MutationHelper.SerializeAndReplace(adapter, finalObjects);
    }
}
