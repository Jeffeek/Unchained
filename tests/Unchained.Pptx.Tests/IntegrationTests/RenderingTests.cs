using Shouldly;
using Unchained.Ooxml;
using Unchained.Pptx.Models.Shapes;
using Unchained.Pptx.Rendering;
using Unchained.Pptx.Rendering.Engine;
using Unchained.Pptx.Tests.Helpers;
using Xunit;

namespace Unchained.Pptx.Tests.IntegrationTests;

public sealed class RenderingTests : PptxTestBase
{
    private static readonly ReadOnlyMemory<byte> PngMagic =
        new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    // ── Model types ───────────────────────────────────────────────────────────

    [Fact]
    public void RenderOptions_DefaultDimensions_Are1920x1080()
    {
        var opts = new RenderOptions();
        opts.WidthPx.ShouldBe(1920);
        opts.HeightPx.ShouldBe(1080);
    }

    [Fact]
    public void RenderOptions_DefaultFormat_IsPng()
    {
        var opts = new RenderOptions();
        opts.Format.ShouldBe(RenderImageFormat.Png);
    }

    [Fact]
    public void PptxImage_PropertiesAccessible()
    {
        var data = new byte[100];
        var image = new PptxImage(800, 600, RenderImageFormat.Png, data);
        image.WidthPx.ShouldBe(800);
        image.HeightPx.ShouldBe(600);
        image.Format.ShouldBe(RenderImageFormat.Png);
        image.Data.Length.ShouldBe(100);
    }

    // ── Rendering (requires FreeType2 native DLLs via fetch-natives script) ──
    // When FreeType2 is not present, the render calls throw DllNotFoundException
    // and the tests return early (vacuous pass). They run fully on CI.

    private static async Task<PptxImage?> TryRenderAsync(
        Slides.Slide slide, Core.SlideSize slideSize, RenderOptions options)
    {
        try
        {
            return await SlideRenderer.RenderAsync(slide, slideSize, options);
        }
        catch (DllNotFoundException) { return null; }
        catch (Exception ex) when (ex.GetType().Name.Contains("FreeType")) { return null; }
    }

    private static async Task<PptxImage[]?> TryRenderAllAsync(
        Engine.PresentationDocument document, RenderOptions options)
    {
        try
        {
            return await SlideRenderer.RenderAllAsync(document, options);
        }
        catch (DllNotFoundException) { return null; }
        catch (Exception ex) when (ex.GetType().Name.Contains("FreeType")) { return null; }
    }

    [Fact]
    public async Task RenderSlide_BlankSlide_ProducesNonEmptyData()
    {
        var doc = PptxFixtures.WithSlides(1);
        var image = await TryRenderAsync(doc.Slides[0], doc.SlideSize,
            new RenderOptions { WidthPx = 320, HeightPx = 180 });
        if (image == null) return; // natives not available

        image.Data.IsEmpty.ShouldBeFalse();
        image.Data.Length.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task RenderSlide_OutputIsPng()
    {
        var doc = PptxFixtures.WithSlides(1);
        var image = await TryRenderAsync(doc.Slides[0], doc.SlideSize,
            new RenderOptions { WidthPx = 320, HeightPx = 180 });
        if (image == null) return;

        var bytes = image.Data.Span;
        bytes[0].ShouldBe((byte)0x89);
        bytes[1].ShouldBe((byte)0x50); // 'P'
        bytes[2].ShouldBe((byte)0x4E); // 'N'
        bytes[3].ShouldBe((byte)0x47); // 'G'
    }

    [Fact]
    public async Task RenderSlide_DimensionsMatchOptions()
    {
        var doc = PptxFixtures.WithSlides(1);
        var image = await TryRenderAsync(doc.Slides[0], doc.SlideSize,
            new RenderOptions { WidthPx = 640, HeightPx = 360 });
        if (image == null) return;

        image.WidthPx.ShouldBe(640);
        image.HeightPx.ShouldBe(360);
    }

    [Fact]
    public async Task RenderSlide_WithTextShape_ProducesOutput()
    {
        var doc = PptxFixtures.WithSlides(1);
        doc.Slides[0].Shapes.AddTextBox(
            Emu.FromInches(1), Emu.FromInches(1),
            Emu.FromInches(4), Emu.FromInches(2),
            "Hello Renderer");

        var image = await TryRenderAsync(doc.Slides[0], doc.SlideSize,
            new RenderOptions { WidthPx = 320, HeightPx = 180 });
        if (image == null) return;

        image.Data.Length.ShouldBeGreaterThan(100);
    }

    [Fact]
    public async Task RenderSlide_WithColoredShape_ProducesOutput()
    {
        var doc = PptxFixtures.WithSlides(1);
        var shape = doc.Slides[0].Shapes.AddShape(AutoShapeType.Rectangle,
            Emu.FromInches(0.5), Emu.FromInches(0.5),
            Emu.FromInches(3), Emu.FromInches(2));
        shape.Fill.SetSolid(Unchained.Ooxml.Drawing.ColorSpec.FromRgb(0, 112, 192));

        var image = await TryRenderAsync(doc.Slides[0], doc.SlideSize,
            new RenderOptions { WidthPx = 320, HeightPx = 180 });
        if (image == null) return;

        image.Data.Length.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task RenderAllAsync_FiveSlides_ReturnsFiveImages()
    {
        var doc = PptxFixtures.WithSlides(5);
        var images = await TryRenderAllAsync(doc,
            new RenderOptions { WidthPx = 320, HeightPx = 180 });
        if (images == null) return;

        images.Length.ShouldBe(5);
        foreach (var img in images)
            img.Data.IsEmpty.ShouldBeFalse();
    }

    [Fact]
    public async Task RenderAllAsync_AllImagesArePng()
    {
        var doc = PptxFixtures.WithSlides(3);
        var images = await TryRenderAllAsync(doc,
            new RenderOptions { WidthPx = 160, HeightPx = 90 });
        if (images == null) return;

        foreach (var img in images)
        {
            img.Format.ShouldBe(RenderImageFormat.Png);
            img.Data.Span[0].ShouldBe((byte)0x89);
        }
    }

    [Fact]
    public async Task RenderAllAsync_EmptyPresentation_ReturnsEmpty()
    {
        var doc = new Engine.PresentationProcessor().CreateBlank();
        var images = await TryRenderAllAsync(doc,
            new RenderOptions { WidthPx = 320, HeightPx = 180 });
        if (images == null) return;

        images.Length.ShouldBe(0);
    }
}
