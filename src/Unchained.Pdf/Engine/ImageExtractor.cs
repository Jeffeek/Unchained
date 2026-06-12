using Unchained.Pdf.Abstractions;
using Unchained.Pdf.Models;

namespace Unchained.Pdf.Engine;

/// <summary>
///     Default <see cref="IImageExtractor" /> implementation. Reuses the page adapter's image
///     decoding (<see cref="IPdfPage.GetImageXObjects" />), which already handles RGB/Gray/CMYK,
///     Indexed palettes, the standard image filters, and <c>/SMask</c> soft masks.
/// </summary>
public sealed class ImageExtractor : IImageExtractor
{
    /// <inheritdoc />
    public Task<IReadOnlyList<ExtractedImage>> ExtractImagesAsync(
        IPdfDocument document,
        CancellationToken ct = default
    ) => Task.Run(() =>
        {
            var result = new List<ExtractedImage>();
            for (var p = 1; p <= document.PageCount; p++)
            {
                ct.ThrowIfCancellationRequested();
                ExtractPage(document, p, result);
            }

            return (IReadOnlyList<ExtractedImage>)result;
        },
        ct);

    /// <inheritdoc />
    public Task<IReadOnlyList<ExtractedImage>> ExtractImagesAsync(
        IPdfDocument document,
        int pageNumber,
        CancellationToken ct = default
    ) => Task.Run(() =>
        {
            if (pageNumber < 1 || pageNumber > document.PageCount)
            {
                throw new ArgumentOutOfRangeException(nameof(pageNumber),
                    pageNumber,
                    $"Page number must be between 1 and {document.PageCount}.");
            }

            var result = new List<ExtractedImage>();
            ExtractPage(document, pageNumber, result);
            return (IReadOnlyList<ExtractedImage>)result;
        },
        ct);

    private static void ExtractPage(IPdfDocument document, int pageNumber, List<ExtractedImage> sink)
    {
        var page = document.Pages[pageNumber];
        // Sort by resource name for deterministic ordering.
        foreach (var (name, img) in page.GetImageXObjects().OrderBy(static kv => kv.Key, StringComparer.Ordinal))
        {
            if (img.Width <= 0 || img.Height <= 0 || img.RgbData.Length < img.Width * img.Height * 3)
                continue;
            sink.Add(new ExtractedImage(pageNumber,
                name,
                img.Width,
                img.Height,
                img.RgbData,
                img.Alpha));
        }
    }
}
