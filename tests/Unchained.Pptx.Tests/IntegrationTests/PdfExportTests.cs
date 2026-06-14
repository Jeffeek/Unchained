using System.Text;
using Shouldly;
using Unchained.Ooxml;
using Unchained.Ooxml.Drawing;
using Unchained.Pptx.Core;
using Unchained.Pptx.Export;
using Unchained.Pptx.Models.Shapes;
using Unchained.Pptx.Tests.Helpers;
using Xunit;

namespace Unchained.Pptx.Tests.IntegrationTests;

public sealed class PdfExportTests : PptxTestBase
{
    // ── PDF header / validity ─────────────────────────────────────────────────

    [Fact]
    public async Task ExportPdf_OutputStartsWithPdfHeader()
    {
        var doc = PptxFixtures.WithSlides(1);
        using var ms = new MemoryStream();
        await Processor.SaveAsPdfAsync(doc, ms);
        var bytes = ms.ToArray();
        bytes[0].ShouldBe((byte)'%');
        bytes[1].ShouldBe((byte)'P');
        bytes[2].ShouldBe((byte)'D');
        bytes[3].ShouldBe((byte)'F');
    }

    [Fact]
    public async Task ExportPdf_OutputContainsPdfVersion()
    {
        var doc = PptxFixtures.WithSlides(1);
        using var ms = new MemoryStream();
        await Processor.SaveAsPdfAsync(doc, ms);
        var text = Encoding.Latin1.GetString(ms.ToArray());
        text.ShouldContain("%PDF-1.7");
    }

    [Fact]
    public async Task ExportPdf_OutputEndsWithEof()
    {
        var doc = PptxFixtures.WithSlides(1);
        using var ms = new MemoryStream();
        await Processor.SaveAsPdfAsync(doc, ms);
        var text = Encoding.Latin1.GetString(ms.ToArray());
        text.ShouldContain("%%EOF");
    }

    [Fact]
    public async Task ExportPdf_NonEmpty()
    {
        var doc = PptxFixtures.WithSlides(2);
        using var ms = new MemoryStream();
        await Processor.SaveAsPdfAsync(doc, ms);
        ms.Length.ShouldBeGreaterThan(200);
    }

    // ── Page count ────────────────────────────────────────────────────────────

    [Fact]
    public async Task ExportPdf_SingleSlide_OnePageObject()
    {
        var doc = PptxFixtures.WithSlides(1);
        using var ms = new MemoryStream();
        await Processor.SaveAsPdfAsync(doc, ms);
        var text = Encoding.Latin1.GetString(ms.ToArray());
        text.ShouldContain("/Type /Page ");
        // /Count 1 appears in the Pages tree
        text.ShouldContain("/Count 1");
    }

    [Fact]
    public async Task ExportPdf_ThreeSlides_ThreePageObjects()
    {
        var doc = PptxFixtures.WithSlides(3);
        using var ms = new MemoryStream();
        await Processor.SaveAsPdfAsync(doc, ms);
        var text = Encoding.Latin1.GetString(ms.ToArray());
        text.ShouldContain("/Count 3");
    }

    [Fact]
    public async Task ExportPdf_FiveSlides_FivePages()
    {
        var doc = PptxFixtures.WithSlides(5);
        using var ms = new MemoryStream();
        await Processor.SaveAsPdfAsync(doc, ms);
        var text = Encoding.Latin1.GetString(ms.ToArray());
        text.ShouldContain("/Count 5");
    }

    // ── Hidden slides ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ExportPdf_HiddenSlideExcluded_ByDefault()
    {
        var doc = PptxFixtures.WithSlides(2);
        doc.Slides[1].IsHidden = true;

        using var ms = new MemoryStream();
        await Processor.SaveAsPdfAsync(doc, ms);
        var text = Encoding.Latin1.GetString(ms.ToArray());
        text.ShouldContain("/Count 1");
    }

