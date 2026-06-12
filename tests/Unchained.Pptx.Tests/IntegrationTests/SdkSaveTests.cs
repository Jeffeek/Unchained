using System.IO.Packaging;
using Shouldly;
using Unchained.Pptx.Engine;
using Unchained.Pptx.Models;
using Xunit;

namespace Unchained.Pptx.Tests.IntegrationTests;

/// <summary>
///     M5b: the SDK-backed save re-emits modelled slides while preserving every other part. Compared
///     against the custom writer (which rebuilds the package and drops parts), the SDK save must keep
///     the part set intact and still reload to the same slide structure.
/// </summary>
public sealed class SdkSaveTests
{
    private static string SamplePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "TestFiles", "python-pptx", name);

    private static int CountParts(byte[] pptx)
    {
        using var ms = new MemoryStream(pptx);
        using var pkg = Package.Open(ms, FileMode.Open, FileAccess.Read);
        return pkg.GetParts().Count();
    }

    [
        Theory,
        InlineData("shp-shapes.pptx"),
        InlineData("cht-charts.pptx"),
        InlineData("prs-notes.pptx"),
        InlineData("tbl-cell.pptx"),
        InlineData("mst-slide-layouts.pptx")
    ]
    public async Task SdkSave_PreservesAllParts(string fileName)
    {
        var path = SamplePath(fileName);
        Assert.SkipUnless(File.Exists(path), $"sample {fileName} missing");
        var original = await File.ReadAllBytesAsync(path);
        var beforeParts = CountParts(original);

        using var processor = new PresentationProcessor();
        var doc = await processor.LoadAsync(original, new OpenOptions { UseOpenXmlEngine = true });

        using var ms = new MemoryStream();
        await processor.SaveAsync(doc, ms, new SaveOptions { UseOpenXmlEngine = true });
        var saved = ms.ToArray();

        // The SDK save must not drop parts (the custom writer does).
        CountParts(saved).ShouldBe(beforeParts, $"{fileName}: SDK save must preserve all parts");

        doc.Dispose();
    }

    [Fact]
    public async Task SdkSave_ThenReload_PreservesSlideStructure()
    {
        var path = SamplePath("shp-shapes.pptx");
        Assert.SkipUnless(File.Exists(path), "sample missing");
        var original = await File.ReadAllBytesAsync(path);

        using var processor = new PresentationProcessor();
        var doc = await processor.LoadAsync(original, new OpenOptions { UseOpenXmlEngine = true });
        var originalShapeCounts = doc.Slides.Select(static s => s.Shapes.Count).ToArray();

        using var ms = new MemoryStream();
        await processor.SaveAsync(doc, ms, new SaveOptions { UseOpenXmlEngine = true });

        var reloaded = await processor.LoadAsync(ms.ToArray(), new OpenOptions { UseOpenXmlEngine = true });
        reloaded.Slides.Count.ShouldBe(doc.Slides.Count, "slide count after SDK save round-trip");
        reloaded.Slides.Select(static s => s.Shapes.Count).ToArray()
            .ShouldBe(originalShapeCounts, "per-slide shape count after SDK save round-trip");

        doc.Dispose();
        reloaded.Dispose();
    }

    [Fact]
    public async Task SdkSave_FallsBackToCustomWriter_WhenNoEngine()
    {
        // A CreateBlank document has no engine; requesting the SDK save must still succeed
        // (falls back to the custom writer) rather than throw.
        using var processor = new PresentationProcessor();
        var doc = processor.CreateBlank();
        doc.Slides.AddBlank(doc.Masters[0].Layouts[0]);

        using var ms = new MemoryStream();
        await Should.NotThrowAsync(async () =>
            await processor.SaveAsync(doc, ms, new SaveOptions { UseOpenXmlEngine = true }));
        ms.Length.ShouldBeGreaterThan(0);

        doc.Dispose();
    }
}
