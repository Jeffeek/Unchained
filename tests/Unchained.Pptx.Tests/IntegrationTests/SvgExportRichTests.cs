using System.Text;
using Shouldly;
using Unchained.Drawing;
using Unchained.Drawing.Encoders;
using Unchained.Ooxml;
using Unchained.Ooxml.Drawing;
using Unchained.Ooxml.Text;
using Unchained.Pptx.Engine;
using Unchained.Pptx.Export;
using Unchained.Pptx.Models.Shapes;
using Unchained.Pptx.Tests.Helpers;
using Xunit;

namespace Unchained.Pptx.Tests.IntegrationTests;

/// <summary>
///     Exercises the less-common <see cref="PptxToSvgWriter" /> branches: slide background fill,
///     style-driven fill, stroke, picture embedding, responsive sizing, and per-paragraph
///     alignment anchoring.
/// </summary>
public sealed class SvgExportRichTests : PptxTestBase
{
    private static byte[] SmallPng()
    {
        var buffer = new RasterBuffer(4, 4);
        buffer.Clear(10, 200, 30);
        return PngEncoder.Encode(buffer);
    }

    private async Task<string> ExportFirstAsync(
        PresentationDocument doc,
        SvgSaveOptions? options = null
    )
    {
        var svgs = options is null
            ? await Processor.ExportAsSvgAsync(doc)
            : await Processor.ExportAsSvgAsync(doc, options);
        return Encoding.UTF8.GetString(svgs[0]);
    }

    [Fact]
    public async Task SolidBackground_AppearsAsFilledRect()
    {
        var doc = PptxFixtures.WithSlides(1);
        doc.Slides[0].Background.Fill.SetSolid(ColorSpec.FromRgb(0x33, 0x66, 0x99));

        var svg = await ExportFirstAsync(doc);
        svg.ShouldContain("#336699");
    }

    [Fact]
    public async Task ShapeWithStroke_EmitsStrokeAttribute()
    {
        var doc = PptxFixtures.WithSlides(1);
        var shape = doc.Slides[0]
            .Shapes.AddShape(AutoShapeType.Rectangle, Emu.FromInches(1), Emu.FromInches(1), Emu.FromInches(2), Emu.FromInches(1));
        shape.Line.SetSolid(ColorSpec.FromRgb(0, 0, 0), 3);

        var svg = await ExportFirstAsync(doc);
        svg.ShouldContain("stroke=");
        svg.ShouldContain("stroke-width=");
    }

    [Fact]
    public async Task NoFillShape_EmitsFillNone()
    {
        var doc = PptxFixtures.WithSlides(1);
        var shape = doc.Slides[0]
            .Shapes.AddShape(AutoShapeType.Rectangle, Emu.Zero, Emu.Zero, Emu.FromInches(2), Emu.FromInches(1));
        shape.Fill.SetNone();

        var svg = await ExportFirstAsync(doc);
        svg.ShouldContain("fill=\"none\"");
    }

    [Fact]
    public async Task EmbeddedPicture_EmitsImageElementWithDataUri()
    {
        var doc = PptxFixtures.WithSlides(1);
        var image = doc.Media.AddImage(SmallPng(), "image/png");
        doc.Slides[0]
            .Shapes.AddPicture(image, Emu.FromInches(1), Emu.FromInches(1), Emu.FromInches(2), Emu.FromInches(2));

        var svg = await ExportFirstAsync(doc);
        svg.ShouldContain("<image ");
        svg.ShouldContain("data:image/png;base64,");
    }

    [Fact]
    public async Task Picture_NotEmbedded_OmitsImageElement()
    {
        var doc = PptxFixtures.WithSlides(1);
        var image = doc.Media.AddImage(SmallPng(), "image/png");
        doc.Slides[0]
            .Shapes.AddPicture(image, Emu.FromInches(1), Emu.FromInches(1), Emu.FromInches(2), Emu.FromInches(2));

        var svg = await ExportFirstAsync(doc, new SvgSaveOptions { EmbedImages = false });
        svg.ShouldNotContain("<image ");
    }

    [Fact]
    public async Task CenteredAndRightText_EmitsTextAnchors()
    {
        var doc = PptxFixtures.WithSlides(1);
        var tb = doc.Slides[0]
            .Shapes.AddTextBox(Emu.Zero, Emu.Zero, Emu.FromInches(4), Emu.FromInches(2));
        tb.TextFrame.Paragraphs.Add("Centered").Alignment = TextAlignment.Center;
        tb.TextFrame.Paragraphs.Add("Righted").Alignment = TextAlignment.Right;

        var svg = await ExportFirstAsync(doc);
        svg.ShouldContain("text-anchor=\"middle\"");
        svg.ShouldContain("text-anchor=\"end\"");
    }

    [Fact]
    public async Task BoldItalicColoredRun_EmitsRunAttributes()
    {
        var doc = PptxFixtures.WithSlides(1);
        var tb = doc.Slides[0]
            .Shapes.AddTextBox(Emu.Zero, Emu.Zero, Emu.FromInches(4), Emu.FromInches(2));
        var run = tb.TextFrame.Paragraphs.Add().Runs.Add("Styled");
        run.Format.Bold = InheritableBool.True;
        run.Format.Italic = InheritableBool.True;
        run.Format.Fill = new FillFormat();
        run.Format.Fill.SetSolid(ColorSpec.FromRgb(0xAB, 0xCD, 0xEF));

        var svg = await ExportFirstAsync(doc);
        svg.ShouldContain("font-weight=\"bold\"");
        svg.ShouldContain("font-style=\"italic\"");
        svg.ShouldContain("#ABCDEF");
    }

    [Fact]
    public async Task EmptyParagraph_AdvancesCursorWithoutText()
    {
        var doc = PptxFixtures.WithSlides(1);
        var tb = doc.Slides[0]
            .Shapes.AddTextBox(Emu.Zero, Emu.Zero, Emu.FromInches(4), Emu.FromInches(3));
        tb.TextFrame.Paragraphs.Add(); // empty
        tb.TextFrame.Paragraphs.Add("After empty");

        var svg = await ExportFirstAsync(doc);
        svg.ShouldContain("After empty");
    }
}
