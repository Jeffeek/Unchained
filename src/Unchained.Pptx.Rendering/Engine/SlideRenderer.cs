using Unchained.Pptx.Core;
using Unchained.Pptx.Engine;
using Unchained.Pptx.Media;
using Unchained.Ooxml.Media;
using Unchained.Pptx.Slides;
using Unchained.Drawing;
using Unchained.Drawing.Text;
using Unchained.Drawing.Encoders;

namespace Unchained.Pptx.Rendering.Engine;

/// <summary>
/// Rasterizes PPTX slides to <see cref="PptxImage"/> instances using
/// FreeType2 (via SharpFont) and HarfBuzz for text shaping.
/// </summary>
// ReSharper disable once MemberCanBeInternal
public static class SlideRenderer
{
    private static readonly SemaphoreSlim Gate =
        new(Environment.ProcessorCount, Environment.ProcessorCount);

    /// <summary>
    /// Rasterizes a single slide and returns the encoded image.
    /// </summary>
    /// <param name="slide">The slide to render.</param>
    /// <param name="slideSize">The logical dimensions of the slide in EMUs.</param>
    /// <param name="options">Render options; defaults are used when <see langword="null"/>.</param>
    /// <param name="media">
    /// The presentation's media store, used to resolve embedded fonts so custom typefaces
    /// render in their real shape. Pass <c>document.Media</c>; <see langword="null"/> falls
    /// back to bundled substitute fonts.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    public static Task<PptxImage> RenderAsync(
        Slide slide,
        SlideSize slideSize,
        RenderOptions? options = null,
        MediaStore? media = null,
        CancellationToken ct = default
    ) => RenderWithGateAsync(slide, slideSize, options ?? new RenderOptions(), media, ct);

    /// <summary>
    /// Rasterizes all slides in the presentation and returns an image per slide.
    /// Slides are rendered concurrently up to <see cref="Environment.ProcessorCount"/>.
    /// </summary>
    /// <param name="document">The presentation to render.</param>
    /// <param name="options">Render options; defaults are used when <see langword="null"/>.</param>
    /// <param name="ct">Cancellation token.</param>
    public static async Task<PptxImage[]> RenderAllAsync(
        PresentationDocument document,
        RenderOptions? options = null,
        CancellationToken ct = default
    )
    {
        var opts = options ?? new RenderOptions();
        var slideSize = document.SlideSize;
        var slides = document.Slides;
        var media = document.Media;

        var tasks = new Task<PptxImage>[slides.Count];
        for (var i = 0; i < slides.Count; i++)
        {
            var slide = slides[i];
            tasks[i] = RenderWithGateAsync(slide, slideSize, opts, media, ct);
        }

        return await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    private static async Task<PptxImage> RenderWithGateAsync(
        Slide slide,
        SlideSize slideSize,
        RenderOptions options,
        MediaStore? media,
        CancellationToken ct)
    {
        await Gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            return await Task.Run(() => Render(slide, slideSize, options, media), ct).ConfigureAwait(false);
        }
        finally
        {
            Gate.Release();
        }
    }

    private static PptxImage Render(Slide slide, SlideSize slideSize, RenderOptions options, MediaStore? media)
    {
        using var fontCache = new FontCache();
        var rasterizer = new SlideRasterizer(fontCache, media);
        var buffer = rasterizer.Rasterize(slide, slideSize, options);
        var encoded = Encode(buffer, options);

        return new PptxImage(
            options.WidthPx,
            options.HeightPx,
            options.Format,
            encoded
        );
    }

    private static byte[] Encode(RasterBuffer buffer, RenderOptions options) => options.Format switch
    {
        RenderImageFormat.Png  => PngEncoder.Encode(buffer),
        RenderImageFormat.Bmp  => BmpEncoder.Encode(buffer),
        RenderImageFormat.Jpeg => JpegEncoder.Encode(buffer, options.JpegQuality),
        _ => throw new NotSupportedException($"Unsupported render image format: {options.Format}.")
    };
}
