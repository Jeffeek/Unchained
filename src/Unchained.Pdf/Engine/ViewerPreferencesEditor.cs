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
        var adapter = MutationHelper.Cast(nameof(document), document);
        var existing = adapter.Core.CollectObjects();

        var finalObjects = MutationHelper.ModifyCatalog(
            adapter,
            existing,
            catEntries =>
            {
                if (prefs is not null)
                {
                    var vpEntries = new Dictionary<string, PdfObject>();
                    if (prefs.HideToolbar)
                        vpEntries[PdfName.HideToolbar.Value] = PdfBoolean.True;
                    if (prefs.HideMenubar)
                        vpEntries[PdfName.HideMenubar.Value] = PdfBoolean.True;
                    if (prefs.HideWindowUI)
                        vpEntries[PdfName.HideWindowUI.Value] = PdfBoolean.True;
                    if (prefs.FitWindow)
                        vpEntries[PdfName.FitWindow.Value] = PdfBoolean.True;
                    if (prefs.CenterWindow)
                        vpEntries[PdfName.CenterWindow.Value] = PdfBoolean.True;
                    if (prefs.DisplayDocTitle)
                        vpEntries[PdfName.DisplayDocTitle.Value] = PdfBoolean.True;
                    if (prefs.Direction == ReadingDirection.RightToLeft)
                        vpEntries["Direction"] = PdfName.R2L;
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
            }
        );

        MutationHelper.SerializeAndReplace(adapter, finalObjects);
    }
}
