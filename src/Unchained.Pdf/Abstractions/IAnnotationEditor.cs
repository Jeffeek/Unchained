using Unchained.Pdf.Models;

namespace Unchained.Pdf.Abstractions;

/// <summary>Adds annotations to PDF pages.</summary>
// ReSharper disable once MemberCanBeInternal
public interface IAnnotationEditor
{
    /// <summary>
    /// Appends <paramref name="annotation"/> to the <c>/Annots</c> array of the specified page.
    /// The document is mutated in-place.
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
}
