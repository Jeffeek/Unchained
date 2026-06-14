namespace Unchained.Pdf.Models;

/// <summary>
///     A decoded raster image extracted from a PDF <c>/XObject /Subtype /Image</c> stream.
///     Pixel data is in row-major order, top-to-bottom, 3 bytes per pixel (R, G, B).
/// </summary>
/// <param name="Width">Image width in pixels.</param>
/// <param name="Height">Image height in pixels.</param>
/// <param name="RgbData">
///     Decoded RGB pixel data: <c>Width × Height × 3</c> bytes in R,G,B order. Only <c>/DeviceRGB</c>
///     with 8 bits per component is decoded; other colour spaces produce a solid mid-grey placeholder.
/// </param>
/// <param name="Alpha">
///     Optional per-pixel alpha (<c>Width × Height</c> bytes, 0 = transparent, 255 = opaque)
///     derived from the image's <c>/SMask</c> soft mask (ISO 32000-1 §11.6.5.2), or
///     <see langword="null" /> when the image has no soft mask. Renderers must composite the
///     image over the background using this alpha; ignoring it makes masked-out regions (e.g.
///     the transparent background of an anti-aliased formula image) render as opaque colour.
/// </param>
public sealed record ImageXObject(
    int Width,
    int Height,
    byte[] RgbData,
    byte[]? Alpha = null
);
