using Unchained.Pdf.Abstractions;

namespace Unchained.Pdf.Models;

/// <summary>
///     A single input to a page-range merge: a source document paired with the page ranges to
///     take from it. Use with <see cref="IDocumentMerger.MergeAsync(IReadOnlyList{MergeSource}, MergeOptions, CancellationToken)" />
///     to combine selected page ranges from several documents in one call.
/// </summary>
/// <param name="Document">The source document. Not disposed by the merge; caller retains ownership.</param>
/// <param name="PageRanges">
///     Inclusive, 1-based page ranges to take from <paramref name="Document" />, applied in the
///     order given (so ranges may reorder or repeat pages). When <see langword="null" />, every
///     page of the document is included in natural order.
/// </param>
public sealed record MergeSource(
    IPdfDocument Document,
    IReadOnlyList<(int Start, int End)>? PageRanges = null
);
