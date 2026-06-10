namespace Unchained.Pdf.Models;

/// <summary>
/// A decoded raster image extracted from a PDF, together with where it was found.
/// Pixel data is row-major, top-to-bottom, 3 bytes per pixel (R, G, B). Obtain a portable
/// PNG via <see cref="ToPng"/>.
/// </summary>
/// <param name="PageNumber">1-based page the image appears on.</param>
/// <param name="ResourceName">The image's XObject resource name on that page (e.g. <c>Im0</c>).</param>
/// <param name="Width">Image width in pixels.</param>
/// <param name="Height">Image height in pixels.</param>
/// <param name="RgbData">Decoded RGB pixels: <c>Width × Height × 3</c> bytes, R,G,B order.</param>
/// <param name="Alpha">
/// Optional per-pixel alpha (<c>Width × Height</c> bytes, 0 = transparent, 255 = opaque)
/// from the image's <c>/SMask</c>, or <see langword="null"/> when the image is opaque.
/// </param>
public sealed record ExtractedImage(
    int PageNumber,
    string ResourceName,
    int Width,
    int Height,
    byte[] RgbData,
    byte[]? Alpha = null
)
{
    /// <summary>
    /// Encodes the image as PNG bytes (RGB, or RGBA when <see cref="Alpha"/> is present).
    /// </summary>
    public byte[] ToPng() => PngWriter.Encode(Width, Height, RgbData, Alpha);
}
