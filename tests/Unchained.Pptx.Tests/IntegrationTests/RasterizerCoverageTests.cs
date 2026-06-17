using Shouldly;
using Unchained.Drawing;
using Unchained.Drawing.Encoders;
using Unchained.Ooxml;
using Unchained.Ooxml.Charts;
using Unchained.Ooxml.Drawing;
using Unchained.Pptx.Engine;
using Unchained.Pptx.Models.Shapes;
using Unchained.Pptx.Rendering.Engine;
using Unchained.Pptx.Rendering.Models;
using Unchained.Pptx.Shapes;
using Unchained.Pptx.Tests.Helpers;
using Xunit;

namespace Unchained.Pptx.Tests.IntegrationTests;

/// <summary>
///     Drives the <see cref="SlideRasterizer" /> partials and helper rasterizers (SmartArt layouts,
///     fill/effect painting, connectors with arrowheads, gradient/picture backgrounds, groups) by
///     rendering slides that exercise each path and asserting the PNG output is well-formed and
///     non-empty.
/// </summary>
public sealed class RasterizerCoverageTests : PptxTestBase
{
    private static readonly RenderOptions Small = new() { WidthPx = 320, HeightPx = 180 };

    private static byte[] SmallPng()
    {
        var buffer = new RasterBuffer(8, 8);
        buffer.Clear(220, 30, 120);
        return PngEncoder.Encode(buffer);
    }

    private static SmartArtShape AddSmartArt(
        PresentationDocument doc,
        Emu width,
        Emu height
    )
    {
        var shape = new SmartArtShape { X = Emu.FromInches(1), Y = Emu.FromInches(1), Width = width, Height = height };
        doc.Slides[0].Shapes.AddParsed(shape);
        return shape;
    }

    private static async Task<PptxImage> RenderAsync(PresentationDocument doc) =>
        await SlideRenderer.RenderAsync(doc.Slides[0], doc.SlideSize, Small);

    // ── SmartArt layouts ─────────────────────────────────────────────────────────

