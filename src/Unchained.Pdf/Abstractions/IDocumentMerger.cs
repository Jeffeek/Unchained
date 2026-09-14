using Unchained.Pdf.Models;

namespace Unchained.Pdf.Abstractions;

/// <summary>
///     Combines multiple PDF documents into a single document.
///     <para>
///         Large-file merging is memory-critical: implementations must open, copy, and dispose
///         source documents one at a time rather than loading all simultaneously to avoid multi-GB RSS spikes.
///     </para>
/// </summary>
public interface IDocumentMerger
{
    /// <summary>
    ///     Merges <paramref name="documents" /> into a new PDF document in the order supplied.
    ///     Callers retain ownership of each input document — they are not disposed by this method.
    ///     The returned document is caller-owned.
    /// </summary>
    /// <param name="documents">Ordered list of source documents to merge.</param>
    /// <param name="options">Controls which metadata (outlines, named destinations) is copied.</param>
    /// <param name="ct">Token to cancel the operation.</param>
    /// <returns>A new <see cref="IPdfDocument" /> containing all pages from <paramref name="documents" />.</returns>
    Task<IPdfDocument> MergeAsync(
        IReadOnlyList<IPdfDocument> documents,
        MergeOptions options,
        CancellationToken ct = default
    );

    /// <summary>
    ///     Merges PDF documents from raw streams into a single new document.
    ///     Streams are consumed in order; each is processed and released before the next is opened,
    ///     keeping peak memory proportional to the largest single document rather than the total.
    ///     The returned document is caller-owned.
    /// </summary>
    /// <param name="streams">Ordered list of readable streams, each containing a complete PDF.</param>
    /// <param name="options">Controls which metadata is copied into the merged output.</param>
    /// <param name="ct">Token to cancel the operation.</param>
    /// <returns>A new <see cref="IPdfDocument" /> containing all pages from <paramref name="streams" />.</returns>
    Task<IPdfDocument> MergeAsync(
        IReadOnlyList<Stream> streams,
        MergeOptions options,
        CancellationToken ct = default
    );

    /// <summary>
    ///     Merges selected page ranges from several documents into a single new document.
    ///     Each <see cref="MergeSource" /> contributes the pages named by its ranges, in the order
    ///     given; a source with no ranges contributes all its pages. This is the one-call
    ///     equivalent of splitting each source and merging the pieces.
    ///     Callers retain ownership of each input document — they are not disposed by this method.
    ///     The returned document is caller-owned.
    /// </summary>
    /// <param name="sources">Ordered list of documents and the page ranges to take from each.</param>
    /// <param name="options">Controls which metadata is copied into the merged output.</param>
    /// <param name="ct">Token to cancel the operation.</param>
    /// <returns>A new <see cref="IPdfDocument" /> containing the selected pages in source order.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when a range falls outside the bounds of its source document.
    /// </exception>
    Task<IPdfDocument> MergeAsync(
        IReadOnlyList<MergeSource> sources,
        MergeOptions options,
        CancellationToken ct = default
    );
}
