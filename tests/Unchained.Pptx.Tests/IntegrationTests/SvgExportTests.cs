using System.Text;
using Shouldly;
using Unchained.Ooxml;
using Unchained.Ooxml.Drawing;
using Unchained.Pptx.Engine;
using Unchained.Pptx.Export;
using Unchained.Pptx.Models.Shapes;
using Unchained.Pptx.Tests.Helpers;
using Xunit;

namespace Unchained.Pptx.Tests.IntegrationTests;

public sealed class SvgExportTests : PptxTestBase
{
    private static PresentationProcessor Processor() => new();

    // ── Output validity ───────────────────────────────────────────────────────

    [Fact]
    public async Task ExportAsSvg_SingleSlide_ReturnsSvgBytes()
    {
        var doc = PptxFixtures.WithSlides(1);
        var svgs = await Processor().ExportAsSvgAsync(doc);
        svgs.Length.ShouldBe(1);
        svgs[0].Length.ShouldBeGreaterThan(50);
    }

    [Fact]
    public async Task ExportAsSvg_ThreeSlides_ReturnsThreeSvgs()
    {
        var doc = PptxFixtures.WithSlides(3);
        var svgs = await Processor().ExportAsSvgAsync(doc);
        svgs.Length.ShouldBe(3);
    }

    [Fact]
    public async Task ExportAsSvg_OutputContainsSvgRootElement()
    {
        var doc = PptxFixtures.WithSlides(1);
        var svgs = await Processor().ExportAsSvgAsync(doc);
        var text = Encoding.UTF8.GetString(svgs[0]);
        text.ShouldContain("<svg ");
        text.ShouldContain("</svg>");
    }

    [Fact]
    public async Task ExportAsSvg_OutputContainsXmlDeclaration()
    {
        var doc = PptxFixtures.WithSlides(1);
        var svgs = await Processor().ExportAsSvgAsync(doc);
        var text = Encoding.UTF8.GetString(svgs[0]);
        text.ShouldContain("<?xml");
    }

    [Fact]
    public async Task ExportAsSvg_OutputContainsViewBox()
    {
        var doc = PptxFixtures.WithSlides(1);
        var svgs = await Processor().ExportAsSvgAsync(doc);
        var text = Encoding.UTF8.GetString(svgs[0]);
        text.ShouldContain("viewBox=");
    }

    [Fact]
    public async Task ExportAsSvg_TextShape_TextAppearsInOutput()
    {
        var doc = PptxFixtures.WithSlides(1);
        doc.Slides[0].Shapes.AddTextBox(
            Emu.FromInches(1),
            Emu.FromInches(1),
            Emu.FromInches(4),
            Emu.FromInches(2),
            "Hello SVG");

        var svgs = await Processor().ExportAsSvgAsync(doc);
        var text = Encoding.UTF8.GetString(svgs[0]);
        text.ShouldContain("Hello SVG");
    }

    [Fact]
    public async Task ExportAsSvg_ShapeWithFill_ContainsFillAttribute()
    {
        var doc = PptxFixtures.WithSlides(1);
        var shape = doc.Slides[0].Shapes.AddShape(AutoShapeType.Rectangle,
            Emu.FromInches(1),
            Emu.FromInches(1),
            Emu.FromInches(3),
            Emu.FromInches(2));
        shape.Fill.SetSolid(ColorSpec.FromRgb(255, 0, 0));

        var svgs = await Processor().ExportAsSvgAsync(doc);
        var text = Encoding.UTF8.GetString(svgs[0]);
        text.ShouldContain("#FF0000");
    }

    [Fact]
    public async Task ExportAsSvg_HiddenSlide_ExcludedByDefault()
    {
        var doc = PptxFixtures.WithSlides(2);
        doc.Slides[1].IsHidden = true;

        var svgs = await Processor().ExportAsSvgAsync(doc);
        svgs.Length.ShouldBe(1);
    }

    [Fact]
    public async Task ExportAsSvg_HiddenSlide_IncludedWhenRequested()
    {
        var doc = PptxFixtures.WithSlides(2);
        doc.Slides[1].IsHidden = true;

        var svgs = await Processor().ExportAsSvgAsync(doc,
            new SvgSaveOptions { IncludeHiddenSlides = true });
        svgs.Length.ShouldBe(2);
    }

    [Fact]
    public async Task ExportAsSvg_Responsive_NoWidthHeightAttributes()
    {
        var doc = PptxFixtures.WithSlides(1);
        var svgs = await Processor().ExportAsSvgAsync(doc,
            new SvgSaveOptions { Responsive = true });
        var text = Encoding.UTF8.GetString(svgs[0]);
        // Responsive SVG: <svg> root has no fixed width/height, only viewBox
        var svgLine = text.Split('\n').First(l => l.TrimStart().StartsWith("<svg "));
        svgLine.ShouldNotContain("width=\"");
        svgLine.ShouldNotContain("height=\"");
        text.ShouldContain("viewBox=");
    }

    [Fact]
    public async Task ExportSlideAsSvg_SingleSlide_MatchesBulkExport()
    {
        var doc = PptxFixtures.WithSlides(1);
        var p = Processor();

        var single = await p.ExportSlideAsSvgAsync(doc.Slides[0], doc.SlideSize);
        var bulk = await p.ExportAsSvgAsync(doc);

        single.Length.ShouldBe(bulk[0].Length);
    }

    [Fact]
    public async Task ExportAsSvg_EmptyPresentation_ReturnsEmptyArray()
    {
        var doc = Processor().CreateBlank();
        var svgs = await Processor().ExportAsSvgAsync(doc);
        svgs.Length.ShouldBe(0);
    }
}