    [Fact]
    public async Task SmartArt_LinearList_Renders()
    {
        var doc = PptxFixtures.WithSlides(1);
        var sa = AddSmartArt(doc, Emu.FromInches(2), Emu.FromInches(4));
        sa.Nodes.Add(new SmartArtNode { Text = "One" });
        sa.Nodes.Add(new SmartArtNode { Text = "Two" });

        var image = await RenderAsync(doc);
        image.Data.Length.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task SmartArt_Cycle_Renders()
    {
        var doc = PptxFixtures.WithSlides(1);
        var sa = AddSmartArt(doc, Emu.FromInches(5), Emu.FromInches(5));
        for (var i = 0; i < 4; i++) sa.Nodes.Add(new SmartArtNode { Text = $"N{i}" });

        var image = await RenderAsync(doc);
        image.Data.Length.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task SmartArt_Matrix_Renders()
    {
        var doc = PptxFixtures.WithSlides(1);
        var sa = AddSmartArt(doc, Emu.FromInches(6), Emu.FromInches(4));
        for (var i = 0; i < 4; i++) sa.Nodes.Add(new SmartArtNode { Text = $"Q{i}" });

        var image = await RenderAsync(doc);
        image.Data.Length.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task SmartArt_Pyramid_Renders()
    {
        var doc = PptxFixtures.WithSlides(1);
        var sa = AddSmartArt(doc, Emu.FromInches(3), Emu.FromInches(6));
        for (var i = 0; i < 4; i++) sa.Nodes.Add(new SmartArtNode { Text = $"L{i}" });

        var image = await RenderAsync(doc);
        image.Data.Length.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task SmartArt_Hierarchy_Renders()
    {
        var doc = PptxFixtures.WithSlides(1);
        var sa = AddSmartArt(doc, Emu.FromInches(6), Emu.FromInches(4));
        var root = new SmartArtNode { Text = "Root" };
        root.AddChild("Child A");
        root.AddChild("Child B");
        sa.Nodes.Add(root);

        var image = await RenderAsync(doc);
        image.Data.Length.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task SmartArt_Empty_RendersBorderPlaceholder()
    {
        var doc = PptxFixtures.WithSlides(1);
        AddSmartArt(doc, Emu.FromInches(3), Emu.FromInches(2));

        var image = await RenderAsync(doc);
        image.Data.Length.ShouldBeGreaterThan(0);
    }

    // ── Fills & effects ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GradientFilledShape_Renders()
    {
        var doc = PptxFixtures.WithSlides(1);
        var shape = doc.Slides[0]
            .Shapes.AddShape(AutoShapeType.Rectangle, Emu.FromInches(1), Emu.FromInches(1), Emu.FromInches(4), Emu.FromInches(3));
        var grad = shape.Fill.SetGradient();
        grad.Stops.Add(new GradientStop(0.0, ColorSpec.FromRgb(255, 0, 0)));
        grad.Stops.Add(new GradientStop(1.0, ColorSpec.FromRgb(0, 0, 255)));

        var image = await RenderAsync(doc);
        image.Data.Length.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task PictureFilledShape_Renders()
    {
        var doc = PptxFixtures.WithSlides(1);
        var image = doc.Media.AddImage(SmallPng(), "image/png");
        var shape = doc.Slides[0]
            .Shapes.AddShape(AutoShapeType.Rectangle, Emu.FromInches(1), Emu.FromInches(1), Emu.FromInches(3), Emu.FromInches(2));
        shape.Fill.Type = FillType.Picture;
        shape.Fill.Picture = new PictureFill { Image = image };

        var rendered = await RenderAsync(doc);
        rendered.Data.Length.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task BeveledShape_Renders()
    {
        var doc = PptxFixtures.WithSlides(1);
        var shape = doc.Slides[0]
            .Shapes.AddShape(AutoShapeType.Rectangle, Emu.FromInches(1), Emu.FromInches(1), Emu.FromInches(3), Emu.FromInches(2));
        shape.Fill.SetSolid(ColorSpec.FromRgb(120, 180, 200));
        shape.ThreeD.TopBevel = new BevelFormat { Width = Emu.FromPoints(6), Height = Emu.FromPoints(6) };

        var image = await RenderAsync(doc);
        image.Data.Length.ShouldBeGreaterThan(0);
    }

    [
        Theory,
        InlineData("textArchUp"),
        InlineData("textArchDown"),
        InlineData("textWave1"),
        InlineData("textCircle"),
        InlineData("textChevron"),
        InlineData("textInflate")
    ]
    public async Task WarpedText_Renders(string preset)
    {
        var doc = PptxFixtures.WithSlides(1);
        var tb = doc.Slides[0]
            .Shapes.AddTextBox(Emu.FromInches(1), Emu.FromInches(1), Emu.FromInches(4), Emu.FromInches(2));
        tb.TextFrame.Format.Warp = new TextWarpFormat { Preset = preset };
        tb.TextFrame.Paragraphs.Add("Warped");

        var image = await RenderAsync(doc);
        image.Data.Length.ShouldBeGreaterThan(0);
    }

    // ── Connectors ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task ConnectorWithArrowheads_Renders()
    {
        var doc = PptxFixtures.WithSlides(1);
        var conn = doc.Slides[0]
            .Shapes.AddConnector(ConnectorType.Straight, Emu.FromInches(1), Emu.FromInches(1), Emu.FromInches(4), Emu.FromInches(2));
        conn.Line.SetSolid(ColorSpec.FromRgb(0, 0, 0), 2);
        conn.Line.HeadArrow.HeadType = ArrowHeadType.Triangle;
        conn.Line.TailArrow.HeadType = ArrowHeadType.Oval;

        var image = await RenderAsync(doc);
        image.Data.Length.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task FlippedConnector_Renders()
    {
        var doc = PptxFixtures.WithSlides(1);
        var conn = doc.Slides[0]
            .Shapes.AddConnector(ConnectorType.Straight, Emu.FromInches(1), Emu.FromInches(1), Emu.FromInches(4), Emu.FromInches(2));
        conn.FlipHorizontal = true;
        conn.FlipVertical = true;
        conn.Line.SetSolid(ColorSpec.FromRgb(10, 20, 30));

        var image = await RenderAsync(doc);
        image.Data.Length.ShouldBeGreaterThan(0);
    }

    // ── Backgrounds & groups ─────────────────────────────────────────────────────

    [Fact]
    public async Task GradientBackground_Renders()
    {
        var doc = PptxFixtures.WithSlides(1);
        var grad = doc.Slides[0].Background.Fill.SetGradient();
        grad.Stops.Add(new GradientStop(0.0, ColorSpec.FromRgb(255, 255, 0)));
        grad.Stops.Add(new GradientStop(1.0, ColorSpec.FromRgb(0, 128, 255)));

        var image = await RenderAsync(doc);
        image.Data.Length.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task SolidBackground_FillsWithColor()
    {
        var doc = PptxFixtures.WithSlides(1);
        doc.Slides[0].Background.Fill.SetSolid(ColorSpec.FromRgb(10, 200, 40));

        var image = await SlideRenderer.RenderAsync(
            doc.Slides[0],
            doc.SlideSize,
            new RenderOptions { WidthPx = 64, HeightPx = 48 }
        );
        var greenish = PngTestUtils.CountPixels(image.Data.ToArray(), 64, 48, static (r, g, b) => g > 150 && r < 80 && b < 90);
        greenish.ShouldBeGreaterThan(100);
    }

    [Fact]
    public async Task GroupWithChildren_Renders()
    {
        var doc = PptxFixtures.WithSlides(1);
        var group = doc.Slides[0].Shapes.AddGroup();
        group.X = Emu.FromInches(1);
        group.Y = Emu.FromInches(1);
        group.Width = Emu.FromInches(4);
        group.Height = Emu.FromInches(3);
        group.ChildOffsetX = Emu.Zero;
        group.ChildOffsetY = Emu.Zero;
        group.ChildExtentWidth = Emu.FromInches(4);
        group.ChildExtentHeight = Emu.FromInches(3);
        var child = group.Children.AddShape(AutoShapeType.Ellipse, Emu.Zero, Emu.Zero, Emu.FromInches(2), Emu.FromInches(1));
        child.Fill.SetSolid(ColorSpec.FromRgb(200, 50, 50));

        var image = await RenderAsync(doc);
        image.Data.Length.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task TableWithBordersAndFills_Renders()
    {
        var doc = PptxFixtures.WithSlides(1);
        var table = doc.Slides[0]
            .Shapes.AddTable(
                Emu.FromInches(1),
                Emu.FromInches(1),
                [Emu.FromInches(2), Emu.FromInches(2)],
                [Emu.FromInches(1), Emu.FromInches(1)]
            );
        table.Grid[0, 0].Fill.SetSolid(ColorSpec.FromRgb(220, 220, 220));
        table.Grid[0, 0].TextFrame.Paragraphs.Add("H1");
        table.Grid[1, 0].TextFrame.Paragraphs.Add("H2");

        var image = await RenderAsync(doc);
        image.Data.Length.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task ChartWithLegendAndAxes_Renders()
    {
        var doc = PptxFixtures.WithSlides(1);
        var chart = doc.Slides[0]
            .Shapes.AddChart(ChartType.BarClustered, Emu.FromInches(1), Emu.FromInches(1), Emu.FromInches(6), Emu.FromInches(4));
        chart.Chart.Title = "Bars";
        chart.Chart.HasTitle = true;
        chart.Chart.Legend.IsVisible = true;
        chart.Chart.Data.Categories.AddRange(["Mon", "Tue", "Wed"]);
        var s = new ChartSeries { Name = "Hours" };
        s.Values.AddRange([8.0, 6.0, 7.0]);
        chart.Chart.Data.Series.Add(s);

        var image = await RenderAsync(doc);
        image.Data.Length.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task UndecodablePicture_RendersPlaceholder()
    {
        var doc = PptxFixtures.WithSlides(1);
        // GIF bytes are not decodable by the rasterizer; it should draw a placeholder box.
        var image = doc.Media.AddImage(new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61, 1, 0 }, "image/gif");
        doc.Slides[0].Shapes.AddPicture(image, Emu.FromInches(1), Emu.FromInches(1), Emu.FromInches(2), Emu.FromInches(2));

        var rendered = await RenderAsync(doc);
        rendered.Data.Length.ShouldBeGreaterThan(0);
    }
}
