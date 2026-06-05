using Unchained.Pdf.Models;

namespace Unchained.Pdf.Abstractions;

/// <summary>
/// Reads and writes the document's page label ranges (<c>/PageLabels</c> number tree,
/// ISO 32000-1 §12.4.2). Page labels define the logical page numbering shown in PDF
/// reader toolbars and outlines (e.g. "i, ii, iii" for a preface followed by "1, 2, 3").
/// </summary>
public interface IPageLabelEditor
{
    /// <summary>
    /// Returns all page label ranges defined in the document's <c>/PageLabels</c> number tree,
    /// ordered by <see cref="PageLabelRange.StartPageIndex"/>.
    /// Returns an empty list when the document has no <c>/PageLabels</c> entry.
    /// </summary>
    IReadOnlyList<PageLabelRange> GetPageLabels(IPdfDocument document);

    /// <summary>
    /// Replaces the document's <c>/PageLabels</c> number tree with <paramref name="ranges"/>.
    /// The first range must have <see cref="PageLabelRange.StartPageIndex"/> equal to 0.
    /// Ranges must be ordered by <see cref="PageLabelRange.StartPageIndex"/> and must not overlap.
    /// </summary>
    /// <param name="document">The document to update.</param>
    /// <param name="ranges">The new page label ranges. Must not be empty.</param>
    /// <param name="ct">Token to cancel the operation.</param>
    Task SetPageLabelsAsync(
        IPdfDocument document,
        IReadOnlyList<PageLabelRange> ranges,
        CancellationToken ct = default
    );

    /// <summary>
    /// Removes the <c>/PageLabels</c> entry from the document catalog.
    /// PDF readers will fall back to their default sequential Arabic numbering.
    /// </summary>
    /// <param name="document">The document to update.</param>
    /// <param name="ct">Token to cancel the operation.</param>
    Task RemovePageLabelsAsync(
        IPdfDocument document,
        CancellationToken ct = default
    );
}
