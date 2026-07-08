using Shouldly;
using Unchained.Ooxml;
using Unchained.Pptx.Models;
using Unchained.Pptx.Shapes;
using Unchained.Pptx.Tests.Shared;
using Xunit;

namespace Unchained.Pptx.Tests.IntegrationTests;

/// <summary>
///     Verifies the Phase 2 OpenXML-SDK-backed reader (OpenOptions.UseOpenXmlEngine) produces a
///     model consistent with the legacy custom parser for the vocabulary it currently maps:
///     slide count, slide size, hidden flag, shape geometry, and text.
/// </summary>
public sealed class OpenXmlParserParityTests : PptxTestBase
{
    // Committed, MIT-licensed sample files (from python-pptx) copied to the test output by the
    // csproj. These give CI and every developer the same real-world parity corpus.
    private static string SamplesDir =>
        Path.Combine(AppContext.BaseDirectory, "TestFiles", "python-pptx");

    private static string SamplePath(string name) => Path.Combine(SamplesDir, name);

    [Fact]
    public async Task GeneratedDoc_BothParsers_AgreeOnStructureAndText()
    {
        // Build a presentation with the public API, save it (custom writer), then read it back
        // with both parsers and compare what the SDK reader currently maps.
        var doc = PptxFixtures.WithSlides(2);
        doc.Slides[0]
            .Shapes.AddTextBox(
                Emu.FromInches(1),
                Emu.FromInches(1),
                Emu.FromInches(5),
                Emu.FromInches(2),
                "Hello Parity"
            );
        doc.Slides[1].IsHidden = true;

        using var ms = new MemoryStream();
        await Processor.SaveAsync(doc, ms);
        var bytes = ms.ToArray();

        var custom = await Processor.LoadAsync(bytes);
        var sdk = await Processor.LoadAsync(bytes, new OpenOptions { UseOpenXmlEngine = true });

        sdk.Slides.Count.ShouldBe(custom.Slides.Count);
        sdk.SlideSize.Width.Value.ShouldBe(custom.SlideSize.Width.Value);
        sdk.SlideSize.Height.Value.ShouldBe(custom.SlideSize.Height.Value);

        // Text content of slide 1 should match (concatenated).
        var customText = custom.Slides[0].GetAllText().Replace("\r", string.Empty).Trim();
        var sdkText = sdk.Slides[0].GetAllText().Replace("\r", string.Empty).Trim();
        sdkText.ShouldContain("Hello Parity");
        customText.ShouldContain("Hello Parity");

        await custom.DisposeAsync();
        await sdk.DisposeAsync();
    }

