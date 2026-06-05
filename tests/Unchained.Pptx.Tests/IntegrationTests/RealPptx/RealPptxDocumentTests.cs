using Shouldly;
using Unchained.Pptx.Tests.Helpers;
using Xunit;

namespace Unchained.Pptx.Tests.IntegrationTests.RealPptx;

/// <summary>
/// Integration tests that run against real .pptx files placed in TestFiles/.
/// All tests skip gracefully when the file is absent.
/// </summary>
public sealed class RealPptxDocumentTests : PptxTestBase
{
    private static string TestFilesDir =>
        Path.Combine(AppContext.BaseDirectory, "TestFiles");

    private static string FilePath(string name) =>
        Path.Combine(TestFilesDir, name);

    private static bool Exists(string name) => File.Exists(FilePath(name));

    [Fact]
    public async Task Simple_LoadsWithoutException()
    {
        if (!Exists("simple.pptx")) return; // skip gracefully

        using var stream = File.OpenRead(FilePath("simple.pptx"));
        var doc = await Processor.LoadAsync(stream);
        doc.ShouldNotBeNull();
        doc.Slides.Count.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task Simple_RoundTrip_PreservesSlideCount()
    {
        if (!Exists("simple.pptx")) return;

        using var stream = File.OpenRead(FilePath("simple.pptx"));
        var doc = await Processor.LoadAsync(stream);
        var count = doc.Slides.Count;

        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        reloaded.Slides.Count.ShouldBe(count);
    }

    [Fact]
    public async Task Multipage_HasExpectedSlideCount()
    {
        if (!Exists("multipage.pptx")) return;

        using var stream = File.OpenRead(FilePath("multipage.pptx"));
        var doc = await Processor.LoadAsync(stream);
        doc.Slides.Count.ShouldBeGreaterThanOrEqualTo(5);
    }

    [Fact]
    public async Task WithTables_ContainsTableShape()
    {
        if (!Exists("with-tables.pptx")) return;

        using var stream = File.OpenRead(FilePath("with-tables.pptx"));
        var doc = await Processor.LoadAsync(stream);

        var hasTable = doc.Slides
            .Any(s => s.Shapes.OfType<Shapes.TableShape>().Any());
        hasTable.ShouldBeTrue();
    }

    [Fact]
    public async Task WithImages_ContainsPictureShape()
    {
        if (!Exists("with-images.pptx")) return;

        using var stream = File.OpenRead(FilePath("with-images.pptx"));
        var doc = await Processor.LoadAsync(stream);

        var hasPicture = doc.Slides
            .Any(s => s.Shapes.OfType<Shapes.PictureShape>().Any());
        hasPicture.ShouldBeTrue();
    }

    [Fact]
    public async Task AnyPptx_GetAllText_DoesNotThrow()
    {
        if (!Exists("simple.pptx")) return;

        using var stream = File.OpenRead(FilePath("simple.pptx"));
        var doc = await Processor.LoadAsync(stream);

        foreach (var slide in doc.Slides)
        {
            var text = slide.GetAllText();
            text.ShouldNotBeNull();
        }
    }
}
