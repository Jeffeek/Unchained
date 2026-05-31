namespace Unchained.Pdf.Models;

/// <summary>
/// Immutable snapshot of the document information dictionary (<c>/Info</c>),
/// as defined in ISO 32000-1 §14.3.3 Table 317.
/// All fields are optional; a value of <see langword="null"/> means the entry
/// is absent or could not be decoded.
/// <param name="Title">The document title (<c>/Title</c>).</param>
/// <param name="Author">The name of the person who created the document (<c>/Author</c>).</param>
/// <param name="Subject">The subject of the document (<c>/Subject</c>).</param>
/// <param name="Keywords">Keywords associated with the document (<c>/Keywords</c>).</param>
/// <param name="Creator">The application that created the original document (<c>/Creator</c>).</param>
/// <param name="Producer">The application that converted the document to PDF (<c>/Producer</c>).</param>
/// <param name="CreationDate">The date and time the document was created (<c>/CreationDate</c>).</param>
/// <param name="ModificationDate">The date and time the document was most recently modified (<c>/ModDate</c>).</param>
/// </summary>
public sealed record DocumentMetadata(
    string? Title,
    string? Author,
    string? Subject,
    string? Keywords,
    string? Creator,
    string? Producer,
    DateTimeOffset? CreationDate,
    DateTimeOffset? ModificationDate
)
{
    /// <summary>
    /// A shared instance representing a document with no information dictionary.
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