    [
        Theory,
        InlineData("minimal.pptx"),
        InlineData("sld-slides.pptx"),
        InlineData("sld-background.pptx"),
        InlineData("cht-charts.pptx"),
        InlineData("tbl-cell.pptx"),
        InlineData("shp-picture.pptx"),
        InlineData("shp-groupshape.pptx"),
        InlineData("shp-shapes.pptx"),
        InlineData("mst-slide-layouts.pptx"),
        InlineData("prs-notes.pptx"),
        InlineData("prs-properties.pptx"),
        InlineData("dml-fill.pptx"),
        InlineData("dml-line.pptx"),
        InlineData("txt-font-props.pptx")
    ]
    public async Task RealFile_BothParsers_AgreeOnSlideCountSizeAndHidden(string fileName)
    {
        var path = SamplePath(fileName);
        File.Exists(path).ShouldBeTrue($"Sample {fileName} not found at {path}. Ensure TestFiles/python-pptx/ is copied to output.");

        var bytes = await File.ReadAllBytesAsync(path);

        var custom = await Processor.LoadAsync(bytes);
        var sdk = await Processor.LoadAsync(bytes, new OpenOptions { UseOpenXmlEngine = true });

        sdk.Slides.Count.ShouldBe(custom.Slides.Count, $"{fileName}: slide count");
        sdk.SlideSize.Width.Value.ShouldBe(custom.SlideSize.Width.Value, $"{fileName}: width");
        sdk.SlideSize.Height.Value.ShouldBe(custom.SlideSize.Height.Value, $"{fileName}: height");

        // Masters / layouts / theme parity (M3).
        sdk.Masters.Count.ShouldBe(custom.Masters.Count, $"{fileName}: master count");
        for (var m = 0; m < custom.Masters.Count; m++)
        {
            var cm = custom.Masters[m];
            var sm = sdk.Masters[m];
            sm.Layouts.Count.ShouldBe(cm.Layouts.Count, $"{fileName}: master {m + 1} layout count");

            // Theme colours (dark1/light1/accent1) should resolve identically.
            sm.Theme.Colors.Dark1.ShouldBe(cm.Theme.Colors.Dark1, $"{fileName}: master {m + 1} theme dark1");
            sm.Theme.Colors.Accent1.ShouldBe(cm.Theme.Colors.Accent1, $"{fileName}: master {m + 1} theme accent1");

            for (var l = 0; l < cm.Layouts.Count; l++)
            {
                sm.Layouts[l]
                    .LayoutType.ShouldBe(
                        cm.Layouts[l].LayoutType,
                        $"{fileName}: master {m + 1} layout {l + 1} type"
                    );
            }
        }

        // Each slide resolves to a layout whose type matches the custom parser's.
        for (var i = 0; i < custom.Slides.Count; i++)
        {
            sdk.Slides[i]
                .Layout.LayoutType.ShouldBe(
                    custom.Slides[i].Layout.LayoutType,
                    $"{fileName}: slide {i + 1} layout type"
                );
        }

        for (var i = 0; i < custom.Slides.Count; i++)
            sdk.Slides[i].IsHidden.ShouldBe(custom.Slides[i].IsHidden, $"{fileName}: slide {i + 1} hidden flag");

        // Notes / sections / comment authors parity (M4).
        for (var i = 0; i < custom.Slides.Count; i++)
        {
            sdk.Slides[i]
                .Notes.NotesText.ShouldBe(
                    custom.Slides[i].Notes.NotesText,
                    $"{fileName}: slide {i + 1} notes text"
                );
        }

        sdk.Sections.Count.ShouldBe(custom.Sections.Count, $"{fileName}: section count");
        sdk.CommentAuthors.Count.ShouldBe(custom.CommentAuthors.Count, $"{fileName}: comment author count");

        // Top-level shape count + type sequence per slide, plus picture image resolution.
        for (var i = 0; i < custom.Slides.Count; i++)
        {
            var customShapes = custom.Slides[i].Shapes;
            var sdkShapes = sdk.Slides[i].Shapes;
            sdkShapes.Count.ShouldBe(customShapes.Count, $"{fileName}: slide {i + 1} shape count");

            for (var j = 0; j < customShapes.Count; j++)
            {
                var cs = customShapes[j];
                var ss = sdkShapes[j];
                ss.GetType().ShouldBe(cs.GetType(), $"{fileName}: slide {i + 1} shape {j + 1} type");

                // Fill + line parity (M2): type, solid colour, line width/dash.
                ss.Fill.Type.ShouldBe(cs.Fill.Type, $"{fileName}: s{i + 1} sh{j + 1} fill type");
                if (cs.Fill.Solid is not null && ss.Fill.Solid is not null)
                    ss.Fill.Solid.Color.ShouldBe(cs.Fill.Solid.Color, $"{fileName}: s{i + 1} sh{j + 1} fill colour");
                ss.Line.WidthPoints.ShouldBe(cs.Line.WidthPoints, $"{fileName}: s{i + 1} sh{j + 1} line width");
                ss.Line.DashStyle.ShouldBe(cs.Line.DashStyle, $"{fileName}: s{i + 1} sh{j + 1} line dash");

                switch (cs)
                {
                    // Pictures must resolve their embedded image bytes identically.
                    case PictureShape cp when ss is PictureShape sp:
                    {
                        (sp.Image is not null).ShouldBe(
                            cp.Image is not null,
                            $"{fileName}: slide {i + 1} shape {j + 1} image presence"
                        );
                        if (cp.Image is not null && sp.Image is not null)
                        {
                            sp.Image.Data.Length.ShouldBe(
                                cp.Image.Data.Length,
                                $"{fileName}: slide {i + 1} shape {j + 1} image byte length"
                            );
                        }

                        break;
                    }
                    // Charts must resolve the same model (type + series count) from the chart part.
                    case ChartShape cc
                        when ss is ChartShape sc:
                        sc.Chart.Type.ShouldBe(
                            cc.Chart.Type,
                            $"{fileName}: slide {i + 1} shape {j + 1} chart type"
                        );
                        sc.Chart.Data.Series.Count.ShouldBe(
                            cc.Chart.Data.Series.Count,
                            $"{fileName}: slide {i + 1} shape {j + 1} chart series count"
                        );
                    break;
                    // Text runs must carry identical formatting (M1): plain text, bold/italic,
                    // font size, font name, paragraph alignment.
                    case AutoShape ca
                        when ss is AutoShape sa:
                    {
                        var cParas = ca.TextFrame.Paragraphs;
                        var sParas = sa.TextFrame.Paragraphs;
                        sParas.Count.ShouldBe(
                            cParas.Count,
                            $"{fileName}: slide {i + 1} shape {j + 1} paragraph count"
                        );

                        for (var pi = 0; pi < cParas.Count; pi++)
                        {
                            sParas[pi]
                                .Alignment.ShouldBe(
                                    cParas[pi].Alignment,
                                    $"{fileName}: s{i + 1} sh{j + 1} para {pi + 1} alignment"
                                );
                            sParas[pi]
                                .Runs.Count.ShouldBe(
                                    cParas[pi].Runs.Count,
                                    $"{fileName}: s{i + 1} sh{j + 1} para {pi + 1} run count"
                                );

                            for (var ri = 0; ri < cParas[pi].Runs.Count; ri++)
                            {
                                var cr = cParas[pi].Runs[ri];
                                var sr = sParas[pi].Runs[ri];
                                sr.Text.ShouldBe(cr.Text, $"{fileName}: s{i + 1} sh{j + 1} p{pi + 1} run {ri + 1} text");
                                sr.Format.Bold.ShouldBe(cr.Format.Bold, $"{fileName}: …run {ri + 1} bold");
                                sr.Format.Italic.ShouldBe(cr.Format.Italic, $"{fileName}: …run {ri + 1} italic");
                                sr.Format.FontSizePoints.ShouldBe(cr.Format.FontSizePoints, $"{fileName}: …run {ri + 1} size");
                                sr.Format.LatinFont.ShouldBe(cr.Format.LatinFont, $"{fileName}: …run {ri + 1} font");
                                sr.Format.Underline.ShouldBe(cr.Format.Underline, $"{fileName}: …run {ri + 1} underline");
                            }
                        }

                        break;
                    }
                }
            }
        }

        // Resolved media image count should match.
        sdk.Media.Images.Count.ShouldBe(custom.Media.Images.Count, $"{fileName}: media image count");

        await custom.DisposeAsync();
        await sdk.DisposeAsync();
    }
}
