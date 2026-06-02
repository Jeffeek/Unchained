using Unchained.Pdf.Models;

namespace Unchained.Pdf.Abstractions;

/// <summary>
/// Reads and writes the viewer-preference settings stored in the document catalog's
/// <c>/ViewerPreferences</c> dictionary, as well as the top-level <c>/PageLayout</c>,
/// <c>/PageMode</c>, and <c>/OpenAction</c> catalog entries.
/// </summary>
public interface IViewerPreferencesEditor
{
    /// <summary>
    /// Replaces the document's <c>/ViewerPreferences</c> dictionary with the values in
    /// <paramref name="preferences"/>. The document is mutated in-place.
    /// </summary>
    Task SetViewerPreferencesAsync(
        IPdfDocument document,
        ViewerPreferences preferences,
        CancellationToken ct = default
    );

    /// <summary>
    /// Sets the <c>/PageLayout</c> entry in the document catalog.
    /// The document is mutated in-place.
    /// </summary>
    Task SetPageLayoutAsync(
        IPdfDocument document,
        PageLayout layout,
        CancellationToken ct = default
    );

    /// <summary>
    /// Sets the <c>/PageMode</c> entry in the document catalog.
    /// The document is mutated in-place.
    /// </summary>
    Task SetPageModeAsync(
        IPdfDocument document,
        PageMode mode,
        CancellationToken ct = default
    );
}
