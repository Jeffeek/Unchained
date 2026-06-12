using Unchained.Pdf.Models;

namespace Unchained.Pdf.Abstractions;

/// <summary>
///     Extracts embedded raster images from a PDF document. Each image XObject referenced by a
///     page is decoded to RGB(A); use <see cref="ExtractedImage.ToPng" /> to obtain portable PNG
///     bytes. The same image referenced from multiple pages is returned once per referencing page.
/// </summary>
public interface IImageExtractor
{
    /// <summary>
    ///     Extracts every decodable image from every page, in page order.
    /// </summary>
    /// <param name="document">Document to read (not modified).</param>
    /// <param name="ct">Cancellation token.</param>
    Task<IReadOnlyList<ExtractedImage>> ExtractImagesAsync(
        IPdfDocument document,
        CancellationToken ct = default
    );

    /// <summary>
    ///     Extracts every decodable image from a single 1-based page.
    /// </summary>
    /// <param name="document">Document to read (not modified).</param>
    /// <param name="pageNumber">1-based page number.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<IReadOnlyList<ExtractedImage>> ExtractImagesAsync(
        IPdfDocument document,
        int pageNumber,
        CancellationToken ct = default
    );
}
