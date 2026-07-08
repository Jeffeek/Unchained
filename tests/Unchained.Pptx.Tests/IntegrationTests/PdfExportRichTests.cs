using System.Text;
using Shouldly;
using Unchained.Drawing;
using Unchained.Drawing.Encoders;
using Unchained.Ooxml;
using Unchained.Ooxml.Drawing;
using Unchained.Ooxml.Media;
using Unchained.Pptx.Engine;
using Unchained.Pptx.Export;
using Unchained.Pptx.Models.Shapes;
using Unchained.Pptx.Tests.Shared;
using Xunit;

namespace Unchained.Pptx.Tests.IntegrationTests;

/// <summary>
///     Exercises the less-common <see cref="PptxToPdfWriter" /> branches: slide background, table
///     cells with fills/borders, embedded fonts, picture XObjects, and tagged structure elements.
/// </summary>
public sealed class PdfExportRichTests : PptxTestBase
{
    private static byte[] SmallPng()
    {
        var buffer = new RasterBuffer(4, 4);
        buffer.Clear(200, 50, 50);
        return PngEncoder.Encode(buffer);
    }

    private async Task<string> ExportTextAsync(
        PresentationDocument doc,
        PdfSaveOptions? options = null
    )
    {
        using var ms = new MemoryStream();
        if (options is null)
            await Processor.SaveAsPdfAsync(doc, ms);
        else
            await Processor.SaveAsPdfAsync(doc, ms, options);
        return Encoding.Latin1.GetString(ms.ToArray());
    }

    [Fact]
    public async Task SolidBackground_EmitsFillOperator()
    {
        var doc = PptxFixtures.WithSlides(1);
        doc.Slides[0].Background.Fill.SetSolid(ColorSpec.FromRgb(0x20, 0x40, 0x60));

        var pdf = await ExportTextAsync(doc);
        pdf.ShouldContain("rg");
        pdf.ShouldContain("re f");
    }

    [Fact]
    public async Task TableShape_RendersCellsAndText()
    {
        var doc = PptxFixtures.WithSlides(1);
        var table = doc.Slides[0]
            .Shapes.AddTable(
                Emu.FromInches(1),
                Emu.FromInches(1),
                [Emu.FromInches(2), Emu.FromInches(2)],
                [Emu.FromInches(1), Emu.FromInches(1)]
            );
        table.Grid[0, 0].TextFrame.Paragraphs.Add("CellA");
        table.Grid[0, 0].Fill.SetSolid(ColorSpec.FromRgb(0xEE, 0xEE, 0xEE));
        table.Grid[1, 1].TextFrame.Paragraphs.Add("CellB");

        var pdf = await ExportTextAsync(doc);
        pdf.ShouldContain("CellA");
        pdf.ShouldContain("CellB");
    }

    [Fact]
    public async Task ShapeWithStroke_EmitsStrokeOperators()
    {
        var doc = PptxFixtures.WithSlides(1);
        var shape = doc.Slides[0]
            .Shapes.AddShape(AutoShapeType.Rectangle, Emu.FromInches(1), Emu.FromInches(1), Emu.FromInches(3), Emu.FromInches(2));
        shape.Fill.SetSolid(ColorSpec.FromRgb(200, 200, 200));
        shape.Line.SetSolid(ColorSpec.FromRgb(0, 0, 0), 2);

        var pdf = await ExportTextAsync(doc);
        pdf.ShouldContain("RG");
        pdf.ShouldContain("re S");
    }

    [Fact]
    public async Task EmbeddedFontRun_EmitsFontFile2()
    {
        var doc = PptxFixtures.WithSlides(1);
        // Inject fake font bytes (not a valid font file). The PDF writer emits /FontFile2
        // regardless of validity — this test exercises the control-flow path.
        var fontBytes = new byte[] { 0x00, 0x01, 0x00, 0x00, 0x10, 0x20, 0x30, 0x40 };
        doc.Media.AddFont(
            new EmbeddedFont
            {
                Typeface = "CustomFace",
                Style = EmbeddedFontStyle.Regular,
                Data = fontBytes
            }
        );

        var tb = doc.Slides[0]
            .Shapes.AddTextBox(Emu.FromInches(1), Emu.FromInches(1), Emu.FromInches(4), Emu.FromInches(1));
        var run = tb.TextFrame.Paragraphs.Add().Runs.Add("Embedded");
        run.Format.LatinFont = "CustomFace";

        var pdf = await ExportTextAsync(doc);
        pdf.ShouldContain("/FontFile2");
        pdf.ShouldContain("/TrueType");
    }

