using Unchained.Pdf.Models;

namespace Unchained.Pdf.Abstractions;


/// <summary>
/// Represents a loaded PDF document.
/// <para>
/// Implementations must be thread-safe for concurrent read operations
/// (e.g., accessing <see cref="Pages"/> from multiple threads simultaneously).
/// Write operations (mutation) are not yet supported in this version.
/// </para>
/// <para>
/// Callers own the document lifetime. Dispose when done to release the underlying
/// document resources and object cache.
/// </para>
/// </summary>
public interface IPdfDocument : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// The total number of pages in this document.
    /// Equivalent to <c>Pages.Count</c>.
    /// </summary>
    int PageCount { get; }

    /// <summary>
    /// The ordered, 1-based collection of pages in this document.
    /// </summary>
    IPageCollection Pages { get; }

    /// <summary>
    /// Metadata from the document's information dictionary (<c>/Info</c>).
    /// Returns <see cref="DocumentMetadata.Empty"/> when the document has no <c>/Info</c> entry.
    /// </summary>
    DocumentMetadata Metadata { get; }

    /// <summary>
    /// <see langword="true"/> after <see cref="IDisposable.Dispose"/> or
    /// <see cref="IAsyncDisposable.DisposeAsync"/> has been called.
    /// Accessing other members on a disposed document results in undefined behaviour.
    /// </summary>
    bool IsDisposed { get; }

    /// <summary>
    /// Returns the document's outline (bookmark) tree, parsed from <c>/Outlines</c>.
    /// Returns an empty list when the document has no bookmarks.
    /// </summary>
    IReadOnlyList<Bookmark> GetBookmarks();

    /// <summary>
    /// Returns all AcroForm fields, parsed from <c>/AcroForm /Fields</c>.
    /// Returns an empty list when the document has no form fields.
    /// </summary>
    IReadOnlyList<FormField> GetFormFields();
}
