using Unchained.Pdf.Models;

namespace Unchained.Pdf.Abstractions;

/// <summary>
/// Applies text overlays (stamps / watermarks) to PDF pages.
/// </summary>
// ReSharper disable once MemberCanBeInternal
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
    /// <param name="document"></param>
    /// <param name="pageNumber">1-based page number.</param>
    /// <param name="stamp"></param>
    /// <param name="ct"></param>
    Task StampPageAsync(
        IPdfDocument document,
        int pageNumber,
        TextStamp stamp,
        CancellationToken ct = default
    );
}
