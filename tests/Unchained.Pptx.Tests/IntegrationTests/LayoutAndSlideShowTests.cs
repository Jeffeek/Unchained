using Shouldly;
using Unchained.Ooxml;
using Unchained.Pptx.Models.Shapes;
using Unchained.Pptx.Models.Themes;
using Unchained.Pptx.Slides;
using Unchained.Pptx.Tests.Helpers;
using Xunit;

namespace Unchained.Pptx.Tests.IntegrationTests;

/// <summary>
/// Programmatic slide-layout creation and presentation-level slide-show settings (M-G).
/// </summary>
public sealed class LayoutAndSlideShowTests : PptxTestBase
{
    // ── Layout creation ────────────────────────────────────────────────────────

    [Fact]
    public void AddLayout_AttachesToMaster()
    {
        var doc = PptxFixtures.BlankPresentation();
        var master = doc.Masters[0];
        var before = master.Layouts.Count;

        var layout = master.Layouts.AddLayout("My Custom Layout", LayoutType.TitleOnly);

        master.Layouts.Count.ShouldBe(before + 1);
        layout.Master.ShouldBe(master);
        layout.Name.ShouldBe("My Custom Layout");
        layout.LayoutType.ShouldBe(LayoutType.TitleOnly);
    }

    [Fact]
    public async Task AddLayout_RoundTripsAsNewPart()
    {
        var doc = PptxFixtures.BlankPresentation();
        var master = doc.Masters[0];
        var layout = master.Layouts.AddLayout("Created Layout", LayoutType.Blank);
        layout.Shapes.AddTextBox(Emu.Zero, Emu.Zero, Emu.FromInches(3), Emu.FromInches(1), "placeholder");

        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        var found = reloaded.Masters[0].Layouts.FindByName("Created Layout");

        found.ShouldNotBeNull("the new layout must round-trip as its own part");
        found.LayoutType.ShouldBe(LayoutType.Blank);
    }

    [Fact]
    public async Task AddLayout_UsableBySlide()
    {
        var doc = PptxFixtures.BlankPresentation();
        var master = doc.Masters[0];
        var layout = master.Layouts.AddLayout("Slide Base", LayoutType.TitleOnly);

        doc.Slides.AddBlank(layout);

        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        reloaded.Slides.Count.ShouldBe(1);
        reloaded.Slides[0].Layout.ShouldNotBeNull();
    }

    [Fact]
    public void CloneLayout_CopiesShapesAndType()
    {
        var doc = PptxFixtures.BlankPresentation();
        var master = doc.Masters[0];
        var source = master.Layouts[0];
        var sourceShapeCount = source.Shapes.Count;

        var clone = master.Layouts.AddClone(source, "Cloned");

        clone.Name.ShouldBe("Cloned");
        clone.LayoutType.ShouldBe(source.LayoutType);
        clone.Shapes.Count.ShouldBe(sourceShapeCount);
        clone.Master.ShouldBe(master);
    }

    // ── Slide-show settings ──────────────────────────────────────────────────────

    [Fact]
    public void NewDocument_HasDefaultSlideShowSettings()
    {
        var doc = PptxFixtures.BlankPresentation();
        doc.SlideShow.ShowType.ShouldBe(SlideShowType.Presenter);
        doc.SlideShow.Loop.ShouldBeFalse();
    }

    [Fact]
    public async Task SlideShowSettings_RoundTrip()
    {
        var doc = PptxFixtures.WithSlides(5);
        doc.SlideShow.ShowType = SlideShowType.Kiosk;
        doc.SlideShow.Loop = true;
        doc.SlideShow.ShowWithoutNarration = true;
        doc.SlideShow.RangeStart = 2;
        doc.SlideShow.RangeEnd = 4;
        doc.SlideShow.PenColorHex = "FF0000";

        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        var show = reloaded.SlideShow;

        show.ShowType.ShouldBe(SlideShowType.Kiosk);
        show.Loop.ShouldBeTrue();
        show.ShowWithoutNarration.ShouldBeTrue();
        show.ShowWithoutAnimation.ShouldBeFalse();
        show.RangeStart.ShouldBe(2);
        show.RangeEnd.ShouldBe(4);
        show.PenColorHex.ShouldBe("FF0000");
    }

    [Fact]
    public async Task SlideShowSettings_DefaultNotWritten()
    {
        var doc = PptxFixtures.WithSlides(1);
        // No settings changed → presProps part should not be emitted.
        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        reloaded.SlideShow.ShowType.ShouldBe(SlideShowType.Presenter);
        reloaded.SlideShow.Loop.ShouldBeFalse();
    }

    [Fact]
    public async Task SlideShowSettings_BrowsedType_RoundTrips()
    {
        var doc = PptxFixtures.WithSlides(2);
        doc.SlideShow.ShowType = SlideShowType.Browsed;

        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        reloaded.SlideShow.ShowType.ShouldBe(SlideShowType.Browsed);
    }
}
