using Shouldly;
using Unchained.Ooxml;
using Unchained.Pptx.Core;
using Unchained.Pptx.Tests.Helpers;
using Xunit;

namespace Unchained.Pptx.Tests.IntegrationTests;

public sealed class DocumentLifecycleTests : PptxTestBase
{
    [Fact]
    public void CreateBlank_HasNoSlides()
    {
        var doc = Processor.CreateBlank();
        doc.Slides.Count.ShouldBe(0);
    }

    [Fact]
    public void CreateBlank_HasOneMaster()
    {
        var doc = Processor.CreateBlank();
        doc.Masters.Count.ShouldBe(1);
    }

    [Fact]
    public void CreateBlank_DefaultSizeIsWidescreen()
    {
        var doc = Processor.CreateBlank();
        doc.SlideSize.ShouldBe(SlideSize.Widescreen);
    }

    [Fact]
    public void CreateBlank_CustomSize_IsPreserved()
    {
        var size = SlideSize.Standard;
        var doc = Processor.CreateBlank(size);
        doc.SlideSize.ShouldBe(size);
    }

    [Fact]
    public async Task SaveAndLoad_EmptyPresentation_RoundTrips()
    {
        var doc = Processor.CreateBlank();
        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        reloaded.Slides.Count.ShouldBe(0);
    }

    [Fact]
    public async Task SaveAndLoad_PreservesSlideCount()
    {
        var doc = PptxFixtures.WithSlides(3);
        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        reloaded.Slides.Count.ShouldBe(3);
    }

    [Fact]
    public async Task SaveAndLoad_PreservesSlideSize()
    {
        var doc = Processor.CreateBlank(SlideSize.Standard);
        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        reloaded.SlideSize.Width.Value.ShouldBe(SlideSize.Standard.Width.Value);
        reloaded.SlideSize.Height.Value.ShouldBe(SlideSize.Standard.Height.Value);
    }

    [Fact]
    public async Task SaveToStream_ProducesNonEmptyBytes()
    {
        var doc = Processor.CreateBlank();
        var ms = new MemoryStream();
        await Processor.SaveAsync(doc, ms);
        ms.Length.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task LoadFromStream_ParsesSuccessfully()
    {
        var doc = PptxFixtures.WithSlides(2);
        var ms = new MemoryStream();
        await Processor.SaveAsync(doc, ms);
        ms.Position = 0;

        var loaded = await Processor.LoadAsync(ms);
        loaded.Slides.Count.ShouldBe(2);
    }

    [Fact]
    public async Task MultipleRoundTrips_PreservesSlideCount()
    {
        var doc = PptxFixtures.WithSlides(5);
        var r1 = await PptxFixtures.RoundTripAsync(doc);
        var r2 = await PptxFixtures.RoundTripAsync(r1);
        r2.Slides.Count.ShouldBe(5);
    }
}
