namespace Unchained.Pdf.Abstractions;

/// <summary>
/// Reduces the file size of a PDF document by removing redundancy and
/// compressing uncompressed streams (ISO 32000-1 §C).
/// </summary>
public interface IDocumentOptimizer
{
    /// <summary>
    /// Compresses any uncompressed content streams using <c>FlateDecode</c> and
    /// removes redundant objects. The document is mutated in-place.
    /// </summary>
    Task OptimizeAsync(IPdfDocument document, CancellationToken ct = default);

    /// <summary>
    /// Deduplicates identical embedded streams (font programs, image data) so shared
    /// content is stored only once. The document is mutated in-place.
    /// </summary>
    Task OptimizeResourcesAsync(IPdfDocument document, CancellationToken ct = default);
}
