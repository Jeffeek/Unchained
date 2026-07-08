using Shouldly;
using Unchained.Drawing;
using Unchained.Drawing.Encoders;
using Unchained.Ooxml;
using Unchained.Ooxml.Drawing;
using Unchained.Ooxml.Text;
using Unchained.Pptx.Engine;
using Unchained.Pptx.Export;
using Unchained.Pptx.Models.Shapes;
using Unchained.Pptx.Tests.Shared;
using Xunit;

namespace Unchained.Pptx.Tests.IntegrationTests;

/// <summary>
///     Exercises the less-common <see cref="PptxToHtmlWriter" /> branches: slide background fill,
///     transparent fill, borders, picture embedding, additional CSS, and per-paragraph alignment.
/// </summary>
public sealed class HtmlExportRichTests : PptxTestBase
{
    private static byte[] SmallPng()
    {
        var buffer = new RasterBuffer(4, 4);
        buffer.Clear(20, 40, 200);
        return PngEncoder.Encode(buffer);
    }

    private async Task<string> WriteAndReadAsync(
        PresentationDocument doc,
        HtmlSaveOptions? options = null
    )
    {
        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        try
        {
            var files = options is null
                ? await Processor.SaveAsHtmlAsync(doc, dir)
                : await Processor.SaveAsHtmlAsync(doc, dir, options);
            return await File.ReadAllTextAsync(files[0], TestContext.Current.CancellationToken);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task SolidBackground_AppearsInCss()
    {
        var doc = PptxFixtures.WithSlides(1);
        doc.Slides[0].Background.Fill.SetSolid(ColorSpec.FromRgb(10, 20, 30));

        var html = await WriteAndReadAsync(doc);
        html.ShouldContain("background:rgb(10,20,30)");
    }

    [Fact]
    public async Task NoFillShape_EmitsTransparentBackground()
    {
        var doc = PptxFixtures.WithSlides(1);
        var shape = doc.Slides[0]
            .Shapes.AddShape(AutoShapeType.Rectangle, Emu.Zero, Emu.Zero, Emu.FromInches(2), Emu.FromInches(1));
        shape.Fill.SetNone();

        var html = await WriteAndReadAsync(doc);
        html.ShouldContain("background:transparent");
    }

    [Fact]
    public async Task ShapeWithBorder_EmitsBorderStyle()
    {
        var doc = PptxFixtures.WithSlides(1);
        var shape = doc.Slides[0]
            .Shapes.AddShape(AutoShapeType.Rectangle, Emu.Zero, Emu.Zero, Emu.FromInches(2), Emu.FromInches(1));
        shape.Line.SetSolid(ColorSpec.FromRgb(0, 0, 0), 2);

        var html = await WriteAndReadAsync(doc);
        html.ShouldContain("border:2.0px solid rgb(0,0,0)");
    }

    [Fact]
    public async Task EmbeddedPicture_EmitsImgWithDataUri()
    {
        var doc = PptxFixtures.WithSlides(1);
        var image = doc.Media.AddImage(SmallPng(), "image/png");
        doc.Slides[0]
            .Shapes.AddPicture(image, Emu.FromInches(1), Emu.FromInches(1), Emu.FromInches(2), Emu.FromInches(2));

        var html = await WriteAndReadAsync(doc);
        html.ShouldContain("<img");
        html.ShouldContain("data:image/png;base64,");
    }

    [Fact]
    public async Task AdditionalCss_AppearsInOutput()
    {
        var doc = PptxFixtures.WithSlides(1);
        var html = await WriteAndReadAsync(doc, new HtmlSaveOptions { AdditionalCss = ".custom{color:hotpink}" });
        html.ShouldContain(".custom{color:hotpink}");
    }

    [Fact]
    public async Task ParagraphAlignment_EmitsTextAlign()
    {
        var doc = PptxFixtures.WithSlides(1);
        var tb = doc.Slides[0]
            .Shapes.AddTextBox(Emu.Zero, Emu.Zero, Emu.FromInches(4), Emu.FromInches(2));
        tb.TextFrame.Paragraphs.Add("c").Alignment = TextAlignment.Center;
        tb.TextFrame.Paragraphs.Add("j").Alignment = TextAlignment.Justify;

        var html = await WriteAndReadAsync(doc);
        html.ShouldContain("text-align:center");
        html.ShouldContain("text-align:justify");
    }

    [Fact]
    public async Task BoldItalicColoredRun_EmitsSpanStyles()
    {
        var doc = PptxFixtures.WithSlides(1);
        var tb = doc.Slides[0]
            .Shapes.AddTextBox(Emu.Zero, Emu.Zero, Emu.FromInches(4), Emu.FromInches(2));
        var run = tb.TextFrame.Paragraphs.Add().Runs.Add("Styled");
        run.Format.Bold = InheritableBool.True;
        run.Format.Italic = InheritableBool.True;
        run.Format.Fill = new FillFormat();
        run.Format.Fill.SetSolid(ColorSpec.FromRgb(1, 2, 3));

        var html = await WriteAndReadAsync(doc);
        html.ShouldContain("font-weight:bold");
        html.ShouldContain("font-style:italic");
        html.ShouldContain("color:rgb(1,2,3)");
    }

    [Fact]
    public async Task SlideName_AppearsInTitle()
    {
        var doc = PptxFixtures.WithSlides(1);
        doc.Slides[0].Name = "Intro Slide";

        var html = await WriteAndReadAsync(doc);
        html.ShouldContain("<title>Intro Slide</title>");
    }
}
