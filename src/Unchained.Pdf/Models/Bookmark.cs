namespace Unchained.Pdf.Models;

/// <summary>
/// A document outline item (bookmark) as defined in ISO 32000-1 §12.3.3.
/// </summary>
public sealed record Bookmark(
    /// <summary>The text shown in the bookmarks panel.</summary>
    string Title,
    /// <summary>1-based page number this bookmark navigates to.</summary>
    int PageNumber,
    /// <summary>Nested child bookmarks, or <see langword="null"/> for leaf items.</summary>
    IReadOnlyList<Bookmark>? Children = null
);
