using Shouldly;
using Unchained.Ooxml;
using Unchained.Pptx.Models;
using Unchained.Pptx.Models.Shapes;
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
}
