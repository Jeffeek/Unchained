using Unchained.Pdf.Core;
using Unchained.Pdf.Document;
using Unchained.Pdf.Models;

namespace Unchained.Pdf.Engine;

/// <summary>
///     Builds and installs a document <c>/OpenAction</c> in the catalog: a GoTo jump to a page,
///     or a model-supplied <see cref="PdfOpenAction" /> (GoTo / URI / Named). Extracted from
///     <see cref="DocumentProcessor" />; rewrites go through <see cref="MutationHelper.SerializeAndReplace" />.
/// </summary>
internal static class OpenActionMutator
{
    internal static void SetOpenAction(PdfDocumentAdapter adapter, int pageNumber)
    {
        if (pageNumber > adapter.Core.PageCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pageNumber),
                $"Page number {pageNumber} exceeds document page count {adapter.Core.PageCount}."
            );
        }

        var existing = adapter.Core.CollectObjects().ToList();
        var catalogRef = adapter.Core.Trailer[PdfName.Root] as PdfIndirectReference ?? throw new PdfException("Trailer missing /Root.");
        var catalogIdx = existing.FindIndex(o => o.ObjectNumber == catalogRef.ObjectNumber);
        if (catalogIdx < 0)
            throw new PdfException("Catalog object not found.");

        var catalogDict = existing[catalogIdx].Value as PdfDictionary ?? throw new PdfException("Catalog is not a dictionary.");

        // Build a GoTo action pointing at the target page.
        var pageRef = FindPageRef(adapter.Core, pageNumber);
        var dest = new PdfArray(
            [
                pageRef,
                PdfName.XYZ,
                PdfNull.Instance,
                PdfNull.Instance,
                PdfNull.Instance
            ]
        );
        var action = new PdfDictionary(
            new Dictionary<string, PdfObject>
            {
                ["Type"] = PdfName.Action,
                ["S"] = PdfName.GoTo,
                ["D"] = dest
            }
        );

        var newEntries = new Dictionary<string, PdfObject>(catalogDict.Entries)
        {
            [PdfName.OpenAction.Value] = action
        };
        existing[catalogIdx] = new PdfIndirectObject(catalogRef.ObjectNumber, 0, new PdfDictionary(newEntries));

        MutationHelper.SerializeAndReplace(adapter, existing);
    }

    internal static void SetOpenActionFromModel(PdfDocumentAdapter adapter, PdfOpenAction action)
    {
        var existing = adapter.Core.CollectObjects().ToList();
        var catalogRef = adapter.Core.Trailer[PdfName.Root] as PdfIndirectReference ?? throw new PdfException("Trailer missing /Root.");
        var catalogIdx = existing.FindIndex(o => o.ObjectNumber == catalogRef.ObjectNumber);
        if (catalogIdx < 0)
            throw new PdfException("Catalog object not found.");

        var catalogDict = existing[catalogIdx].Value as PdfDictionary ?? throw new PdfException("Catalog is not a dictionary.");

        PdfObject openAction = action switch
        {
            PdfOpenAction.GoToAction g => BuildGoToAction(adapter.Core, g.PageNumber),
            PdfOpenAction.UriAction u => BuildUriAction(u.UriString),
            PdfOpenAction.NamedAction n => BuildNamedAction(n.ActionName),
            _ => throw new ArgumentException($"Unknown PdfOpenAction type: {action.GetType().Name}")
        };

        var newEntries = new Dictionary<string, PdfObject>(catalogDict.Entries)
        {
            [PdfName.OpenAction.Value] = openAction
        };
        existing[catalogIdx] = new PdfIndirectObject(
            catalogRef.ObjectNumber,
            0,
            new PdfDictionary(newEntries)
        );
        MutationHelper.SerializeAndReplace(adapter, existing);
    }

    private static PdfDictionary BuildGoToAction(PdfDocumentCore core, int pageNumber)
    {
        if (pageNumber > core.PageCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pageNumber),
                $"Page number {pageNumber} exceeds document page count {core.PageCount}."
            );
        }

        var pageRef = FindPageRef(core, pageNumber);
        var dest = new PdfArray(
            [
                pageRef,
                PdfName.XYZ,
                PdfNull.Instance, PdfNull.Instance, PdfNull.Instance
            ]
        );
        return new PdfDictionary(
            new Dictionary<string, PdfObject>
            {
                ["Type"] = PdfName.Action,
                ["S"] = PdfName.GoTo,
                ["D"] = dest
            }
        );
    }

    private static PdfDictionary BuildUriAction(string uri) =>
        new(
            new Dictionary<string, PdfObject>
            {
                ["Type"] = PdfName.Action,
                ["S"] = PdfName.URI,
                ["URI"] = PdfString.FromLatin1(uri)
            }
        );

    private static PdfDictionary BuildNamedAction(string name) =>
        new(
            new Dictionary<string, PdfObject>
            {
                ["Type"] = PdfName.Action,
                ["S"] = PdfName.Named,
                ["N"] = PdfName.Get(name)
            }
        );

    private static PdfIndirectReference FindPageRef(PdfDocumentCore core, int pageNumber)
    {
        var pageDict = core.GetPage(pageNumber);

        foreach (var obj in core.CollectObjects().Where(obj => ReferenceEquals(obj.Value, pageDict)))
            return new PdfIndirectReference(obj.ObjectNumber, obj.Generation);

        throw new PdfException($"Could not find indirect reference for page {pageNumber}.");
    }
}
