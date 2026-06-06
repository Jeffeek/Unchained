using Unchained.Pptx.Core;
using Unchained.Pptx.Engine;
using Unchained.Pptx.Slides;

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
    /// <param name="ct">Cancellation token.</param>
    public static Task<PptxImage> RenderAsync(
        Slide slide,
        SlideSize slideSize,
        RenderOptions? options = null,
        CancellationToken ct = default
    ) => RenderWithGateAsync(slide, slideSize, options ?? new RenderOptions(), ct);

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

        var tasks = new Task<PptxImage>[slides.Count];
        for (var i = 0; i < slides.Count; i++)
        {
            var slide = slides[i];
            tasks[i] = RenderWithGateAsync(slide, slideSize, opts, ct);
        }

        return await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    private static async Task<PptxImage> RenderWithGateAsync(
        Slide slide,
        SlideSize slideSize,
        RenderOptions options,
        CancellationToken ct)
    {
        await Gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            return await Task.Run(() => Render(slide, slideSize, options), ct).ConfigureAwait(false);
        }
        finally
        {
            Gate.Release();
        }
    }

    private static PptxImage Render(Slide slide, SlideSize slideSize, RenderOptions options)
    {
        using var fontCache = new FontCache();
        var rasterizer = new SlideRasterizer(fontCache);
        var buffer = rasterizer.Rasterize(slide, slideSize, options);
        var encoded = PptxPngEncoder.Encode(buffer);

        return new PptxImage(
            options.WidthPx,
            options.HeightPx,
            options.Format,
            encoded
        );
    }
}
