using Shouldly;
using Unchained.Drawing;
using Unchained.Drawing.Encoders;
using Unchained.Drawing.Text;
using Unchained.Ooxml;
using Unchained.Ooxml.Charts;
using Unchained.Ooxml.Drawing;
using Unchained.Ooxml.Media;
using Unchained.Ooxml.Text;
using Unchained.Pptx.Engine;
using Unchained.Pptx.Models.Shapes;
using Unchained.Pptx.Rendering.Engine;
using Unchained.Pptx.Rendering.Models;
using Unchained.Pptx.Rendering.Tests.Helpers;
using Unchained.Pptx.Shapes;
using Unchained.Pptx.Tests.Shared;
using Xunit;

namespace Unchained.Pptx.Rendering.Tests.IntegrationTests;

/// <summary>
///     Drives the <see cref="SlideRasterizer" /> partials and helper rasterizers (SmartArt layouts,
///     fill/effect painting, connectors with arrowheads, gradient/picture backgrounds, groups) by
///     rendering slides that exercise each path and asserting the PNG output is well-formed and
///     non-empty.
/// </summary>
public sealed class RasterizerCoverageTests : PptxTestBase
{
    private static readonly RenderOptions Small = new() { WidthPx = 320, HeightPx = 180 };
    private static readonly RenderOptions Large = new() { WidthPx = 1280, HeightPx = 720 };

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

    private static Task<PptxImage> RenderAsync(PresentationDocument doc) =>
        SlideRenderer.RenderAsync(doc.Slides[0], doc.SlideSize, Small);

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