    [Fact]
    public async Task BoldRun_StillEmbedsFallbackFont()
    {
        var doc = PptxFixtures.WithSlides(1);
        var tb = doc.Slides[0]
            .Shapes.AddTextBox(Emu.FromInches(1), Emu.FromInches(1), Emu.FromInches(4), Emu.FromInches(1));
        var run = tb.TextFrame.Paragraphs.Add().Runs.Add("Bolded");
        run.Format.Bold = InheritableBool.True;

        var pdf = await ExportTextAsync(doc);
        pdf.ShouldContain("/Helvetica");
        pdf.ShouldContain("Bolded");
    }

    [Fact]
    public async Task EmbeddedJpegPicture_EmitsImageXObject()
    {
        var doc = PptxFixtures.WithSlides(1);
        // A minimal JPEG-typed image with a PartUri so the PDF writer emits an XObject.
        var image = new EmbeddedImage("image/jpeg", new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0, 0 })
        {
            PartUri = "/ppt/media/image1.jpg",
            PixelWidth = 8,
            PixelHeight = 8
        };
        doc.Media.AddImage(image);
        doc.Slides[0]
            .Shapes.AddPicture(image, Emu.FromInches(1), Emu.FromInches(1), Emu.FromInches(2), Emu.FromInches(2));

        var pdf = await ExportTextAsync(doc);
        pdf.ShouldContain("/XObject");
        pdf.ShouldContain("/DCTDecode");
    }

    [Fact]
    public async Task EmbeddedPngPicture_EmitsWhitePlaceholderImage()
    {
        var doc = PptxFixtures.WithSlides(1);
        var image = new EmbeddedImage("image/png", SmallPng())
        {
            PartUri = "/ppt/media/image2.png",
            PixelWidth = 4,
            PixelHeight = 4
        };
        doc.Media.AddImage(image);
        doc.Slides[0]
            .Shapes.AddPicture(image, Emu.FromInches(1), Emu.FromInches(1), Emu.FromInches(2), Emu.FromInches(2));

        var pdf = await ExportTextAsync(doc);
        // Non-JPEG images are written as raw RGB XObjects (no DCTDecode filter).
        pdf.ShouldContain("/XObject");
        pdf.ShouldContain("/Subtype /Image");
    }

    [Fact]
    public async Task DecorativePicture_EmitsArtifactMarker()
    {
        var doc = PptxFixtures.WithSlides(1);
        var image = new EmbeddedImage("image/jpeg", new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 })
        {
            PartUri = "/ppt/media/dec.jpg",
            PixelWidth = 4,
            PixelHeight = 4
        };
        doc.Media.AddImage(image);
        var pic = doc.Slides[0]
            .Shapes.AddPicture(image, Emu.FromInches(1), Emu.FromInches(1), Emu.FromInches(2), Emu.FromInches(2));
        pic.IsDecorative = true;

        var pdf = await ExportTextAsync(doc);
        pdf.ShouldContain("/Artifact BMC");
    }

    [Fact]
    public async Task TaggedStructure_EmitsStructTreeRootAndFigure()
    {
        var doc = PptxFixtures.WithSlides(1);
        var image = new EmbeddedImage("image/jpeg", new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 })
        {
            PartUri = "/ppt/media/fig.jpg",
            PixelWidth = 4,
            PixelHeight = 4
        };
        doc.Media.AddImage(image);
        var pic = doc.Slides[0]
            .Shapes.AddPicture(image, Emu.FromInches(1), Emu.FromInches(1), Emu.FromInches(2), Emu.FromInches(2));
        pic.AltText = "A figure";

        var pdf = await ExportTextAsync(doc);
        pdf.ShouldContain("/StructTreeRoot");
        pdf.ShouldContain("/Figure");
        pdf.ShouldContain("/Alt (A figure)");
    }
}