    [Fact]
    public async Task ExportPdf_HiddenSlideIncluded_WhenRequested()
    {
        var doc = PptxFixtures.WithSlides(2);
        doc.Slides[1].IsHidden = true;

        using var ms = new MemoryStream();
        await Processor.SaveAsPdfAsync(
            doc,
            ms,
            new PdfSaveOptions { IncludeHiddenSlides = true }
        );
        var text = Encoding.Latin1.GetString(ms.ToArray());
        text.ShouldContain("/Count 2");
    }

    // ── Page size ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task ExportPdf_CustomSlideSize_CorrectMediaBox()
    {
        // Use a 10 × 7.5 inch slide = 720 × 540 pt exactly
        var doc = Processor.CreateBlank(
            SlideSize.Custom(Emu.FromInches(10), Emu.FromInches(7.5))
        );
        doc.Slides.AddBlank(doc.Masters[0].Layouts[0]);

        using var ms = new MemoryStream();
        await Processor.SaveAsPdfAsync(doc, ms);
        var text = Encoding.Latin1.GetString(ms.ToArray());
        text.ShouldContain("/MediaBox");
        // 10 in = 720 pt, 7.5 in = 540 pt
        text.ShouldContain("720");
        text.ShouldContain("540");
    }

    // ── Text presence ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ExportPdf_TextShape_TextAppearsInOutput()
    {
        var doc = PptxFixtures.WithSlides(1);
        doc.Slides[0]
            .Shapes.AddTextBox(
                Emu.FromInches(1),
                Emu.FromInches(1),
                Emu.FromInches(4),
                Emu.FromInches(2),
                "Hello PDF World"
            );

        using var ms = new MemoryStream();
        await Processor.SaveAsPdfAsync(doc, ms);
        var text = Encoding.Latin1.GetString(ms.ToArray());
        text.ShouldContain("Hello PDF World");
    }

    [Fact]
    public async Task ExportPdf_MultipleTextShapes_AllTextPresent()
    {
        var doc = PptxFixtures.WithSlides(1);
        doc.Slides[0]
            .Shapes.AddTextBox(
                Emu.FromInches(0.5),
                Emu.FromInches(0.5),
                Emu.FromInches(3),
                Emu.FromInches(1),
                "Title text"
            );
        doc.Slides[0]
            .Shapes.AddTextBox(
                Emu.FromInches(0.5),
                Emu.FromInches(2),
                Emu.FromInches(5),
                Emu.FromInches(3),
                "Body content here"
            );

        using var ms = new MemoryStream();
        await Processor.SaveAsPdfAsync(doc, ms);
        var text = Encoding.Latin1.GetString(ms.ToArray());
        text.ShouldContain("Title text");
        text.ShouldContain("Body content here");
    }

    // ── Shapes ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ExportPdf_ShapeWithSolidFill_ContainsRgOperator()
    {
        var doc = PptxFixtures.WithSlides(1);
        var shape = doc.Slides[0]
            .Shapes.AddShape(
                AutoShapeType.Rectangle,
                Emu.FromInches(1),
                Emu.FromInches(1),
                Emu.FromInches(3),
                Emu.FromInches(2)
            );
        shape.Fill.SetSolid(ColorSpec.FromRgb(0, 112, 192));

        using var ms = new MemoryStream();
        await Processor.SaveAsPdfAsync(doc, ms);
        var text = Encoding.Latin1.GetString(ms.ToArray());
        // PDF solid fill uses 'rg' operator
        text.ShouldContain("rg");
    }

    // ── Options ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task ExportPdf_DefaultOptions_NoException()
    {
        var doc = PptxFixtures.WithSlides(2);
        using var ms = new MemoryStream();
        await Should.NotThrowAsync(() =>
            Processor.SaveAsPdfAsync(doc, ms, PdfSaveOptions.Default)
        );
    }

    [Fact]
    public async Task ExportPdf_EmptyPresentation_ValidPdf()
    {
        var doc = PptxFixtures.BlankPresentation();
        using var ms = new MemoryStream();
        await Processor.SaveAsPdfAsync(doc, ms);
        var text = Encoding.Latin1.GetString(ms.ToArray());
        text.ShouldContain("%PDF-1.7");
        text.ShouldContain("/Count 0");
    }
}
