namespace Unchained.Pdf.Models;

/// <summary>
/// Immutable snapshot of the document information dictionary (<c>/Info</c>),
/// as defined in ISO 32000-1 §14.3.3 Table 317.
/// All fields are optional; a value of <see langword="null"/> means the entry
/// is absent or could not be decoded.
/// </summary>
public sealed record DocumentMetadata(
    /// <summary>The document title (<c>/Title</c>).</summary>
    string? Title,
    /// <summary>The name of the person who created the document (<c>/Author</c>).</summary>
    string? Author,
    /// <summary>The subject of the document (<c>/Subject</c>).</summary>
    string? Subject,
    /// <summary>Keywords associated with the document (<c>/Keywords</c>).</summary>
    string? Keywords,
    /// <summary>The application that created the original document (<c>/Creator</c>).</summary>
    string? Creator,
    /// <summary>The application that converted the document to PDF (<c>/Producer</c>).</summary>
    string? Producer,
    /// <summary>The date and time the document was created (<c>/CreationDate</c>).</summary>
    DateTimeOffset? CreationDate,
    /// <summary>The date and time the document was most recently modified (<c>/ModDate</c>).</summary>
    DateTimeOffset? ModificationDate
)
{
    /// <summary>
    /// A shared instance representing a document with no information dictionary.
    /// Returned by <see cref="IPdfDocument.Metadata"/> when <c>/Info</c> is absent.
    /// </summary>
    // ReSharper disable once MemberCanBeInternal
    public static readonly DocumentMetadata Empty = new(
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null
    );
}
