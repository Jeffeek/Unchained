using Unchained.Pdf.Models;

namespace Unchained.Pdf.Abstractions;

/// <summary>
///     Permanently removes page content within rectangular regions. Unlike a covering
///     annotation, redaction deletes the underlying text-show and image-paint operators from the
///     content stream so the data cannot be recovered, then paints the area with the region's
///     fill colour.
///     <para>
///         Text removal is per text-show operator: an operator is dropped when its drawing origin
///         lies inside the region. Glyph-level partial redaction within a single show operator is not
///         performed (the whole run is removed), so callers should sense-check regions against text
///         runs. Image XObjects whose placement falls inside the region are removed.
///     </para>
/// </summary>
public interface IRedactor
{
    /// <summary>
    ///     Applies all <paramref name="regions" /> to <paramref name="document" />, mutating it in
    ///     place. Regions may target different pages.
    /// </summary>
    /// <param name="document">Document to redact in place.</param>
    /// <param name="regions">Rectangular regions to redact (1-based page numbers).</param>
    /// <param name="ct">Cancellation token.</param>
    Task RedactAsync(
        IPdfDocument document,
        IReadOnlyList<RedactionRegion> regions,
        CancellationToken ct = default
    );
}
