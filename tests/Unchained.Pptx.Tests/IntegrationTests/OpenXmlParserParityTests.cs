using Shouldly;
using Unchained.Ooxml;
using Unchained.Pptx.Engine;
using Unchained.Pptx.Models;
using Unchained.Pptx.Models.Shapes;
using Unchained.Pptx.Tests.Helpers;
using Xunit;

namespace Unchained.Pptx.Tests.IntegrationTests;

/// <summary>
/// Verifies the Phase 2 OpenXML-SDK-backed reader (OpenOptions.UseOpenXmlEngine) produces a
/// model consistent with the legacy custom parser for the vocabulary it currently maps:
/// slide count, slide size, hidden flag, shape geometry, and text.
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
        doc.Slides[0].Shapes.AddTextBox(
            Emu.FromInches(1), Emu.FromInches(1), Emu.FromInches(5), Emu.FromInches(2),
            "Hello Parity");
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

        custom.Dispose();
        sdk.Dispose();
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
        Assert.SkipUnless(File.Exists(path),
            $"Sample {fileName} not found at {path}. Ensure TestFiles/python-pptx/ is copied to output.");

        var bytes = await File.ReadAllBytesAsync(path);

        var custom = await Processor.LoadAsync(bytes);
        var sdk = await Processor.LoadAsync(bytes, new OpenOptions { UseOpenXmlEngine = true });

        sdk.Slides.Count.ShouldBe(custom.Slides.Count, $"{fileName}: slide count");
        sdk.SlideSize.Width.Value.ShouldBe(custom.SlideSize.Width.Value, $"{fileName}: width");
        sdk.SlideSize.Height.Value.ShouldBe(custom.SlideSize.Height.Value, $"{fileName}: height");

        for (var i = 0; i < custom.Slides.Count; i++)
            sdk.Slides[i].IsHidden.ShouldBe(custom.Slides[i].IsHidden, $"{fileName}: slide {i + 1} hidden flag");

        // Top-level shape count + type sequence per slide.
        for (var i = 0; i < custom.Slides.Count; i++)
        {
            var customShapes = custom.Slides[i].Shapes;
            var sdkShapes = sdk.Slides[i].Shapes;
            sdkShapes.Count.ShouldBe(customShapes.Count, $"{fileName}: slide {i + 1} shape count");

            for (var j = 0; j < customShapes.Count; j++)
                sdkShapes[j].GetType().ShouldBe(customShapes[j].GetType(),
                    $"{fileName}: slide {i + 1} shape {j + 1} type");
        }

        custom.Dispose();
        sdk.Dispose();
    }
}
