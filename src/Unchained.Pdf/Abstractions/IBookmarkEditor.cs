using Unchained.Pdf.Models;

namespace Unchained.Pdf.Abstractions;

/// <summary>
///     Reads and writes the document outline (bookmarks) tree.
/// </summary>
public interface IBookmarkEditor
{
    /// <summary>
    ///     Replaces the entire <c>/Outlines</c> tree with <paramref name="bookmarks" />.
    ///     Pass an empty list to remove all bookmarks.
    ///     The document is mutated in-place.
    /// </summary>
    Task SetBookmarksAsync(
        IPdfDocument document,
        IReadOnlyList<Bookmark> bookmarks,
        CancellationToken ct = default
    );
}
