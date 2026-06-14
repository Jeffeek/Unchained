using Unchained.Pdf.Models;

namespace Unchained.Pdf.Abstractions;

/// <summary>
///     Represents a loaded PDF document.
///     <para>
///         Implementations must be thread-safe for concurrent read operations
///         (e.g., accessing <see cref="Pages" /> from multiple threads simultaneously).
///         Write operations (mutation) are not yet supported in this version.
///     </para>
///     <para>
///         Callers own the document lifetime. Dispose when done to release the underlying
///         document resources and object cache.
///     </para>
/// </summary>
public interface IPdfDocument : IDisposable, IAsyncDisposable
{
    /// <summary>The total number of pages in this document. Equivalent to <c>Pages.Count</c>.</summary>
    int PageCount { get; }

    /// <summary>The ordered, 1-based collection of pages in this document.</summary>
    IPageCollection Pages { get; }

    /// <summary>
    ///     Metadata from the document's information dictionary (<c>/Info</c>).
    ///     Returns <see cref="DocumentMetadata.Empty" /> when the document has no <c>/Info</c> entry.
    /// </summary>
    DocumentMetadata Metadata { get; }

    /// <summary>
    ///     <see langword="true" /> when this document was loaded from an encrypted PDF file.
    ///     All content is already decrypted in memory; this flag is informational.
    /// </summary>
    bool IsEncrypted { get; }

    /// <summary>
    ///     Operations permitted when the document is opened with the user password.
    ///     Returns <see cref="PdfPermissions.All" /> for unencrypted documents (no restrictions).
    /// </summary>
    PdfPermissions Permissions { get; }

    /// <summary>
    ///     The encryption algorithm used to protect this document, or
    ///     <see langword="null" /> when the document is not encrypted.
    /// </summary>
    PdfEncryptionAlgorithm? CryptoAlgorithm { get; }

    /// <summary>
    ///     <see langword="true" /> after <see cref="IDisposable.Dispose" /> or
    ///     <see cref="IAsyncDisposable.DisposeAsync" /> has been called.
    /// </summary>
    bool IsDisposed { get; }

    /// <summary>
    ///     <see langword="true" /> when the PDF was saved in linearized (web-optimized) form
    ///     (ISO 32000-1 Annex F). Detected by the presence of a <c>/Linearized</c> entry in the
    ///     first indirect object of the file.
    /// </summary>
    bool IsLinearized { get; }

    /// <summary>
    ///     <see langword="true" /> when the document contains a <c>/MarkInfo /Marked true</c>
    ///     entry in the catalog, indicating that content is tagged for accessibility.
    /// </summary>
    bool IsTagged { get; }

    /// <summary>
    ///     <see langword="true" /> when the document's XMP metadata declares PDF/A conformance
    ///     (contains a valid <c>pdfaid:part</c> property).
    ///     This is a lightweight metadata check — use
    ///     <c>IDocumentProcessor.ValidatePdfAAsync</c> for full structural validation.
    /// </summary>
    bool IsPdfaCompliant { get; }

    /// <summary>
    ///     <see langword="true" /> when the document's XMP metadata declares PDF/UA conformance
    ///     (contains a valid <c>pdfuaid:part</c> property).
    ///     This is a lightweight metadata check — use
    ///     <c>IDocumentProcessor.ValidatePdfUAAsync</c> for full structural validation.
    /// </summary>
    bool IsPdfUaCompliant { get; }

    /// <summary>
    ///     The document identifier from the trailer's <c>/ID</c> array, as a pair of
    ///     hex-encoded strings. Returns <see langword="null" /> when the trailer has no
    ///     <c>/ID</c> entry.
    /// </summary>
    (string First, string Second)? Id { get; }

    /// <summary>
    ///     Returns the initial page layout from the catalog's <c>/PageLayout</c> entry,
    ///     or <see cref="PageLayout.Default" /> when not specified.
    /// </summary>
    PageLayout PageLayout { get; }

    /// <summary>
    ///     Returns the initial page mode from the catalog's <c>/PageMode</c> entry,
    ///     or <see cref="PageMode.Default" /> when not specified.
    /// </summary>
    PageMode PageMode { get; }

    /// <summary>
    ///     Returns the document's outline (bookmark) tree, parsed from <c>/Outlines</c>.
    ///     Returns an empty list when the document has no bookmarks.
    /// </summary>
    IReadOnlyList<Bookmark> GetBookmarks();

    /// <summary>
    ///     Returns all AcroForm fields, parsed from <c>/AcroForm /Fields</c>.
    ///     Returns an empty list when the document has no form fields.
    /// </summary>
    IReadOnlyList<FormField> GetFormFields();

    /// <summary>
    ///     Returns the document's viewer preferences from the catalog's
    ///     <c>/ViewerPreferences</c> dictionary. Returns <see cref="ViewerPreferences.Default" />
    ///     when no preferences are set.
    /// </summary>
    ViewerPreferences GetViewerPreferences();

    /// <summary>
    ///     Returns the raw XMP metadata XML from the catalog's <c>/Metadata</c> stream,
    ///     or <see langword="null" /> when no XMP metadata is present.
    /// </summary>
    string? GetXmpMetadata();

    /// <summary>
    ///     Returns all named destinations from the document's <c>/Names /Dests</c> tree
    ///     (or the legacy <c>/Dests</c> dict). Returns an empty list when none exist.
    /// </summary>
    IReadOnlyList<NamedDestination> GetNamedDestinations();

    /// <summary>
    ///     Returns the document's optional content groups ("layers", <c>/OCProperties</c>),
    ///     each with its name and default visibility. Returns an empty list when the document has
    ///     no layers. Default interface implementation returns empty for non-Unchained documents.
    /// </summary>
    IReadOnlyList<OptionalContentGroup> GetLayers() => [];
}
