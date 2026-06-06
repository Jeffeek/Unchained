namespace Unchained.Pptx.Rendering;

/// <summary>
/// Controls the output resolution, dimensions, and encoding format when rasterizing a slide.
/// </summary>
/// <param name="WidthPx">Target image width in pixels. Default is 1920.</param>
/// <param name="HeightPx">Target image height in pixels. Default is 1080.</param>
/// <param name="Dpi">Logical DPI used for font sizing calculations. Default is 96.0.</param>
/// <param name="Format">The output image format. Default is <see cref="RenderImageFormat.Png"/>.</param>
/// <param name="JpegQuality">
/// JPEG encoding quality from 0 (worst) to 100 (best). Only used when
/// <see cref="Format"/> is <see cref="RenderImageFormat.Jpeg"/>. Default is 90.
/// </param>
public sealed record RenderOptions(
    int WidthPx = 1920,
    int HeightPx = 1080,
    double Dpi = 96.0,
    RenderImageFormat Format = RenderImageFormat.Png,
    int JpegQuality = 90
);
