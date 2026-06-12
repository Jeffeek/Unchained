namespace Unchained.Pdf.Models;

/// <summary>
///     Represents a PDF document open action (ISO 32000-1 §12.6).
///     Use the static factory methods to create the desired action type.
/// </summary>
public abstract class PdfOpenAction
{
    private PdfOpenAction() { }

    /// <summary>
    ///     Navigates to the given 1-based page number when the document is opened.
    ///     Writes a <c>/S /GoTo</c> action with an <c>XYZ</c> destination (top of page, inherit zoom).
    /// </summary>
    /// <param name="pageNumber">1-based page number.</param>
    public static PdfOpenAction GoTo(int pageNumber) => new GoToAction(pageNumber);

    /// <summary>
    ///     Opens the given URI in the default browser when the document is opened.
    ///     Writes a <c>/S /URI</c> action per ISO 32000-1 §12.6.4.7.
    /// </summary>
    /// <param name="uri">The URI to open (e.g. <c>"https://example.com"</c>).</param>
    public static PdfOpenAction Uri(string uri) => new UriAction(uri);

    /// <summary>
    ///     Executes a named action when the document is opened (e.g. <c>"NextPage"</c>,
    ///     <c>"PrevPage"</c>, <c>"FirstPage"</c>, <c>"LastPage"</c>, <c>"Find"</c>).
    ///     Writes a <c>/S /Named</c> action per ISO 32000-1 §12.6.4.11.
    /// </summary>
    /// <param name="name">PDF named action identifier (e.g. <c>"NextPage"</c>).</param>
    public static PdfOpenAction Named(string name) => new NamedAction(name);

    // ── Concrete subtypes ────────────────────────────────────────────────────

    internal sealed class GoToAction(int pageNumber) : PdfOpenAction
    {
        internal int PageNumber { get; } = pageNumber;
    }

    internal sealed class UriAction(string uri) : PdfOpenAction
    {
        internal string UriString { get; } = uri;
    }

    internal sealed class NamedAction(string name) : PdfOpenAction
    {
        internal string ActionName { get; } = name;
    }
}
