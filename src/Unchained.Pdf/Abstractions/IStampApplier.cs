using Unchained.Pdf.Models;

namespace Unchained.Pdf.Abstractions;

/// <summary>
/// Applies text overlays (stamps / watermarks) to PDF pages.
/// </summary>
public interface IStampApplier
{
    /// <summary>
    /// Applies <paramref name="stamp"/> to every page of <paramref name="document"/>.
    /// The document is mutated in-place.
    /// </summary>
    Task StampAsync(
        IPdfDocument document,
        TextStamp stamp,
        CancellationToken ct = default
    );

    /// <summary>
    /// Applies <paramref name="stamp"/> to a single page of <paramref name="document"/>.
    /// The document is mutated in-place.
    /// </summary>
    /// <param name="pageNumber">1-based page number.</param>
    Task StampPageAsync(
        IPdfDocument document,
        int pageNumber,
        TextStamp stamp,
        CancellationToken ct = default
    );
}
