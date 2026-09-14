namespace Unchained.Pdf.Models;

/// <summary>
///     Raw raster image to place onto a page via
///     <see cref="Abstractions.IPageContentEditor.DrawImageAsync" />. Pixels are 8-bit-per-component
///     <c>DeviceRGB</c>, row-major, top row first, three bytes (R, G, B) per pixel.
/// </summary>
/// <param name="Width">Image width in pixels.</param>
/// <param name="Height">Image height in pixels.</param>
/// <param name="RgbData">
///     Packed RGB samples; length must be exactly <c>Width * Height * 3</c>.
/// </param>
/// <param name="Alpha">
///     Optional 8-bit alpha channel, one byte per pixel (length <c>Width * Height</c>), embedded as
///     a soft mask. <see langword="null" /> for a fully opaque image.
/// </param>
public sealed record ImageContent(
    int Width,
    int Height,
    byte[] RgbData,
    byte[]? Alpha = null
);
