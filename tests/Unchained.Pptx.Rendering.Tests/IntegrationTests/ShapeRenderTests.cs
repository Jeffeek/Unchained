using Shouldly;
using Unchained.Ooxml;
using Unchained.Ooxml.Drawing;
using Unchained.Pptx.Models.Shapes;
using Unchained.Pptx.Rendering.Engine;
using Unchained.Pptx.Rendering.Models;
using Unchained.Pptx.Rendering.Tests.Helpers;
using Unchained.Pptx.Shapes;
using Unchained.Pptx.Tests.Shared;
using Xunit;

namespace Unchained.Pptx.Rendering.Tests.IntegrationTests;

// Verifies the rasterizer now draws groups and tables (previously only AutoShape + Picture).
public sealed class ShapeRenderTests : PptxTestBase
{
    private static readonly RenderOptions Small = new() { WidthPx = 640, HeightPx = 360 };

    [Fact]
    public async Task Table_RendersCellFillsAndGridLines()
    {
        var doc = PptxFixtures.WithSlides(1);
        var table = doc.Slides[0]
            .Shapes.AddTable(
                Emu.FromInches(1),
                Emu.FromInches(1),
                [Emu.FromInches(2), Emu.FromInches(2)],
                [Emu.FromInches(1), Emu.FromInches(1)]
            );
        table[0, 0].Fill.SetSolid(ColorSpec.FromRgb(0, 112, 192));

        var image = await SlideRenderer.RenderAsync(
            doc.Slides[0],
            doc.SlideSize,
            Small
        );

        // At least 1% of the canvas should be non-white (the blue cell fill).
        var colored = CountColoredPixels(image.Data.ToArray(), 640, 360);
        colored.ShouldBeGreaterThan(640 * 360 / 100);
    }

    [Fact]
    public async Task Group_RendersChildShapes()
    {
        var doc = PptxFixtures.WithSlides(1);
        var group = doc.Slides[0].Shapes.AddGroup();
        var child = new AutoShape
        {
            ShapeType = AutoShapeType.Rectangle,
            X = Emu.FromInches(1),
            Y = Emu.FromInches(1),
            Width = Emu.FromInches(3),
            Height = Emu.FromInches(2)
        };
        child.Fill.SetSolid(ColorSpec.FromRgb(0, 200, 0));
        group.Children.AddParsed(child);

        var image = await SlideRenderer.RenderAsync(
            doc.Slides[0],
            doc.SlideSize,
            Small
        );

        var colored = CountColoredPixels(image.Data.ToArray(), 640, 360);
        colored.ShouldBeGreaterThan(640 * 360 / 100);
    }

    // Counts non-white (and non-near-white) pixels.
    private static int CountColoredPixels(byte[] png, int width, int height) =>
        PngTestUtils.CountPixels(png, width, height, static (r, g, b) => r < 240 || g < 240 || b < 240);
}
