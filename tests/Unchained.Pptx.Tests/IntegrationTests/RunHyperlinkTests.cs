using Shouldly;
using Unchained.Ooxml;
using Unchained.Ooxml.Text;
using Unchained.Pptx.Engine;
using Unchained.Pptx.Shapes;
using Unchained.Pptx.Tests.Helpers;
using Xunit;

namespace Unchained.Pptx.Tests.IntegrationTests;

/// <summary>
///     Run-level (text) hyperlinks via <see cref="RunFormat.Hyperlink" /> (M-G). External URLs and
///     internal slide jumps round-trip and preserve the run's other formatting.
/// </summary>
public sealed class RunHyperlinkTests : PptxTestBase
{
    private static Run AddLinkedRun(PresentationDocument doc, int slideIndex, string text)
    {
        var box = doc.Slides[slideIndex].Shapes.AddTextBox(
            Emu.Zero,
            Emu.Zero,
            Emu.FromInches(4),
            Emu.FromInches(1));
        var para = box.TextFrame.Paragraphs.Add();
        return para.Runs.Add(text);
    }

    [Fact]
    public async Task RunUrlHyperlink_RoundTrips()
    {
        var doc = PptxFixtures.WithSlides(1);
        var run = AddLinkedRun(doc, 0, "click me");
        run.Format.Bold = InheritableBool.True;
        run.Format.Hyperlink = RunHyperlink.ToUrl("https://example.com");
        run.Format.Hyperlink.Tooltip = "Go";

        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        var reRun = reloaded.Slides[0].Shapes
            .OfType<AutoShape>().First()
            .TextFrame.Paragraphs[0].Runs[0];

        reRun.Format.Hyperlink.ShouldNotBeNull();
        reRun.Format.Hyperlink.Url.ShouldBe("https://example.com");
        reRun.Format.Hyperlink.Tooltip.ShouldBe("Go");
        reRun.Format.Bold.ShouldBe(InheritableBool.True, "run formatting must survive alongside the link");
    }

    [Fact]
    public async Task RunSlideJumpHyperlink_RoundTripsAsSlideNumber()
    {
        var doc = PptxFixtures.WithSlides(3);
        var run = AddLinkedRun(doc, 0, "to slide 3");
        run.Format.Hyperlink = RunHyperlink.ToSlide(3);

        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        var reRun = reloaded.Slides[0].Shapes
            .OfType<AutoShape>().First()
            .TextFrame.Paragraphs[0].Runs[0];

        reRun.Format.Hyperlink.ShouldNotBeNull();
        reRun.Format.Hyperlink.TargetSlideNumber.ShouldBe(3);
        reRun.Format.Hyperlink.Url.ShouldBeNull();
    }

    [Fact]
    public async Task MultipleRunLinks_OnSameSlide_RoundTrip()
    {
        var doc = PptxFixtures.WithSlides(1);
        var box = doc.Slides[0].Shapes.AddTextBox(Emu.Zero, Emu.Zero, Emu.FromInches(4), Emu.FromInches(1));
        var para = box.TextFrame.Paragraphs.Add();
        var a = para.Runs.Add("one ");
        a.Format.Hyperlink = RunHyperlink.ToUrl("https://one.example");
        var b = para.Runs.Add("two");
        b.Format.Hyperlink = RunHyperlink.ToUrl("https://two.example");

        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        var runs = reloaded.Slides[0].Shapes
            .OfType<AutoShape>().First()
            .TextFrame.Paragraphs[0].Runs;

        runs.Count.ShouldBe(2);
        runs[0].Format.Hyperlink!.Url.ShouldBe("https://one.example");
        runs[1].Format.Hyperlink!.Url.ShouldBe("https://two.example");
    }

    [Fact]
    public async Task RunWithoutLink_StaysNull()
    {
        var doc = PptxFixtures.WithSlides(1);
        AddLinkedRun(doc, 0, "plain");

        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        var reRun = reloaded.Slides[0].Shapes
            .OfType<AutoShape>().First()
            .TextFrame.Paragraphs[0].Runs[0];

        reRun.Format.Hyperlink.ShouldBeNull();
    }
}
