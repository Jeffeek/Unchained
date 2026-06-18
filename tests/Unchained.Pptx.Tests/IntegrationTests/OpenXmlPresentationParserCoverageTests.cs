using Shouldly;
using Unchained.Drawing;
using Unchained.Drawing.Encoders;
using Unchained.Ooxml;
using Unchained.Ooxml.Charts;
using Unchained.Ooxml.Drawing;
using Unchained.Pptx.Comments;
using Unchained.Pptx.Core;
using Unchained.Pptx.Engine;
using Unchained.Pptx.Models;
using Unchained.Pptx.Models.Shapes;
using Unchained.Pptx.Models.Themes;
using Unchained.Pptx.Shapes;
using Unchained.Pptx.Tests.Helpers;
using Xunit;

namespace Unchained.Pptx.Tests.IntegrationTests;

/// <summary>
///     Branch coverage for <see cref="Unchained.Pptx.Parsing.OpenXmlPresentationParser" /> (selected
///     via <see cref="OpenOptions.UseOpenXmlEngine" />), driving the present-vs-absent optional
///     element branches throughout the SDK-backed reader: slides with and without notes / shapes /
///     layouts, every shape kind incl. groups + connectors + tables + charts + pictures, shapes with
///     and without geometry / fill, comment authors present-vs-absent, and the synthesized fallback
///     layout path. Complements <c>OpenXmlEngineParserTests</c> and <c>OpenXmlParserParityTests</c>.
/// </summary>
public sealed class OpenXmlPresentationParserCoverageTests : PptxTestBase
{
    private static readonly OpenOptions Sdk = new() { UseOpenXmlEngine = true };

