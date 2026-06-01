using Unchained.Pdf.Models;

namespace Unchained.Pdf.Abstractions;

/// <summary>
/// Adds and removes annotations from PDF pages.
/// </summary>
public interface IAnnotationEditor
{
    /// <summary>
    /// Appends <paramref name="annotation"/> to the <c>/Annots</c> array of the specified page.
    /// The document is mutated in-place.
    /// </summary>
    /// <param name="pageNumber">1-based page number.</param>
    Task AddAnnotationAsync(
        IPdfDocument document,
        int pageNumber,
        Annotation annotation,
        CancellationToken ct = default
    );
}
