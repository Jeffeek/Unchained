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
}
