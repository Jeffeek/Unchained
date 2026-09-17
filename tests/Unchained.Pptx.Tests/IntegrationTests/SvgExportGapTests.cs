using Shouldly;
using System.Text;
using Unchained.Ooxml;
using Unchained.Ooxml.Media;
using Unchained.Pptx.Export;
using Unchained.Pptx.Tests.Shared;
using Xunit;

namespace Unchained.Pptx.Tests.IntegrationTests;

/// <summary>
///     Covers <see cref="PptxToSvgWriter" /> paths not exercised by the baseline SVG tests: the
///     font-warning-collecting <c>WriteSlide</c> overload (including nested group recursion) and the
///     shape-annotation option.
/// </summary>
public sealed class SvgExportGapTests : PptxTestBase
{
    private static void SetRunFont(Shapes.AutoShape shape, string font) =>
        shape.TextFrame.Paragraphs[0].Runs[0].Format.LatinFont = font;

    [Fact]
    public void WriteSlide_WithUnresolvedFonts_ReportsFontWarnings()
    {
        var doc = PptxFixtures.WithSlides(1);
        var slide = doc.Slides[0];

        var top = slide.Shapes.AddTextBox(Emu.FromInches(1), Emu.FromInches(1), Emu.FromInches(4), Emu.FromInches(1), "Top");
        SetRunFont(top, "ExoticFontAAA");

        var group = slide.Shapes.AddGroup();
        var nested = group.Children.AddTextBox(Emu.FromInches(1), Emu.FromInches(3), Emu.FromInches(4), Emu.FromInches(1), "Nested");
        SetRunFont(nested, "ExoticFontBBB");

        var (svg, warnings) = PptxToSvgWriter.WriteSlide(
            slide,
            doc.SlideSize,
            SvgSaveOptions.Default,
            slide.Master.Theme.Fonts,
            Array.Empty<EmbeddedFont>()
        );

        svg.Length.ShouldBeGreaterThan(50);
        warnings.ShouldContain("ExoticFontAAA");
        warnings.ShouldContain("ExoticFontBBB"); // gathered via group recursion
    }

    [Fact]
    public async Task ExportAsSvg_AnnotateShapes_EmitsShapeIndexAttribute()
    {
        var doc = PptxFixtures.WithSlides(1);
        doc.Slides[0].Shapes.AddTextBox(Emu.FromInches(1), Emu.FromInches(1), Emu.FromInches(4), Emu.FromInches(1), "Annotated");

        var svgs = await Processor.ExportAsSvgAsync(
            doc,
            new SvgSaveOptions { AnnotateShapes = true },
            TestContext.Current.CancellationToken
        );

        Encoding.UTF8.GetString(svgs[0]).ShouldContain("data-shape-index=");
    }
}
