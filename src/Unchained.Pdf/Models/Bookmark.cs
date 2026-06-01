namespace Unchained.Pdf.Models;

/// <summary>
/// A document outline item (bookmark) as defined in ISO 32000-1 §12.3.3.
/// <param name="Title">The text shown in the bookmarks panel.</param>
/// <param name="PageNumber">1-based page number this bookmark navigates to.</param>
/// <param name="Children">Nested child bookmarks, or <see langword="null"/> for leaf items.</param>
/// </summary>
public sealed record Bookmark(
    string Title,
    int PageNumber,
    IReadOnlyList<Bookmark>? Children = null
);
