using System.IO.Compression;
using System.Text;
using Shouldly;
using Unchained.Drawing;
using Unchained.Drawing.Constants;
using Unchained.Drawing.Encoders;
using Unchained.Ooxml;
using Unchained.Pptx.Rendering.Engine;
using Unchained.Pptx.Rendering.Models;
using Unchained.Pptx.Tests.Helpers;
using Xunit;

namespace Unchained.Pptx.Tests.IntegrationTests;

/// <summary>
///     Verifies that glyph bitmaps actually blit to the raster (the Windows-x64 SharpFont
///     offset bug previously left text invisible). Decodes the PNG and counts dark pixels.
/// </summary>
public sealed class GlyphRenderTests : PptxTestBase
{
    [Fact]
    public async Task TextBox_ProducesDarkTextPixels()
    {
        var doc = PptxFixtures.WithSlides(1);
        doc.Slides[0].Shapes.AddTextBox(
            Emu.FromInches(0.5),
            Emu.FromInches(0.5),
            Emu.FromInches(8),
            Emu.FromInches(2),
            "Rendering Works Now");

        var image = await SlideRenderer.RenderAsync(
            doc.Slides[0],
            doc.SlideSize,
            new RenderOptions { WidthPx = 640, HeightPx = 360 });

        var darkPixels = CountDarkPixels(image.Data.ToArray(), 640, 360);

        // Glyph strokes are near-black on a white slide. Before the fix this was ~0.
        darkPixels.ShouldBeGreaterThan(50);
    }

    [Fact]
    public async Task BlankSlide_HasNoDarkPixels()
    {
        var doc = PptxFixtures.WithSlides(1);

        var image = await SlideRenderer.RenderAsync(
            doc.Slides[0],
            doc.SlideSize,
            new RenderOptions { WidthPx = 640, HeightPx = 360 });

        CountDarkPixels(image.Data.ToArray(), 640, 360).ShouldBe(0);
    }

    [Fact]
    public async Task EmbeddedPngPicture_RendersImagePixels()
    {
        // Build a solid magenta 8×8 PNG, embed it as a picture filling part of the slide.
        var src = new RasterBuffer(8, 8);
        src.Clear(255, 0); // magenta — not a default text/background colour

        var pngBytes = PngEncoder.Encode(src);

        var doc = PptxFixtures.WithSlides(1);
        var image = doc.Media.AddImage(pngBytes, "image/png");
        doc.Slides[0].Shapes.AddPicture(
            image,
            Emu.FromInches(1),
            Emu.FromInches(1),
            Emu.FromInches(4),
            Emu.FromInches(3));

        var rendered = await SlideRenderer.RenderAsync(
            doc.Slides[0],
            doc.SlideSize,
            new RenderOptions { WidthPx = 640, HeightPx = 360 });

        var magenta = CountMagentaPixels(rendered.Data.ToArray(), 640, 360);
        magenta.ShouldBeGreaterThan(100);
    }

    // Decodes a filter-None RGBA PNG produced by PngEncoder and counts near-black pixels.
    private static int CountDarkPixels(byte[] png, int width, int height) =>
        PngTestUtils.CountPixels(png, width, height, static (r, g, b) => r < 80 && g < 80 && b < 80);

    // Magenta: high R, low G, high B.
    private static int CountMagentaPixels(byte[] png, int width, int height) =>
        PngTestUtils.CountPixels(png, width, height, static (r, g, b) => r > 200 && g < 80 && b > 200);
}
