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
                [PdfName.Type.Value] = PdfName.Action,
                [PdfName.S.Value] = PdfName.GoTo,
                [PdfName.D.Value] = dest
            }
        );

        MutationHelper.ApplyCatalogMutation(
            adapter,
            entries =>
            {
                entries[PdfName.OpenAction.Value] = action;
            }
        );
    }

    internal static void SetOpenActionFromModel(PdfDocumentAdapter adapter, PdfOpenAction action)
    {
        PdfObject openAction = action switch
        {
            PdfOpenAction.GoToAction g => BuildGoToAction(adapter.Core, g.PageNumber),
            PdfOpenAction.UriAction u => BuildUriAction(u.UriString),
            PdfOpenAction.NamedAction n => BuildNamedAction(n.ActionName),
            _ => throw new ArgumentException($"Unknown PdfOpenAction type: {action.GetType().Name}")
        };

        MutationHelper.ApplyCatalogMutation(
            adapter,
            entries =>
            {
                entries[PdfName.OpenAction.Value] = openAction;
            }
        );
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
                [PdfName.Type.Value] = PdfName.Action,
                [PdfName.S.Value] = PdfName.GoTo,
                [PdfName.D.Value] = dest
            }
        );
    }

    private static PdfDictionary BuildUriAction(string uri) =>
        new(
            new Dictionary<string, PdfObject>
            {
                [PdfName.Type.Value] = PdfName.Action,
                [PdfName.S.Value] = PdfName.URI,
                [PdfName.URI.Value] = PdfString.FromLatin1(uri)
            }
        );

    private static PdfDictionary BuildNamedAction(string name) =>
        new(
            new Dictionary<string, PdfObject>
            {
                [PdfName.Type.Value] = PdfName.Action,
                [PdfName.S.Value] = PdfName.Named,
                [PdfName.N.Value] = PdfName.Get(name)
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