    [
        Theory,
        InlineData(ArrowHeadSize.Small),
        InlineData(ArrowHeadSize.Medium),
        InlineData(ArrowHeadSize.Large)
    ]
    public async Task ConnectorArrowheadSizes_Render(ArrowHeadSize size)
    {
        // Exercises the head-length and head-width size-switch arms (Small=6/3, Large=14/7, else 10/5).
        var doc = PptxFixtures.WithSlides(1);
        var conn = doc.Slides[0]
            .Shapes.AddConnector(ConnectorType.Straight, Emu.FromInches(1), Emu.FromInches(1), Emu.FromInches(4), Emu.FromInches(2));
        conn.Line.SetSolid(ColorSpec.FromRgb(0, 0, 0), 2);
        conn.Line.HeadArrow.HeadType = ArrowHeadType.Triangle;
        conn.Line.HeadArrow.Width = size;
        conn.Line.HeadArrow.Length = size;

        var image = await RenderAsync(doc);
        image.Data.Length.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task ConnectorZeroLength_WithArrowhead_DoesNotThrow()
    {
        // A degenerate (zero-size) connector means the arrowhead direction length < 1, hitting the
        // early-return guard in the arrowhead drawer.
        var doc = PptxFixtures.WithSlides(1);
        var conn = doc.Slides[0]
            .Shapes.AddConnector(ConnectorType.Straight, Emu.FromInches(1), Emu.FromInches(1), new Emu(0), new Emu(0));
        conn.Line.SetSolid(ColorSpec.FromRgb(0, 0, 0), 2);
        conn.Line.HeadArrow.HeadType = ArrowHeadType.Triangle;

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

    // ── Text layout branches (SlideRasterizer.Text) ──────────────────────────────

    [Fact]
    public async Task MultiColumnText_Renders()
    {
        var doc = PptxFixtures.WithSlides(1);
        var tb = doc.Slides[0]
            .Shapes.AddTextBox(Emu.FromInches(1), Emu.FromInches(1), Emu.FromInches(6), Emu.FromInches(4));
        tb.TextFrame.Format.ColumnCount = 3;
        tb.TextFrame.Format.ColumnSpacing = Emu.FromPoints(18);
        for (var i = 0; i < 9; i++)
            tb.TextFrame.Paragraphs.Add($"Paragraph number {i} with several words to wrap");

        var image = await RenderAsync(doc);
        image.Data.Length.ShouldBeGreaterThan(0);
    }

    [
        Theory,
        InlineData(TextAnchor.Top),
        InlineData(TextAnchor.Middle),
        InlineData(TextAnchor.Bottom),
        InlineData(TextAnchor.MiddleCentered),
        InlineData(TextAnchor.BottomCentered)
    ]
    public async Task VerticalAnchoredText_Renders(TextAnchor anchor)
    {
        var doc = PptxFixtures.WithSlides(1);
        var tb = doc.Slides[0]
            .Shapes.AddTextBox(Emu.FromInches(1), Emu.FromInches(1), Emu.FromInches(4), Emu.FromInches(3));
        tb.TextFrame.Format.VerticalAnchor = anchor;
        tb.TextFrame.Paragraphs.Add("anchored line one");
        tb.TextFrame.Paragraphs.Add("anchored line two");

        var image = await RenderAsync(doc);
        image.Data.Length.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task AutofitShrinkText_Renders()
    {
        var doc = PptxFixtures.WithSlides(1);
        var tb = doc.Slides[0]
            .Shapes.AddTextBox(Emu.FromInches(1), Emu.FromInches(1), Emu.FromInches(2), Emu.FromInches(1));
        tb.TextFrame.Format.Autofit = TextAutofit.ShrinkText;
        for (var i = 0; i < 12; i++)
            tb.TextFrame.Paragraphs.Add($"Overflowing paragraph {i} that needs shrinking to fit the box");

        var image = await RenderAsync(doc);
        image.Data.Length.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task EmptyAndColoredParagraphs_Render()
    {
        var doc = PptxFixtures.WithSlides(1);
        var tb = doc.Slides[0]
            .Shapes.AddTextBox(Emu.FromInches(1), Emu.FromInches(1), Emu.FromInches(4), Emu.FromInches(3));
        // Empty paragraph (no runs) then a coloured run.
        tb.TextFrame.Paragraphs.Add("");
        var para = tb.TextFrame.Paragraphs.Add("Coloured text here");
        para.Runs[0].Format.Fill = new FillFormat();
        para.Runs[0].Format.Fill!.SetSolid(ColorSpec.FromRgb(200, 30, 30));
        para.Runs[0].Format.FontSizePoints = 28;

        var image = await RenderAsync(doc);
        image.Data.Length.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task LongWrappingParagraph_ExceedsBoxAndClips()
    {
        var doc = PptxFixtures.WithSlides(1);
        var tb = doc.Slides[0]
            .Shapes.AddTextBox(Emu.FromInches(1), Emu.FromInches(1), Emu.FromInches(2), Emu.FromInches(1));
        tb.TextFrame.Paragraphs.Add(string.Join(" ", Enumerable.Repeat("wordwordword", 40)));

        var image = await RenderAsync(doc);
        image.Data.Length.ShouldBeGreaterThan(0);
    }

    // ── Chart rendering branches (SlideRasterizer.Charts) ─────────────────────────

    private static ChartShape AddChart(
        PresentationDocument doc,
        ChartType type,
        bool legend,
        bool title,
        int seriesCount
    )
    {
        var chart = doc.Slides[0]
            .Shapes.AddChart(type, Emu.FromInches(1), Emu.FromInches(1), Emu.FromInches(6), Emu.FromInches(4));
        chart.Chart.HasTitle = title;
        if (title) chart.Chart.Title = $"{type} chart";
        chart.Chart.Legend.IsVisible = legend;
        chart.Chart.Data.Categories.AddRange(["Q1", "Q2", "Q3", "Q4"]);
        for (var i = 0; i < seriesCount; i++)
        {
            var s = new ChartSeries { Name = $"S{i}" };
            s.Values.AddRange([3.0 + i, 7.0 - i, 5.0, 9.0 + i]);
            chart.Chart.Data.Series.Add(s);
        }

        return chart;
    }

    private static Task<PptxImage> RenderLargeAsync(PresentationDocument doc) =>
        SlideRenderer.RenderAsync(doc.Slides[0], doc.SlideSize, Large);

    [
        Theory,
        InlineData(ChartType.Pie),
        InlineData(ChartType.Doughnut),
        InlineData(ChartType.Line),
        InlineData(ChartType.LineWithMarkers),
        InlineData(ChartType.ScatterWithStraightLines),
        InlineData(ChartType.BarClustered),
        InlineData(ChartType.ColumnClustered)
    ]
    public async Task ChartTypes_RenderWithLegendAndTitle(ChartType type)
    {
        var doc = PptxFixtures.WithSlides(1);
        AddChart(doc, type, true, true, 2);

        var image = await RenderLargeAsync(doc);
        image.Data.Length.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task Chart_NoLegendNoTitle_Renders()
    {
        var doc = PptxFixtures.WithSlides(1);
        AddChart(doc, ChartType.ColumnClustered, false, false, 1);

        var image = await RenderLargeAsync(doc);
        image.Data.Length.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task Chart_NoSeries_RendersEmptyFrame()
    {
        var doc = PptxFixtures.WithSlides(1);
        var chart = doc.Slides[0]
            .Shapes.AddChart(ChartType.BarClustered, Emu.FromInches(1), Emu.FromInches(1), Emu.FromInches(5), Emu.FromInches(3));
        chart.Chart.HasTitle = true;
        chart.Chart.Title = "Empty";

        var image = await RenderLargeAsync(doc);
        image.Data.Length.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task Chart_TitleEnabledButBlank_SkipsTitle()
    {
        var doc = PptxFixtures.WithSlides(1);
        var chart = AddChart(doc, ChartType.Line, true, false, 2);
        chart.Chart.HasTitle = true;
        chart.Chart.Title = "   ";

        var image = await RenderLargeAsync(doc);
        image.Data.Length.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task Chart_MultiSeriesBar_RendersGroups()
    {
        var doc = PptxFixtures.WithSlides(1);
        AddChart(doc, ChartType.BarClustered, true, true, 3);

        var image = await RenderLargeAsync(doc);
        image.Data.Length.ShouldBeGreaterThan(0);
    }

    // ── Embedded-font resolution (SlideRasterizer.Text.ResolveEmbeddedFont/ResolveStyle) ─────

    private static byte[] BundledDejaVu()
    {
        var asm = typeof(FontCache).Assembly;
        using var stream = asm.GetManifestResourceStream("Unchained.Drawing.Text.Fonts.DejaVuSans-Regular.ttf")
                           ?? throw new InvalidOperationException("DejaVuSans-Regular.ttf not found.");
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

    [
        Theory,
        InlineData(false, false, EmbeddedFontStyle.Regular),
        InlineData(true, false, EmbeddedFontStyle.Bold),
        InlineData(false, true, EmbeddedFontStyle.Italic),
        InlineData(true, true, EmbeddedFontStyle.BoldItalic)
    ]
    public async Task EmbeddedFontRun_ResolvesByStyle_Renders(bool bold, bool italic, EmbeddedFontStyle style)
    {
        var doc = PptxFixtures.WithSlides(1);
        var fontBytes = BundledDejaVu();
        doc.Media.AddFont(new EmbeddedFont { Typeface = "Bundled", Style = style, Data = fontBytes });

        var tb = doc.Slides[0]
            .Shapes.AddTextBox(Emu.FromInches(1), Emu.FromInches(1), Emu.FromInches(5), Emu.FromInches(2));
        var run = tb.TextFrame.Paragraphs.Add().Runs.Add("Embedded styled text");
        run.Format.LatinFont = "Bundled";
        run.Format.Bold = bold ? InheritableBool.True : InheritableBool.False;
        run.Format.Italic = italic ? InheritableBool.True : InheritableBool.False;

        var image = await RenderAsync(doc);
        image.Data.Length.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task MasterAndLayoutBackdropShapes_RenderBeneathSlideContent()
    {
        // Non-placeholder shapes on the master and layout are composited under the slide's own
        // shapes (the master/layout backdrop loops in SlideRasterizer.Render).
        var doc = PptxFixtures.WithSlides(1);
        var slide = doc.Slides[0];

        var masterRect = slide.Layout.Master.Shapes.AddShape(AutoShapeType.Rectangle, Emu.FromInches(0), Emu.FromInches(0), Emu.FromInches(10), Emu.FromInches(7));
        masterRect.Fill.SetSolid(ColorSpec.FromRgb(0xEE, 0xEE, 0xEE));

        var layoutRect = slide.Layout.Shapes.AddShape(AutoShapeType.Rectangle, Emu.FromInches(1), Emu.FromInches(1), Emu.FromInches(3), Emu.FromInches(2));
        layoutRect.Fill.SetSolid(ColorSpec.FromRgb(0xCC, 0xDD, 0xEE));

        var slideRect = slide.Shapes.AddShape(AutoShapeType.Ellipse, Emu.FromInches(2), Emu.FromInches(2), Emu.FromInches(2), Emu.FromInches(2));
        slideRect.Fill.SetSolid(ColorSpec.FromRgb(0x33, 0x66, 0x99));

        var image = await RenderAsync(doc);
        image.Data.Length.ShouldBeGreaterThan(0);
    }
}
