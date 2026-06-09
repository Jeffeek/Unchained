namespace Unchained.Pdf.Abstractions;

/// <summary>
/// Page-level document operations: rotate, delete, reorder, insert, and split.
/// <para>
/// All operations work against documents created by Unchained (a
/// <c>PdfDocumentAdapter</c>). Mutating operations rewrite the document in place via
/// full-rewrite serialization; <see cref="SplitAsync"/> returns new caller-owned documents.
/// Page numbers are 1-based throughout, matching <see cref="IPdfDocument.Pages"/>.
/// </para>
/// </summary>
public interface IPageOrganizer
{
    /// <summary>
    /// Sets the clockwise display rotation of the given pages. The angle is normalised to
    /// one of 0, 90, 180, 270 (ISO 32000-1 §7.7.3.3, <c>/Rotate</c>) and is applied
    /// <em>relative</em> to each page's existing rotation when <paramref name="relative"/>
    /// is <see langword="true"/>, or as an absolute value otherwise.
    /// </summary>
    /// <param name="document">Document to mutate in place.</param>
    /// <param name="pageNumbers">1-based page numbers to rotate.</param>
    /// <param name="degrees">Rotation in degrees; any multiple of 90 (may be negative).</param>
    /// <param name="relative">Add to the existing rotation when true; set absolutely when false.</param>
    /// <param name="ct">Cancellation token.</param>
    Task RotatePagesAsync(
        IPdfDocument document,
        IReadOnlyList<int> pageNumbers,
        int degrees,
        bool relative = true,
        CancellationToken ct = default
    );

    /// <summary>
    /// Removes the given pages from the document. At least one page must remain.
    /// </summary>
    /// <param name="document">Document to mutate in place.</param>
    /// <param name="pageNumbers">1-based page numbers to delete (duplicates ignored).</param>
    /// <param name="ct">Cancellation token.</param>
    Task DeletePagesAsync(
        IPdfDocument document,
        IReadOnlyList<int> pageNumbers,
        CancellationToken ct = default
    );

    /// <summary>
    /// Reorders the document's pages to match <paramref name="newOrder"/>, a permutation of
    /// the 1-based page numbers <c>1..PageCount</c>. Every existing page must appear exactly once.
    /// </summary>
    /// <param name="document">Document to mutate in place.</param>
    /// <param name="newOrder">The new page sequence as 1-based original page numbers.</param>
    /// <param name="ct">Cancellation token.</param>
    Task ReorderPagesAsync(
        IPdfDocument document,
        IReadOnlyList<int> newOrder,
        CancellationToken ct = default
    );

    /// <summary>
    /// Inserts all pages of <paramref name="source"/> into <paramref name="document"/> so that
    /// the first inserted page becomes page <paramref name="atPageNumber"/>. Use
    /// <c>atPageNumber = PageCount + 1</c> to append. The source document is not modified.
    /// </summary>
    /// <param name="document">Destination document, mutated in place.</param>
    /// <param name="atPageNumber">1-based position the first inserted page will occupy.</param>
    /// <param name="source">Document whose pages are copied in.</param>
    /// <param name="ct">Cancellation token.</param>
    Task InsertPagesAsync(
        IPdfDocument document,
        int atPageNumber,
        IPdfDocument source,
        CancellationToken ct = default
    );

    /// <summary>
    /// Splits the document into multiple new documents, one per group of consecutive pages
    /// defined by <paramref name="ranges"/>. Each range is an inclusive 1-based
    /// <c>(Start, End)</c> page span. The input document is not modified; the returned
    /// documents are caller-owned.
    /// </summary>
    /// <param name="document">Source document (not modified).</param>
    /// <param name="ranges">Inclusive 1-based page ranges, one per output document.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>One new <see cref="IPdfDocument"/> per range, in the order supplied.</returns>
    Task<IReadOnlyList<IPdfDocument>> SplitAsync(
        IPdfDocument document,
        IReadOnlyList<(int Start, int End)> ranges,
        CancellationToken ct = default
    );
}
