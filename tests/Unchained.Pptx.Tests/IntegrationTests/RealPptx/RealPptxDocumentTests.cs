using Shouldly;
using Unchained.Pptx.Shapes;
using Unchained.Pptx.Tests.Shared;
using Xunit;

namespace Unchained.Pptx.Tests.IntegrationTests.RealPptx;

/// <summary>
///     Integration tests that run against the committed python-pptx sample files in
///     <c>TestFiles/python-pptx/</c>.
/// </summary>
public sealed class RealPptxDocumentTests : PptxTestBase
{
    private static string FilePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "TestFiles", "python-pptx", name);

    [Fact]
    public async Task Simple_LoadsWithoutException()
    {
        await using var stream = File.OpenRead(FilePath("sld-slides.pptx"));
        var doc = await Processor.LoadAsync(stream);
        doc.ShouldNotBeNull();
        doc.Slides.Count.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task Simple_RoundTrip_PreservesSlideCount()
    {
        await using var stream = File.OpenRead(FilePath("sld-slides.pptx"));
        var doc = await Processor.LoadAsync(stream);
        var count = doc.Slides.Count;

        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        reloaded.Slides.Count.ShouldBe(count);
    }

    [Fact]
    public async Task Multipage_HasExpectedSlideCount()
    {
        await using var stream = File.OpenRead(FilePath("sld-slides.pptx"));
        var doc = await Processor.LoadAsync(stream);
        doc.Slides.Count.ShouldBeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task WithTables_ContainsTableShape()
    {
        await using var stream = File.OpenRead(FilePath("tbl-cell.pptx"));
        var doc = await Processor.LoadAsync(stream);

        var hasTable = doc.Slides
            .Any(static s => s.Shapes.OfType<TableShape>().Any());
        hasTable.ShouldBeTrue();
    }

    [Fact]
    public async Task WithImages_ContainsPictureShape()
    {
        await using var stream = File.OpenRead(FilePath("shp-picture.pptx"));
        var doc = await Processor.LoadAsync(stream);

        var hasPicture = doc.Slides
            .Any(static s => s.Shapes.OfType<PictureShape>().Any());
        hasPicture.ShouldBeTrue();
    }

    [Fact]
    public async Task AnyPptx_GetAllText_DoesNotThrow()
    {
        await using var stream = File.OpenRead(FilePath("sld-slides.pptx"));
        var doc = await Processor.LoadAsync(stream);

        foreach (var text in doc.Slides.Select(static slide => slide.GetAllText())) text.ShouldNotBeNull();
    }
}
