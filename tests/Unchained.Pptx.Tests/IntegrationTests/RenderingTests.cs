using Shouldly;
using Unchained.Drawing.Constants;
using Unchained.Ooxml;
using Unchained.Pptx.Models.Shapes;
using Unchained.Pptx.Rendering;
using Unchained.Pptx.Rendering.Engine;
using Unchained.Pptx.Tests.Helpers;
using Xunit;

namespace Unchained.Pptx.Tests.IntegrationTests;

public sealed class RenderingTests : PptxTestBase
{
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

    // ── Rendering (requires FreeType2 native DLLs via fetch-natives) ──
    // If FreeType2 is not present, these tests throw DllNotFoundException and fail —
    // the same behaviour as Unchained.Pdf.Tests rendering tests.
    // Run scripts/FetchNatives/fetch-natives.{ps1,sh} before executing.

    [Fact]
    public async Task RenderSlide_BlankSlide_ProducesNonEmptyData()
    {
        var doc = PptxFixtures.WithSlides(1);
        var image = await SlideRenderer.RenderAsync(doc.Slides[0], doc.SlideSize,
            new RenderOptions { WidthPx = 320, HeightPx = 180 });

        image.Data.IsEmpty.ShouldBeFalse();
        image.Data.Length.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task RenderSlide_OutputIsPng()
    {
        var doc = PptxFixtures.WithSlides(1);
        var image = await SlideRenderer.RenderAsync(doc.Slides[0], doc.SlideSize,
            new RenderOptions { WidthPx = 320, HeightPx = 180 });

        // PNG magic bytes: 89 50 4E 47
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
        var image = await SlideRenderer.RenderAsync(doc.Slides[0], doc.SlideSize,
            new RenderOptions { WidthPx = 640, HeightPx = 360 });

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

        var image = await SlideRenderer.RenderAsync(doc.Slides[0], doc.SlideSize,
            new RenderOptions { WidthPx = 320, HeightPx = 180 });

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

        var image = await SlideRenderer.RenderAsync(doc.Slides[0], doc.SlideSize,
            new RenderOptions { WidthPx = 320, HeightPx = 180 });

        image.Data.Length.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task RenderAllAsync_FiveSlides_ReturnsFiveImages()
    {
        var doc = PptxFixtures.WithSlides(5);
        var images = await SlideRenderer.RenderAllAsync(doc,
            new RenderOptions { WidthPx = 320, HeightPx = 180 });

        images.Length.ShouldBe(5);
        foreach (var img in images)
            img.Data.IsEmpty.ShouldBeFalse();
    }

    [Fact]
    public async Task RenderAllAsync_AllImagesArePng()
    {
        var doc = PptxFixtures.WithSlides(3);
        var images = await SlideRenderer.RenderAllAsync(doc,
            new RenderOptions { WidthPx = 160, HeightPx = 90 });

        foreach (var img in images)
        {
            img.Format.ShouldBe(RenderImageFormat.Png);
            img.Data.Span[0].ShouldBe((byte)0x89); // PNG magic
        }
    }

    [Fact]
    public async Task RenderAllAsync_EmptyPresentation_ReturnsEmpty()
    {
        var doc = new Engine.PresentationProcessor().CreateBlank();
        var images = await SlideRenderer.RenderAllAsync(doc,
            new RenderOptions { WidthPx = 320, HeightPx = 180 });

        images.Length.ShouldBe(0);
    }

    [Fact]
    public async Task RenderSlide_BmpFormat_ProducesBmpBytesAndLabel()
    {
        var doc = PptxFixtures.WithSlides(1);
        var image = await SlideRenderer.RenderAsync(doc.Slides[0], doc.SlideSize,
            new RenderOptions { WidthPx = 64, HeightPx = 48, Format = RenderImageFormat.Bmp });

        image.Format.ShouldBe(RenderImageFormat.Bmp);
        var bytes = image.Data.Span;
        bytes[0].ShouldBe((byte)0x42); // 'B'
        bytes[1].ShouldBe((byte)0x4D); // 'M'
    }

    [Fact]
    public async Task RenderSlide_JpegFormat_ProducesJpegBytesAndLabel()
    {
        var doc = PptxFixtures.WithSlides(1);
        var image = await SlideRenderer.RenderAsync(doc.Slides[0], doc.SlideSize,
            new RenderOptions { WidthPx = 64, HeightPx = 48, Format = RenderImageFormat.Jpeg });

        image.Format.ShouldBe(RenderImageFormat.Jpeg);
        var bytes = image.Data.Span;
        bytes[0].ShouldBe(JpegMarkers.MarkerPrefix); // JPEG SOI marker
        bytes[1].ShouldBe(JpegMarkers.Soi);
    }
}