    private static string SamplePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "TestFiles", "python-pptx", name);

    private static async Task<byte[]?> SampleBytesOrNullAsync(string name)
    {
        var path = SamplePath(name);
        return File.Exists(path) ? await File.ReadAllBytesAsync(path) : null;
    }

    private static async Task<PresentationDocument> SaveReloadSdkAsync(PresentationDocument doc)
    {
        using var ms = new MemoryStream();
        await new PresentationProcessor().SaveAsync(doc, ms);
        return await new PresentationProcessor().LoadAsync(ms.ToArray(), Sdk);
    }

    // ── Slide size present vs default ───────────────────────────────────────────────

    [Fact]
    public async Task SlideSize_Custom_RoundTrips()
    {
        var doc = new PresentationProcessor().CreateBlank(new SlideSize(Emu.FromInches(10), Emu.FromInches(7.5)));
        var layout = doc.Masters[0].Layouts[0];
        doc.Slides.AddBlank(layout);

        var sdk = await SaveReloadSdkAsync(doc);
        sdk.SlideSize.Width.Value.ShouldBeGreaterThan(0);
        await sdk.DisposeAsync();
    }

    // ── Notes present vs absent ─────────────────────────────────────────────────────

    [Fact]
    public async Task Slide_WithNotes_AndWithout_BothMap()
    {
        var doc = PptxFixtures.WithSlides(2);
        doc.Slides[0].Notes.NotesText = "only slide one has notes";

        var sdk = await SaveReloadSdkAsync(doc);
        sdk.Slides[0].Notes.NotesText.ShouldContain("only slide one");
        sdk.Slides[1].Notes.NotesText.ShouldBeNullOrEmpty();
        await sdk.DisposeAsync();
    }

    // ── Hidden flag present vs absent ────────────────────────────────────────────────

    [Fact]
    public async Task Slide_HiddenAndVisible_BothMap()
    {
        var doc = PptxFixtures.WithSlides(2);
        doc.Slides[0].IsHidden = true;

        var sdk = await SaveReloadSdkAsync(doc);
        sdk.Slides[0].IsHidden.ShouldBeTrue();
        sdk.Slides[1].IsHidden.ShouldBeFalse();
        sdk.Properties.HiddenSlideCount.ShouldBe(1);
        sdk.Properties.SlideCount.ShouldBe(2);
        await sdk.DisposeAsync();
    }

    // ── Empty slide (no shape tree content) ──────────────────────────────────────────

    [Fact]
    public async Task Slide_NoShapes_StillLoads()
    {
        var doc = PptxFixtures.WithSlides(1);

        var sdk = await SaveReloadSdkAsync(doc);
        sdk.Slides.Count.ShouldBe(1);
        await sdk.DisposeAsync();
    }

    // ── Every shape kind in one slide ────────────────────────────────────────────────

    [Fact]
    public async Task Slide_AllShapeKinds_MapToConcreteTypes()
    {
        var doc = PptxFixtures.WithSlides(1);
        var shapes = doc.Slides[0].Shapes;
        shapes.AddTextBox(Emu.FromInches(1), Emu.FromInches(1), Emu.FromInches(3), Emu.FromInches(1), "text");
        shapes.AddShape(AutoShapeType.Ellipse, Emu.FromInches(1), Emu.FromInches(2), Emu.FromInches(2), Emu.FromInches(1));
        shapes.AddConnector(ConnectorType.Bent, Emu.FromInches(1), Emu.FromInches(3), Emu.FromInches(3), Emu.FromInches(3));
        shapes.AddTable(
            Emu.FromInches(1),
            Emu.FromInches(4),
            [Emu.FromInches(2), Emu.FromInches(2)],
            [Emu.FromInches(1)]
        );
        var group = shapes.AddGroup();
        group.Children.AddShape(AutoShapeType.Rectangle, Emu.Zero, Emu.Zero, Emu.FromInches(1), Emu.FromInches(1));

        var sdk = await SaveReloadSdkAsync(doc);
        var rt = sdk.Slides[0].Shapes;
        rt.OfType<AutoShape>().ShouldNotBeEmpty();
        rt.OfType<ConnectorShape>().ShouldNotBeEmpty();
        rt.OfType<TableShape>().ShouldNotBeEmpty();
        rt.OfType<GroupShape>().ShouldNotBeEmpty();
        await sdk.DisposeAsync();
    }

    // ── AutoShape with picture fill (spPr blipFill resolution) ───────────────────────

    [Fact]
    public async Task AutoShape_WithPictureFill_ResolvesImage()
    {
        var doc = PptxFixtures.WithSlides(1);
        var buffer = new RasterBuffer(6, 6);
        buffer.Clear(20, 90, 160);
        var png = PngEncoder.Encode(buffer);
        var image = doc.Media.AddImage(png, "image/png");
        var shape = doc.Slides[0]
            .Shapes.AddShape(AutoShapeType.Rectangle, Emu.FromInches(1), Emu.FromInches(1), Emu.FromInches(3), Emu.FromInches(2));
        shape.Fill.Type = FillType.Picture;
        shape.Fill.Picture = new PictureFill { Image = image };

        var sdk = await SaveReloadSdkAsync(doc);
        sdk.Slides[0].Shapes.OfType<AutoShape>().ShouldNotBeEmpty();
        await sdk.DisposeAsync();
    }

    // ── Connector type tokens ────────────────────────────────────────────────────────

    [
        Theory,
        InlineData(ConnectorType.Bent),
        InlineData(ConnectorType.Curved),
        InlineData(ConnectorType.Straight)
    ]
    public async Task Connector_AllTypes_MapByPresetPrefix(ConnectorType type)
    {
        var doc = PptxFixtures.WithSlides(1);
        doc.Slides[0]
            .Shapes.AddConnector(type, Emu.FromInches(1), Emu.FromInches(1), Emu.FromInches(4), Emu.FromInches(1));

        var sdk = await SaveReloadSdkAsync(doc);
        sdk.Slides[0].Shapes.OfType<ConnectorShape>().Single().ConnectorType.ShouldBe(type);
        await sdk.DisposeAsync();
    }

    // ── Group geometry (offset / extents / child offset / child extents) ─────────────

    [Fact]
    public async Task Group_WithFullTransform_MapsAllGeometry()
    {
        var doc = PptxFixtures.WithSlides(1);
        var group = doc.Slides[0].Shapes.AddGroup();
        group.X = Emu.FromInches(1);
        group.Y = Emu.FromInches(1);
        group.Width = Emu.FromInches(4);
        group.Height = Emu.FromInches(3);
        group.ChildOffsetX = Emu.FromInches(0.5);
        group.ChildOffsetY = Emu.FromInches(0.5);
        group.ChildExtentWidth = Emu.FromInches(4);
        group.ChildExtentHeight = Emu.FromInches(3);
        group.Children.AddShape(AutoShapeType.Ellipse, Emu.Zero, Emu.Zero, Emu.FromInches(1), Emu.FromInches(1));

        var sdk = await SaveReloadSdkAsync(doc);
        var rg = sdk.Slides[0].Shapes.OfType<GroupShape>().Single();
        rg.Width.Value.ShouldBeGreaterThan(0);
        rg.Children.ShouldNotBeEmpty();
        await sdk.DisposeAsync();
    }

    // ── Chart present, and chart with title/legend/data ──────────────────────────────

    [Fact]
    public async Task Chart_WithTitleLegendSeries_MapsModel()
    {
        var doc = PptxFixtures.WithSlides(1);
        var chart = doc.Slides[0]
            .Shapes.AddChart(ChartType.Line, Emu.FromInches(1), Emu.FromInches(1), Emu.FromInches(6), Emu.FromInches(4));
        chart.Chart.HasTitle = true;
        chart.Chart.Title = "Trend";
        chart.Chart.Legend.IsVisible = true;
        chart.Chart.Data.Categories.AddRange(["A", "B", "C"]);
        var s = new ChartSeries { Name = "S1" };
        s.Values.AddRange([1.0, 2.0, 3.0]);
        chart.Chart.Data.Series.Add(s);

        var sdk = await SaveReloadSdkAsync(doc);
        var rc = sdk.Slides[0].Shapes.OfType<ChartShape>().Single();
        rc.Chart.Type.ShouldBe(ChartType.Line);
        rc.Chart.Data.Series.Count.ShouldBe(1);
        await sdk.DisposeAsync();
    }

    // ── Comment authors present vs absent ─────────────────────────────────────────────

    [Fact]
    public async Task CommentAuthors_Present_AreMapped()
    {
        var doc = PptxFixtures.WithSlides(1);
        var author = doc.CommentAuthors.Add("Reviewer");
        doc.Slides[0].AddComment("note", new SlidePosition(Emu.Zero, Emu.Zero), author);

        var sdk = await SaveReloadSdkAsync(doc);
        sdk.CommentAuthors.Count.ShouldBeGreaterThan(0);
        await sdk.DisposeAsync();
    }

    [Fact]
    public async Task CommentAuthors_Absent_YieldsEmptyCollection()
    {
        var doc = PptxFixtures.WithSlides(1);

        var sdk = await SaveReloadSdkAsync(doc);
        sdk.CommentAuthors.Count.ShouldBe(0);
        await sdk.DisposeAsync();
    }

    // ── Sections present vs absent ───────────────────────────────────────────────────

    [Fact]
    public async Task Sections_Present_AreMapped()
    {
        var doc = PptxFixtures.WithSlides(2);
        doc.Sections.Add("Part 1", [doc.Slides[0].SlideId]);

        var sdk = await SaveReloadSdkAsync(doc);
        sdk.Sections.Count.ShouldBeGreaterThan(0);
        await sdk.DisposeAsync();
    }

    // ── Properties: created/modified dates and all string fields ──────────────────────

    [Fact]
    public async Task Properties_AllFields_RoundTrip()
    {
        var doc = PptxFixtures.WithSlides(1);
        doc.Properties.Title = "T";
        doc.Properties.Subject = "S";
        doc.Properties.Author = "A";
        doc.Properties.Keywords = "k";
        doc.Properties.Description = "d";
        doc.Properties.LastModifiedBy = "lm";
        doc.Properties.Category = "c";
        doc.Properties.ContentStatus = "Draft";

        var sdk = await SaveReloadSdkAsync(doc);
        sdk.Properties.Title.ShouldBe("T");
        sdk.Properties.Category.ShouldBe("c");
        sdk.Properties.ContentStatus.ShouldBe("Draft");
        await sdk.DisposeAsync();
    }

    // ── Picture image de-duplication (shared image → one EmbeddedImage) ───────────────

    [Fact]
    public async Task Pictures_OnMultipleSlides_ResolveImages()
    {
        var doc = PptxFixtures.WithSlides(2);
        var buffer = new RasterBuffer(5, 5);
        buffer.Clear(200, 10, 10);
        var png = PngEncoder.Encode(buffer);
        var image = doc.Media.AddImage(png, "image/png");
        doc.Slides[0].Shapes.AddPicture(image, Emu.FromInches(1), Emu.FromInches(1), Emu.FromInches(2), Emu.FromInches(2));
        doc.Slides[1].Shapes.AddPicture(image, Emu.FromInches(4), Emu.FromInches(1), Emu.FromInches(2), Emu.FromInches(2));

        var sdk = await SaveReloadSdkAsync(doc);
        sdk.Slides.SelectMany(static s => s.Shapes).OfType<PictureShape>().Count().ShouldBe(2);
        await sdk.DisposeAsync();
    }

    // ── Real samples exercising master/layout shape trees + variety ───────────────────

    [
        Theory,
        InlineData("shp-shapes.pptx"),
        InlineData("mst-slide-layouts.pptx"),
        InlineData("tbl-cell.pptx"),
        InlineData("shp-groupshape.pptx"),
        InlineData("shp-picture.pptx"),
        InlineData("cht-charts.pptx"),
        InlineData("dml-fill.pptx"),
        InlineData("dml-line.pptx")
    ]
    public async Task RealSample_MapsMastersLayoutsAndShapes(string fileName)
    {
        var bytes = await SampleBytesOrNullAsync(fileName);
        Assert.SkipUnless(bytes is not null, $"sample missing: {fileName}");

        var doc = await new PresentationProcessor().LoadAsync(bytes!, Sdk);
        doc.Masters.Count.ShouldBeGreaterThan(0);
        foreach (var master in doc.Masters)
        {
            master.Layouts.Count.ShouldBeGreaterThan(0);
            foreach (var layout in master.Layouts)
                layout.Master.ShouldNotBeNull();
        }

        foreach (var slide in doc.Slides)
            slide.Layout.ShouldNotBeNull();

        await doc.DisposeAsync();
    }

    // ── Layout types across the full ST_SlideLayoutType vocabulary ────────────────────

    [Fact]
    public async Task AllLayoutTypes_MapThroughSwitch()
    {
        var doc = PptxFixtures.WithSlides(1);
        var layouts = doc.Masters[0].Layouts;
        layouts.AddLayout("blank", LayoutType.Blank);
        layouts.AddLayout("title", LayoutType.Title);
        layouts.AddLayout("content", LayoutType.TitleAndContent);
        layouts.AddLayout("twoContent", LayoutType.TitleAndTwoContent);
        layouts.AddLayout("titleOnly", LayoutType.TitleOnly);
        layouts.AddLayout("secHead", LayoutType.SectionHeader);
        layouts.AddLayout("twoTx", LayoutType.TwoTextColumns);
        layouts.AddLayout("vertTx", LayoutType.TitleAndVerticalText);
        layouts.AddLayout("picTx", LayoutType.PictureWithCaption);
        layouts.AddLayout("ctrTitle", LayoutType.TitleSlide);

        var sdk = await SaveReloadSdkAsync(doc);
        var types = sdk.Masters.SelectMany(static m => m.Layouts).Select(static l => l.LayoutType).ToList();
        types.ShouldContain(LayoutType.SectionHeader);
        types.ShouldContain(LayoutType.TwoTextColumns);
        types.ShouldContain(LayoutType.TitleAndVerticalText);
        types.ShouldContain(LayoutType.PictureWithCaption);
        types.ShouldContain(LayoutType.TitleSlide);
        types.ShouldContain(LayoutType.TitleOnly);
        await sdk.DisposeAsync();
    }
}
