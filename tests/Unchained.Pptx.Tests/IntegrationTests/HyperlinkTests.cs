using Shouldly;
using Unchained.Ooxml;
using Unchained.Pptx.Models.Shapes;
using Unchained.Pptx.Shapes;
using Unchained.Pptx.Tests.Helpers;
using Xunit;

namespace Unchained.Pptx.Tests.IntegrationTests;

/// <summary>
/// Shape click-hyperlinks: external URLs and internal slide jumps, round-trip, and the
/// document-wide hyperlink manager (M-G).
/// </summary>
public sealed class HyperlinkTests : PptxTestBase
{
    private static AutoShape AddBox(Engine.PresentationDocument doc, int slideIndex)
        => doc.Slides[slideIndex].Shapes.AddShape(
            AutoShapeType.Rectangle, Emu.Zero, Emu.Zero, Emu.FromInches(2), Emu.FromInches(1));

    [Fact]
    public async Task UrlHyperlink_RoundTrips()
    {
        var doc = PptxFixtures.WithSlides(1);
        var box = AddBox(doc, 0);
        box.ClickAction = HyperlinkAction.ToUrl("https://example.com/page", openInNewWindow: true);
        box.ClickAction.Tooltip = "Visit";

        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        var link = reloaded.Slides[0].Shapes.OfType<AutoShape>().First().ClickAction;

        link.ShouldNotBeNull();
        link.Url.ShouldBe("https://example.com/page");
        link.Tooltip.ShouldBe("Visit");
    }

    [Fact]
    public async Task SlideJumpHyperlink_RoundTripsAsSlideNumber()
    {
        var doc = PptxFixtures.WithSlides(3);
        var box = AddBox(doc, 0);
        box.ClickAction = HyperlinkAction.ToSlide(3);

        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        var link = reloaded.Slides[0].Shapes.OfType<AutoShape>().First().ClickAction;

        link.ShouldNotBeNull();
        link.TargetSlideNumber.ShouldBe(3);
        link.Url.ShouldBeNull();
    }

    [Fact]
    public async Task GetHyperlinks_EnumeratesAcrossSlides()
    {
        var doc = PptxFixtures.WithSlides(2);
        AddBox(doc, 0).ClickAction = HyperlinkAction.ToUrl("https://a.example");
        AddBox(doc, 1).ClickAction = HyperlinkAction.ToSlide(1);

        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        var links = reloaded.GetHyperlinks().ToList();

        links.Count.ShouldBe(2);
        links.ShouldContain(l => l.Action.Url == "https://a.example");
        links.ShouldContain(l => l.Action.TargetSlideNumber == 1);
    }

    [Fact]
    public async Task HyperlinkManager_RetargetUrl_Persists()
    {
        var doc = PptxFixtures.WithSlides(1);
        AddBox(doc, 0).ClickAction = HyperlinkAction.ToUrl("https://old.example");

        // Retarget in place through the manager reference.
        var link = doc.GetHyperlinks().Single();
        link.Action.Url = "https://new.example";

        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        reloaded.GetHyperlinks().Single().Action.Url.ShouldBe("https://new.example");
    }

    [Fact]
    public async Task HyperlinkManager_Remove_DropsLink()
    {
        var doc = PptxFixtures.WithSlides(1);
        AddBox(doc, 0).ClickAction = HyperlinkAction.ToUrl("https://gone.example");

        doc.GetHyperlinks().Single().Remove();

        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        reloaded.GetHyperlinks().ShouldBeEmpty();
    }
}
