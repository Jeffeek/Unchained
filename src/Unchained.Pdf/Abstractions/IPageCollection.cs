namespace Unchained.Pdf.Abstractions;

/// <summary>
/// A read-only, ordered collection of the pages within an <see cref="IPdfDocument"/>.
/// Indexing is 1-based to match the PDF spec (ISO 32000-1 §7.7.3).
/// </summary>
public interface IPageCollection : IReadOnlyList<IPdfPage>
{
    /// <summary>
    /// Returns the page at the given 1-based <paramref name="pageNumber"/>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="pageNumber"/> is less than 1 or greater than <see cref="IReadOnlyCollection{T}.Count"/>.
    /// </exception>
    new IPdfPage this[int pageNumber] { get; }
}
