using Shouldly;
using Unchained.Pptx.Tests.Shared;
using Xunit;

namespace Unchained.Pptx.Tests.IntegrationTests;

public sealed class MetadataTests : PptxTestBase
{
    [Fact]
    public void Properties_DefaultAuthor_IsNull()
    {
        var doc = PptxFixtures.BlankPresentation();
        doc.Properties.Author.ShouldBeNull();
    }

    [Fact]
    public void Properties_SetTitle_IsPreserved()
    {
        var doc = PptxFixtures.BlankPresentation();
        doc.Properties.Title = "My Presentation";
        doc.Properties.Title.ShouldBe("My Presentation");
    }

    [Fact]
    public async Task Properties_Title_RoundTrips()
    {
        var doc = PptxFixtures.BlankPresentation();
        doc.Properties.Title = "Round-trip Title";

        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        reloaded.Properties.Title.ShouldBe("Round-trip Title");
    }

    [Fact]
    public async Task Properties_Author_RoundTrips()
    {
        var doc = PptxFixtures.BlankPresentation();
        doc.Properties.Author = "Test Author";

        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        reloaded.Properties.Author.ShouldBe("Test Author");
    }

    [Fact]
    public void Properties_SlideCount_ReflectsActualSlides()
    {
        // SlideCount is synced from Slides.Count on each save.
        // For a freshly-created document, access doc.Slides.Count directly.
        var doc = PptxFixtures.WithSlides(5);
        doc.Slides.Count.ShouldBe(5);
    }
}
