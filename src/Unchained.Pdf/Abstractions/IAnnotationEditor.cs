using Unchained.Pdf.Models;

namespace Unchained.Pdf.Abstractions;

/// <summary>
/// Adds and removes annotations from PDF pages.
/// </summary>
// ReSharper disable once MemberCanBeInternal
public interface IAnnotationEditor
{
    /// <summary>
    /// Appends <paramref name="annotation"/> to the <c>/Annots</c> array of the specified page.
    /// The document is mutated in-place.
    /// </summary>
    /// <param name="document"></param>
    /// <param name="pageNumber">1-based page number.</param>
    /// <param name="annotation"></param>
    /// <param name="ct"></param>
    Task AddAnnotationAsync(
        IPdfDocument document,
        int pageNumber,
        Annotation annotation,
        CancellationToken ct = default
    );
}
