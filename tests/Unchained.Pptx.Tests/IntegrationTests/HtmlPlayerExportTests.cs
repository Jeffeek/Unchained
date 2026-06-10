using System.Text;
using Shouldly;
using Unchained.Ooxml;
using Unchained.Pptx.Export;
using Unchained.Pptx.Tests.Helpers;
using Xunit;

namespace Unchained.Pptx.Tests.IntegrationTests;

/// <summary>
/// Single-file HTML5 player export (M-H): one navigable document containing all slides.
/// </summary>
public sealed class HtmlPlayerExportTests : PptxTestBase
{
    private static string Html(byte[] bytes) => Encoding.UTF8.GetString(bytes);

    [Fact]
    public async Task ExportHtmlPlayer_ProducesSingleDocument()
    {
        var doc = PptxFixtures.WithSlides(3);
        var bytes = await Processor.ExportHtmlPlayerAsync(doc);
        var html = Html(bytes);

        html.ShouldContain("<!DOCTYPE html>");
        // Exactly one document, three slide pages.
        System.Text.RegularExpressions.Regex.Matches(html, "<!DOCTYPE html>").Count.ShouldBe(1);
        System.Text.RegularExpressions.Regex.Matches(html, "class=\"slide-page\"").Count.ShouldBe(3);
    }

    [Fact]
    public async Task ExportHtmlPlayer_IncludesNavigationAndCounter()
    {
        var doc = PptxFixtures.WithSlides(2);
        var html = Html(await Processor.ExportHtmlPlayerAsync(doc));

        html.ShouldContain("id=\"next\"");
        html.ShouldContain("id=\"prev\"");
        html.ShouldContain("id=\"counter\"");
        html.ShouldContain("1 / 2");
        html.ShouldContain("addEventListener('keydown'");
    }

    [Fact]
    public async Task ExportHtmlPlayer_TextContentAppears()
    {
        var doc = PptxFixtures.WithSlides(1);
        doc.Slides[0].Shapes.AddTextBox(Emu.Zero, Emu.Zero, Emu.FromInches(4), Emu.FromInches(1), "PlayerHello");

        var html = Html(await Processor.ExportHtmlPlayerAsync(doc));
        html.ShouldContain("PlayerHello");
    }

    [Fact]
    public async Task ExportHtmlPlayer_HiddenSlidesExcludedByDefault()
    {
        var doc = PptxFixtures.WithSlides(3);
        doc.Slides[1].IsHidden = true;

        var html = Html(await Processor.ExportHtmlPlayerAsync(doc));
        System.Text.RegularExpressions.Regex.Matches(html, "class=\"slide-page\"").Count.ShouldBe(2);

        var withHidden = Html(await Processor.ExportHtmlPlayerAsync(
            doc, new HtmlPlayerSaveOptions { IncludeHiddenSlides = true }));
        System.Text.RegularExpressions.Regex.Matches(withHidden, "class=\"slide-page\"").Count.ShouldBe(3);
    }

    [Fact]
    public async Task ExportHtmlPlayer_CounterHiddenWhenDisabled()
    {
        var doc = PptxFixtures.WithSlides(2);
        var html = Html(await Processor.ExportHtmlPlayerAsync(
            doc, new HtmlPlayerSaveOptions { ShowSlideCounter = false }));
        html.ShouldNotContain("id=\"counter\"");
    }

    [Fact]
    public async Task SaveAsHtmlPlayer_WritesFile()
    {
        var doc = PptxFixtures.WithSlides(2);
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".html");
        try
        {
            await Processor.SaveAsHtmlPlayerAsync(doc, path, new HtmlPlayerSaveOptions { Title = "My Deck" });
            File.Exists(path).ShouldBeTrue();
            var html = await File.ReadAllTextAsync(path);
            html.ShouldContain("<title>My Deck</title>");
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }
}
