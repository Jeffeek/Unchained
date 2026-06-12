namespace Unchained.Pdf.Models;

/// <summary>
///     A named destination in a PDF document (ISO 32000-1 §12.3.2).
///     Named destinations allow hyperlinks and bookmarks to reference a page by name
///     rather than by direct page reference.
/// </summary>
/// <param name="Name">The destination name as it appears in the <c>/Names /Dests</c> tree.</param>
/// <param name="PageNumber">1-based page number this destination navigates to.</param>
public sealed record NamedDestination(
    string Name,
    int PageNumber
);
