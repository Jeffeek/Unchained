using Unchained.Pdf.Models;

namespace Unchained.Pdf.Abstractions;

/// <summary>Adds annotations to PDF pages.</summary>
// ReSharper disable once MemberCanBeInternal
public interface IAnnotationEditor
{
    /// <summary>
    ///     Appends <paramref name="annotation" /> to the <c>/Annots</c> array of the specified page.
    ///     The document is mutated in-place.
    /// </summary>
    /// <param name="document">The document to annotate. Must not be disposed.</param>
    /// <param name="pageNumber">1-based page number.</param>
    /// <param name="annotation">The annotation to append.</param>
    /// <param name="ct">Token to cancel the operation.</param>
    Task AddAnnotationAsync(
        IPdfDocument document,
        int pageNumber,
        Annotation annotation,
        CancellationToken ct = default
    );

    /// <summary>
    ///     Removes the annotation at <paramref name="annotationIndex" /> from the specified page.
    ///     The document is mutated in-place.
    /// </summary>
    /// <param name="document">The document to modify. Must not be disposed.</param>
    /// <param name="pageNumber">1-based page number.</param>
    /// <param name="annotationIndex">
    ///     Zero-based index into the annotations returned by <see cref="IPdfPage.GetAnnotations" />
    ///     for this page.
    /// </param>
    /// <param name="ct">Token to cancel the operation.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="annotationIndex" /> is negative or does not correspond to an
    ///     annotation on the page.
    /// </exception>
    Task RemoveAnnotationAsync(
        IPdfDocument document,
        int pageNumber,
        int annotationIndex,
        CancellationToken ct = default
    );

    /// <summary>
    ///     Replaces the annotation at <paramref name="annotationIndex" /> on the specified page with
    ///     <paramref name="annotation" />. The document is mutated in-place.
    /// </summary>
    /// <param name="document">The document to modify. Must not be disposed.</param>
    /// <param name="pageNumber">1-based page number.</param>
    /// <param name="annotationIndex">
    ///     Zero-based index into the annotations returned by <see cref="IPdfPage.GetAnnotations" />
    ///     for this page.
    /// </param>
    /// <param name="annotation">The replacement annotation.</param>
    /// <param name="ct">Token to cancel the operation.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="annotationIndex" /> is negative or does not correspond to an
    ///     annotation on the page.
    /// </exception>
    Task UpdateAnnotationAsync(
        IPdfDocument document,
        int pageNumber,
        int annotationIndex,
        Annotation annotation,
        CancellationToken ct = default
    );
}
