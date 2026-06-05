using Shouldly;
using Unchained.Pptx.Tests.Helpers;
using Xunit;

namespace Unchained.Pptx.Tests.IntegrationTests;

public sealed class SlideManagementTests : PptxTestBase
{
    [Fact]
    public void AddBlank_IncreasesSlideCount()
    {
        var doc = PptxFixtures.BlankPresentation();
        var layout = doc.Masters[0].Layouts[0];
        doc.Slides.AddBlank(layout);
        doc.Slides.Count.ShouldBe(1);
    }

    [Fact]
    public void AddBlank_AssignsSlideNumber()
    {
        var doc = PptxFixtures.BlankPresentation();
        var layout = doc.Masters[0].Layouts[0];
        var slide = doc.Slides.AddBlank(layout);
        slide.SlideNumber.ShouldBe(1);
    }

    [Fact]
    public void AddMultipleSlides_NumbersAreSequential()
    {
        var doc = PptxFixtures.WithSlides(3);
        for (var i = 0; i < 3; i++)
            doc.Slides[i].SlideNumber.ShouldBe(i + 1);
    }

    [Fact]
    public void Remove_DecreasesSlideCount()
    {
        var doc = PptxFixtures.WithSlides(3);
        var slide = doc.Slides[1];
        doc.Slides.Remove(slide);
        doc.Slides.Count.ShouldBe(2);
    }

    [Fact]
    public void RemoveAt_WorksCorrectly()
    {
        var doc = PptxFixtures.WithSlides(3);
        doc.Slides.RemoveAt(0);
        doc.Slides.Count.ShouldBe(2);
        doc.Slides[0].SlideNumber.ShouldBe(1);
    }

    [Fact]
    public void MoveTo_ReordersSlides()
    {
        var doc = PptxFixtures.WithSlides(3);
        var firstId = doc.Slides[0].SlideId;
        doc.Slides.MoveTo(0, 2);
        doc.Slides[2].SlideId.ShouldBe(firstId);
    }

    [Fact]
    public void AddClone_CreatesNewSlideWithSameLayout()
    {
        var doc = PptxFixtures.WithSlides(1);
        var original = doc.Slides[0];
        var clone = doc.Slides.AddClone(original);

        doc.Slides.Count.ShouldBe(2);
        clone.SlideId.ShouldNotBe(original.SlideId);
        clone.Layout.ShouldBeSameAs(original.Layout);
    }

    [Fact]
    public void InsertBlank_InsertsAtCorrectPosition()
    {
        var doc = PptxFixtures.WithSlides(2);
        var layout = doc.Masters[0].Layouts[0];
        var inserted = doc.Slides.InsertBlank(1, layout);

        doc.Slides.Count.ShouldBe(3);
        doc.Slides[1].SlideId.ShouldBe(inserted.SlideId);
    }

    [Fact]
    public void Slide_IsHidden_DefaultIsFalse()
    {
        var doc = PptxFixtures.WithSlides(1);
        doc.Slides[0].IsHidden.ShouldBeFalse();
    }

    [Fact]
    public void Slide_SetName_IsPreserved()
    {
        var doc = PptxFixtures.WithSlides(1);
        doc.Slides[0].Name = "My Slide";
        doc.Slides[0].Name.ShouldBe("My Slide");
    }

    [Fact]
    public async Task SlideCount_PreservedAfterRoundTrip()
    {
        var doc = PptxFixtures.WithSlides(4);
        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        reloaded.Slides.Count.ShouldBe(4);
    }
}
