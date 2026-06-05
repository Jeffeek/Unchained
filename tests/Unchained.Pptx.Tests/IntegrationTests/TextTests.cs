using Shouldly;
using Unchained.Pptx.Core;
using Unchained.Pptx.Models.Shapes;
using Unchained.Pptx.Tests.Helpers;
using Xunit;

namespace Unchained.Pptx.Tests.IntegrationTests;

public sealed class TextTests : PptxTestBase
{
    [Fact]
    public void TextFrame_PlainText_Set_Get_RoundTrips()
    {
        var doc = PptxFixtures.WithSlides(1);
        var tb = doc.Slides[0].Shapes.AddTextBox(
            Emu.Zero, Emu.Zero, Emu.FromInches(3), Emu.FromInches(1));
        tb.TextFrame.PlainText = "Hello World";
        tb.TextFrame.PlainText.ShouldBe("Hello World");
    }

    [Fact]
    public void TextFrame_SetPlainText_CreatesSingleParagraphSingleRun()
    {
        var doc = PptxFixtures.WithSlides(1);
        var tb = doc.Slides[0].Shapes.AddTextBox(
            Emu.Zero, Emu.Zero, Emu.FromInches(3), Emu.FromInches(1));
        tb.TextFrame.PlainText = "Test";
        tb.TextFrame.Paragraphs.Count.ShouldBe(1);
        tb.TextFrame.Paragraphs[0].Runs.Count.ShouldBe(1);
        tb.TextFrame.Paragraphs[0].Runs[0].Text.ShouldBe("Test");
    }

    [Fact]
    public void Paragraph_PlainText_ConcatenatesRuns()
    {
        var doc = PptxFixtures.WithSlides(1);
        var tb = doc.Slides[0].Shapes.AddTextBox(
            Emu.Zero, Emu.Zero, Emu.FromInches(3), Emu.FromInches(1));
        var para = tb.TextFrame.Paragraphs.Add();
        para.Runs.Add("Hello");
        para.Runs.Add(" World");
        para.PlainText.ShouldBe("Hello World");
    }

    [Fact]
    public void Run_FormatBold_SetAndGet()
    {
        var doc = PptxFixtures.WithSlides(1);
        var tb = doc.Slides[0].Shapes.AddTextBox(
            Emu.Zero, Emu.Zero, Emu.FromInches(3), Emu.FromInches(1));
        var para = tb.TextFrame.Paragraphs.Add();
        var run = para.Runs.Add("Bold text");
        run.Format.Bold = Core.InheritableBool.True;
        run.Format.Bold.Value.ShouldBe(true);
    }

    [Fact]
    public void Run_FontSize_SetAndGet()
    {
        var doc = PptxFixtures.WithSlides(1);
        var tb = doc.Slides[0].Shapes.AddTextBox(
            Emu.Zero, Emu.Zero, Emu.FromInches(3), Emu.FromInches(1));
        var para = tb.TextFrame.Paragraphs.Add();
        var run = para.Runs.Add("Big text");
        run.Format.FontSizePoints = 24.0;
        run.Format.FontSizePoints.ShouldBe(24.0);
    }

    [Fact]
    public void TextFrame_MultipleParagraphs_PlainText_JoinsWithNewline()
    {
        var doc = PptxFixtures.WithSlides(1);
        var tb = doc.Slides[0].Shapes.AddTextBox(
            Emu.Zero, Emu.Zero, Emu.FromInches(3), Emu.FromInches(2));
        tb.TextFrame.Paragraphs.Add("Line 1");
        tb.TextFrame.Paragraphs.Add("Line 2");
        tb.TextFrame.PlainText.ShouldBe("Line 1\nLine 2");
    }

    [Fact]
    public async Task TextContent_RoundTrips()
    {
        var doc = PptxFixtures.WithSlides(1);
        var tb = doc.Slides[0].Shapes.AddTextBox(
            Emu.Zero, Emu.Zero, Emu.FromInches(3), Emu.FromInches(1),
            "Round-trip text");

        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        var reloadedShape = reloaded.Slides[0].Shapes
            .OfType<Shapes.AutoShape>()
            .FirstOrDefault(s => s.IsTextBox);

        reloadedShape.ShouldNotBeNull();
        reloadedShape.TextFrame.PlainText.ShouldBe("Round-trip text");
    }

    [Fact]
    public void GetAllText_ReturnsConcatenatedShapeText()
    {
        var doc = PptxFixtures.WithSlides(1);
        var slide = doc.Slides[0];
        slide.Shapes.AddTextBox(Emu.Zero, Emu.Zero,
            Emu.FromInches(2), Emu.FromInches(1), "Title");
        slide.Shapes.AddTextBox(Emu.FromInches(3), Emu.Zero,
            Emu.FromInches(2), Emu.FromInches(1), "Body");

        var allText = slide.GetAllText();
        allText.ShouldContain("Title");
        allText.ShouldContain("Body");
    }
}
