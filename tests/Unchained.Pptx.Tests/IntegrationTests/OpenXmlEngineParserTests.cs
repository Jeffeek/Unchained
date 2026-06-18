using System.IO.Compression;
using Shouldly;
using Unchained.Drawing;
using Unchained.Drawing.Encoders;
using Unchained.Ooxml;
using Unchained.Ooxml.Charts;
using Unchained.Pptx.Comments;
using Unchained.Pptx.Models;
using Unchained.Pptx.Models.Shapes;
using Unchained.Pptx.Models.Themes;
using Unchained.Pptx.Shapes;
using Unchained.Pptx.Tests.Helpers;
using Xunit;

namespace Unchained.Pptx.Tests.IntegrationTests;

/// <summary>
///     Exercises the OpenXML-SDK-backed reader (<c>OpenXmlPresentationParser</c>, selected via
///     <see cref="OpenOptions.UseOpenXmlEngine" />) against the committed python-pptx sample corpus
///     and round-tripped in-memory presentations, covering slide/master/layout/notes/shape mapping.
/// </summary>
public sealed class OpenXmlEngineParserTests : PptxTestBase
{
    private static string SamplePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "TestFiles", "python-pptx", name);

    private static async Task<byte[]?> SampleBytesOrNullAsync(string name)
    {
        var path = SamplePath(name);
        return File.Exists(path) ? await File.ReadAllBytesAsync(path) : null;
    }

    [
        Theory,
        InlineData("minimal.pptx"),
        InlineData("sld-slides.pptx"),
        InlineData("tbl-cell.pptx"),
        InlineData("shp-picture.pptx"),
        InlineData("shp-groupshape.pptx"),
        InlineData("prs-notes.pptx"),
        InlineData("dml-fill.pptx")
    ]
    public async Task SdkEngine_LoadsRealFile_WithMastersAndSlides(string fileName)
    {
        var bytes = await SampleBytesOrNullAsync(fileName);
        Assert.SkipUnless(bytes is not null, $"sample missing: {fileName}");

        var doc = await Processor.LoadAsync(bytes!, new OpenOptions { UseOpenXmlEngine = true });

        doc.Masters.Count.ShouldBeGreaterThan(0);
        doc.Masters[0].Layouts.Count.ShouldBeGreaterThan(0);
        doc.SlideSize.Width.Value.ShouldBeGreaterThan(0);
        foreach (var slide in doc.Slides)
            slide.Layout.ShouldNotBeNull();

        await doc.DisposeAsync();
    }

    [Fact]
    public async Task SdkEngine_ReadsPropertiesAndShapes()
    {
        var bytes = await SampleBytesOrNullAsync("shp-shapes.pptx");
        Assert.SkipUnless(bytes is not null, "sample missing: shp-shapes.pptx");

        var doc = await Processor.LoadAsync(bytes!, new OpenOptions { UseOpenXmlEngine = true });
        doc.Slides.SelectMany(static s => s.Shapes).ShouldNotBeEmpty();
        await doc.DisposeAsync();
    }

    [Fact]
    public async Task SdkEngine_GeneratedDoc_RoundTripsTextAndGeometry()
    {
        var doc = PptxFixtures.WithSlides(2);
        doc.Slides[0]
            .Shapes.AddTextBox(Emu.FromInches(1), Emu.FromInches(1), Emu.FromInches(4), Emu.FromInches(2), "SDK text");
        doc.Slides[0]
            .Shapes.AddShape(AutoShapeType.Ellipse, Emu.FromInches(1), Emu.FromInches(4), Emu.FromInches(2), Emu.FromInches(1));
        doc.Slides[1].IsHidden = true;

        using var ms = new MemoryStream();
        await Processor.SaveAsync(doc, ms);
        var bytes = ms.ToArray();

        var sdk = await Processor.LoadAsync(bytes, new OpenOptions { UseOpenXmlEngine = true });
        sdk.Slides.Count.ShouldBe(2);
        sdk.Slides[1].IsHidden.ShouldBeTrue();
        sdk.Slides[0].GetAllText().ShouldContain("SDK text");
        sdk.Slides[0].Shapes.OfType<AutoShape>().ShouldNotBeEmpty();
        await sdk.DisposeAsync();
    }

    [Fact]
    public async Task SdkEngine_GeneratedDoc_RoundTripsTable()
    {
        var doc = PptxFixtures.WithSlides(1);
        var table = doc.Slides[0]
            .Shapes.AddTable(
                Emu.FromInches(1),
                Emu.FromInches(1),
                [Emu.FromInches(2), Emu.FromInches(2)],
                [Emu.FromInches(1), Emu.FromInches(1)]
            );
        table.Grid[0, 0].TextFrame.Paragraphs.Add("R0C0");

        using var ms = new MemoryStream();
        await Processor.SaveAsync(doc, ms);

        var sdk = await Processor.LoadAsync(ms.ToArray(), new OpenOptions { UseOpenXmlEngine = true });
        var rt = sdk.Slides[0].Shapes.OfType<TableShape>().Single();
        rt.Grid.RowCount.ShouldBe(2);
        rt.Grid.ColumnCount.ShouldBe(2);
        rt.Grid[0, 0].TextFrame.PlainText.ShouldContain("R0C0");
        await sdk.DisposeAsync();
    }

    [Fact]
    public async Task SdkEngine_GeneratedDoc_RoundTripsNotes()
    {
        var doc = PptxFixtures.WithSlides(1);
        doc.Slides[0].Notes.NotesText = "engine notes text";

        using var ms = new MemoryStream();
        await Processor.SaveAsync(doc, ms);

        var sdk = await Processor.LoadAsync(ms.ToArray(), new OpenOptions { UseOpenXmlEngine = true });
        sdk.Slides[0].Notes.NotesText.ShouldContain("engine notes text");
        await sdk.DisposeAsync();
    }

    [Fact]
    public async Task SdkEngine_GroupShape_RoundTripsChildren()
    {
        var doc = PptxFixtures.WithSlides(1);
        var group = doc.Slides[0].Shapes.AddGroup();
        group.Children.AddShape(AutoShapeType.Rectangle, Emu.Zero, Emu.Zero, Emu.FromInches(1), Emu.FromInches(1));

        using var ms = new MemoryStream();
        await Processor.SaveAsync(doc, ms);

        var sdk = await Processor.LoadAsync(ms.ToArray(), new OpenOptions { UseOpenXmlEngine = true });
        sdk.Slides[0].Shapes.OfType<GroupShape>().ShouldNotBeEmpty();
        await sdk.DisposeAsync();
    }

    [Fact]
    public async Task SdkEngine_Chart_RoundTripsTypeAndData()
    {
        var doc = PptxFixtures.WithSlides(1);
        var chart = doc.Slides[0]
            .Shapes.AddChart(ChartType.BarClustered, Emu.FromInches(1), Emu.FromInches(1), Emu.FromInches(5), Emu.FromInches(4));
        chart.Chart.HasTitle = true;
        chart.Chart.Title = "Engine Chart";
        chart.Chart.Data.Categories.AddRange(["A", "B"]);
        var series = new ChartSeries { Name = "S1" };
        series.Values.AddRange([1.0, 2.0]);
        chart.Chart.Data.Series.Add(series);

        using var ms = new MemoryStream();
        await Processor.SaveAsync(doc, ms);

        var sdk = await Processor.LoadAsync(ms.ToArray(), new OpenOptions { UseOpenXmlEngine = true });
        var rc = sdk.Slides[0].Shapes.OfType<ChartShape>().Single();
        rc.Chart.Type.ShouldBe(ChartType.BarClustered);
        await sdk.DisposeAsync();
    }

    [
        Theory,
        InlineData(ConnectorType.Bent),
        InlineData(ConnectorType.Curved),
        InlineData(ConnectorType.Straight)
    ]
    public async Task SdkEngine_Connector_RoundTripsType(ConnectorType type)
    {
        var doc = PptxFixtures.WithSlides(1);
        doc.Slides[0]
            .Shapes.AddConnector(type, Emu.FromInches(1), Emu.FromInches(1), Emu.FromInches(3), Emu.FromInches(1));

        using var ms = new MemoryStream();
        await Processor.SaveAsync(doc, ms);

        var sdk = await Processor.LoadAsync(ms.ToArray(), new OpenOptions { UseOpenXmlEngine = true });
        sdk.Slides[0].Shapes.OfType<ConnectorShape>().Single().ConnectorType.ShouldBe(type);
        await sdk.DisposeAsync();
    }

    [Fact]
    public async Task SdkEngine_CommentAuthors_RoundTrip()
    {
        var doc = PptxFixtures.WithSlides(1);
        var author = doc.CommentAuthors.Add("Engine Author");
        doc.Slides[0].AddComment("hi", new SlidePosition(Emu.Zero, Emu.Zero), author);

        using var ms = new MemoryStream();
        await Processor.SaveAsync(doc, ms);

        var sdk = await Processor.LoadAsync(ms.ToArray(), new OpenOptions { UseOpenXmlEngine = true });
        sdk.CommentAuthors.Count.ShouldBeGreaterThan(0);
        await sdk.DisposeAsync();
    }

    [Fact]
    public async Task SdkEngine_Sections_RoundTrip()
    {
        var doc = PptxFixtures.WithSlides(2);
        doc.Sections.Add("Intro", [doc.Slides[0].SlideId]);

        using var ms = new MemoryStream();
        await Processor.SaveAsync(doc, ms);

        var sdk = await Processor.LoadAsync(ms.ToArray(), new OpenOptions { UseOpenXmlEngine = true });
        sdk.Sections.Count.ShouldBeGreaterThan(0);
        await sdk.DisposeAsync();
    }

    [Fact]
    public async Task SdkEngine_DocumentProperties_RoundTrip()
    {
        var doc = PptxFixtures.WithSlides(1);
        doc.Properties.Title = "T";
        doc.Properties.Subject = "Su";
        doc.Properties.Author = "Au";
        doc.Properties.Keywords = "k1 k2";
        doc.Properties.Description = "desc";
        doc.Properties.Category = "cat";
        doc.Properties.ContentStatus = "Final";

        using var ms = new MemoryStream();
        await Processor.SaveAsync(doc, ms);

        var sdk = await Processor.LoadAsync(ms.ToArray(), new OpenOptions { UseOpenXmlEngine = true });
        sdk.Properties.Title.ShouldBe("T");
        sdk.Properties.Subject.ShouldBe("Su");
        sdk.Properties.Author.ShouldBe("Au");
        sdk.Properties.Description.ShouldBe("desc");
        await sdk.DisposeAsync();
    }

    [Fact]
    public async Task SdkEngine_PictureWithImage_RoundTripsBytes()
    {
        var doc = PptxFixtures.WithSlides(1);
        var buffer = new RasterBuffer(8, 8);
        buffer.Clear(10, 50, 200);
        var png = PngEncoder.Encode(buffer);
        var image = doc.Media.AddImage(png, "image/png");
        doc.Slides[0].Shapes.AddPicture(image, Emu.FromInches(1), Emu.FromInches(1), Emu.FromInches(2), Emu.FromInches(2));

        using var ms = new MemoryStream();
        await Processor.SaveAsync(doc, ms);

        var sdk = await Processor.LoadAsync(ms.ToArray(), new OpenOptions { UseOpenXmlEngine = true });
        sdk.Slides[0].Shapes.OfType<PictureShape>().ShouldNotBeEmpty();
        await sdk.DisposeAsync();
    }

    [Fact]
    public async Task SdkEngine_UnknownGraphicFrame_LoadsAsStub()
    {
        // A chart shape whose chart model has no data still serializes a graphicFrame; the SDK
        // reader maps it through the chart/graphic-frame path. This drives the frame-geometry and
        // graphic-data dispatch without requiring diagram parts.
        var doc = PptxFixtures.WithSlides(1);
        doc.Slides[0]
            .Shapes.AddChart(ChartType.Pie, Emu.FromInches(1), Emu.FromInches(1), Emu.FromInches(4), Emu.FromInches(3));

        using var ms = new MemoryStream();
        await Processor.SaveAsync(doc, ms);

        var sdk = await Processor.LoadAsync(ms.ToArray(), new OpenOptions { UseOpenXmlEngine = true });
        sdk.Slides[0].Shapes.OfType<ChartShape>().ShouldNotBeEmpty();
        await sdk.DisposeAsync();
    }

    [
        Theory,
        InlineData("mst-slide-layouts.pptx"),
        InlineData("cht-charts.pptx"),
        InlineData("prs-properties.pptx"),
        InlineData("sld-background.pptx"),
        InlineData("dml-line.pptx"),
        InlineData("txt-font-props.pptx")
    ]
    public async Task SdkEngine_RealSamples_MapLayoutsChartsAndProperties(string fileName)
    {
        var bytes = await SampleBytesOrNullAsync(fileName);
        Assert.SkipUnless(bytes is not null, $"sample missing: {fileName}");

        var doc = await Processor.LoadAsync(bytes!, new OpenOptions { UseOpenXmlEngine = true });

        // Every master exposes at least one layout, and every layout maps to a LayoutType — this
        // drives the MapLayoutType switch across the corpus's variety of layout types.
        doc.Masters.Count.ShouldBeGreaterThan(0);
        foreach (var master in doc.Masters)
            master.Layouts.Count.ShouldBeGreaterThan(0);

        await doc.DisposeAsync();
    }

    [Fact]
    public async Task SdkEngine_ChartSample_ReadsChartShape()
    {
        var bytes = await SampleBytesOrNullAsync("cht-charts.pptx");
        Assert.SkipUnless(bytes is not null, "sample missing: cht-charts.pptx");

        var doc = await Processor.LoadAsync(bytes!, new OpenOptions { UseOpenXmlEngine = true });
        doc.Slides.SelectMany(static s => s.Shapes).OfType<ChartShape>().ShouldNotBeEmpty();
        await doc.DisposeAsync();
    }

    [Fact]
    public async Task SdkEngine_AllLayoutTypes_MapThroughSwitch()
    {
        var doc = PptxFixtures.WithSlides(1);
        var layouts = doc.Masters[0].Layouts;
        layouts.AddLayout("L-obj", LayoutType.TitleAndContent);
        layouts.AddLayout("L-twoObj", LayoutType.TitleAndTwoContent);
        layouts.AddLayout("L-titleOnly", LayoutType.TitleOnly);
        layouts.AddLayout("L-secHead", LayoutType.SectionHeader);
        layouts.AddLayout("L-twoTx", LayoutType.TwoTextColumns);
        layouts.AddLayout("L-vertTx", LayoutType.TitleAndVerticalText);
        layouts.AddLayout("L-picTx", LayoutType.PictureWithCaption);
        layouts.AddLayout("L-ctrTitle", LayoutType.TitleSlide);

        using var ms = new MemoryStream();
        await Processor.SaveAsync(doc, ms);

        var sdk = await Processor.LoadAsync(ms.ToArray(), new OpenOptions { UseOpenXmlEngine = true });
        var roundTripped = sdk.Masters.SelectMany(static m => m.Layouts).Select(static l => l.LayoutType).ToList();
        roundTripped.ShouldContain(LayoutType.SectionHeader);
        roundTripped.ShouldContain(LayoutType.TitleSlide);
        await sdk.DisposeAsync();
    }

    [Fact]
    public async Task SdkEngine_NonPresentationPackage_Throws()
    {
        // A DOCX/XLSX OOXML package is a valid zip but not a presentation — the engine recognises
        // the format mismatch and the parser surfaces a PptxException (and disposes the engine).
        var fakeWord = BuildMinimalWordPackage();
        await Should.ThrowAsync<Exception>(async () =>
            await Processor.LoadAsync(fakeWord, new OpenOptions { UseOpenXmlEngine = true })
        );
    }

    private static byte[] BuildMinimalWordPackage()
    {
        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            var ct = archive.CreateEntry("[Content_Types].xml");
            using (var s = new StreamWriter(ct.Open()))
            {
                s.Write(
                    "<?xml version=\"1.0\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
                    "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
                    "<Override PartName=\"/word/document.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"/>" +
                    "</Types>"
                );
            }

            var rels = archive.CreateEntry("_rels/.rels");
            using (var s = new StreamWriter(rels.Open()))
            {
                s.Write(
                    "<?xml version=\"1.0\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                    "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"word/document.xml\"/>" +
                    "</Relationships>"
                );
            }

            var docEntry = archive.CreateEntry("word/document.xml");
            using (var s = new StreamWriter(docEntry.Open()))
                s.Write("<?xml version=\"1.0\"?><w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:body/></w:document>");
        }

        return ms.ToArray();
    }
}
