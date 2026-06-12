using Unchained.Pdf.Abstractions;
using Unchained.Pdf.Core;
using Unchained.Pdf.Models;

namespace Unchained.Pdf.Engine;

/// <summary>Default <see cref="IViewerPreferencesEditor" /> implementation.</summary>
// ReSharper disable once MemberCanBeInternal
public sealed class ViewerPreferencesEditor : IViewerPreferencesEditor
{
    /// <inheritdoc />
    public Task SetViewerPreferencesAsync(
        IPdfDocument document,
        ViewerPreferences preferences,
        CancellationToken ct = default
    ) => Task.Run(() => Apply(document, preferences, null, null), ct);

    /// <inheritdoc />
    public Task SetPageLayoutAsync(
        IPdfDocument document,
        PageLayout layout,
        CancellationToken ct = default
    ) => Task.Run(() => Apply(document, null, layout, null), ct);

    /// <inheritdoc />
    public Task SetPageModeAsync(
        IPdfDocument document,
        PageMode mode,
        CancellationToken ct = default
    ) => Task.Run(() => Apply(document, null, null, mode), ct);

    private static void Apply(
        IPdfDocument document,
        ViewerPreferences? prefs,
        PageLayout? layout,
        PageMode? mode
    )
    {
        var adapter = Cast(document);
        var existing = adapter.Core.CollectObjects();

        var catalogObj = existing.First(static o =>
            o.Value is PdfDictionary d && d.GetName(PdfName.Type.Value) == "Catalog");
        var catalog = (PdfDictionary)catalogObj.Value;
        var catEntries = new Dictionary<string, PdfObject>(catalog.Entries);

        if (prefs is not null)
        {
            var vpEntries = new Dictionary<string, PdfObject>();
            if (prefs.HideToolbar)
                vpEntries["HideToolbar"] = PdfBoolean.True;
            if (prefs.HideMenubar)
                vpEntries["HideMenubar"] = PdfBoolean.True;
            if (prefs.HideWindowUI)
                vpEntries["HideWindowUI"] = PdfBoolean.True;
            if (prefs.FitWindow)
                vpEntries["FitWindow"] = PdfBoolean.True;
            if (prefs.CenterWindow)
                vpEntries["CenterWindow"] = PdfBoolean.True;
            if (prefs.DisplayDocTitle)
                vpEntries["DisplayDocTitle"] = PdfBoolean.True;
            if (prefs.Direction == ReadingDirection.RightToLeft)
                vpEntries["Direction"] = PdfName.Get("R2L");
            if (prefs.Duplex != DuplexMode.None)
                vpEntries["Duplex"] = PdfName.Get(prefs.Duplex.ToString());
            if (prefs.NonFullScreenPageMode != PageMode.Default && prefs.NonFullScreenPageMode != PageMode.UseNone)
                vpEntries["NonFullScreenPageMode"] = PdfName.Get(prefs.NonFullScreenPageMode.ToString());

            catEntries[PdfName.ViewerPreferences.Value] = new PdfDictionary(vpEntries);
        }

        if (layout.HasValue && layout.Value != PageLayout.Default)
            catEntries[PdfName.PageLayout.Value] = PdfName.Get(layout.Value.ToString());

        if (mode.HasValue && mode.Value != PageMode.Default)
            catEntries[PdfName.PageMode.Value] = PdfName.Get(mode.Value.ToString());

        var rebuiltCatalog = new PdfIndirectObject(catalogObj.ObjectNumber, catalogObj.Generation, new PdfDictionary(catEntries));
        var finalObjects = existing
            .Select(o => o.ObjectNumber == catalogObj.ObjectNumber ? rebuiltCatalog : o)
            .ToList();

        MutationHelper.SerializeAndReplace(adapter, finalObjects);
    }

    private static PdfDocumentAdapter Cast(IPdfDocument document) =>
        document as PdfDocumentAdapter
        ?? throw new ArgumentException(
            $"Document was not created by Unchained. Expected {nameof(PdfDocumentAdapter)}.",
            nameof(document));
}
