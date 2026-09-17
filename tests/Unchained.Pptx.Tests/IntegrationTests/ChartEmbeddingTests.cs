using Shouldly;
using System.IO.Compression;
using Unchained.Pptx.Models;
using Unchained.Pptx.Shapes;
using Unchained.Pptx.Tests.Shared;
using Xunit;

namespace Unchained.Pptx.Tests.IntegrationTests;

/// <summary>
///     A chart's embedded workbook (and any chart style/colour parts) must survive load/save and
///     cross-deck clone through the custom writer, with the chart part's <c>.rels</c> re-emitted.
/// </summary>
public sealed class ChartEmbeddingTests : PptxTestBase
{
    private static Task<byte[]> ChartSampleAsync()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestFiles", "python-pptx", "cht-charts.pptx");
        File.Exists(path).ShouldBeTrue("chart sample missing");
        return File.ReadAllBytesAsync(path);
    }

    private static IEnumerable<string> PartNames(byte[] pptx)
    {
        using var ms = new MemoryStream(pptx);
        using var archive = new ZipArchive(ms, ZipArchiveMode.Read);
        return archive.Entries.Select(static e => e.FullName).ToList();
    }

    private static string ReadEntry(byte[] pptx, string name)
    {
        using var ms = new MemoryStream(pptx);
        using var archive = new ZipArchive(ms, ZipArchiveMode.Read);
        var entry = archive.GetEntry(name);
        entry.ShouldNotBeNull($"missing {name}");
        using var reader = new StreamReader(entry.Open());
        return reader.ReadToEnd();
    }

    private static IEnumerable<string> Embeddings(byte[] pptx) =>
        PartNames(pptx).Where(static n => n.StartsWith("ppt/embeddings/", StringComparison.Ordinal) && n.EndsWith(".xlsx", StringComparison.Ordinal));

    // ── Load → save (fixes the general drop bug, not just clone) ───────────────

    [Fact]
    public async Task LoadSave_PreservesEmbeddedWorkbook()
    {
        var doc = await Processor.LoadAsync(await ChartSampleAsync(), cancellationToken: TestContext.Current.CancellationToken);

        using var ms = new MemoryStream();
        await Processor.SaveAsync(doc, ms, cancellationToken: TestContext.Current.CancellationToken);
        var saved = ms.ToArray();

        Embeddings(saved).ShouldNotBeEmpty();
    }

    [Fact]
    public async Task LoadSave_ChartRelsReferenceWorkbook()
    {
        var doc = await Processor.LoadAsync(await ChartSampleAsync(), cancellationToken: TestContext.Current.CancellationToken);

        using var ms = new MemoryStream();
        await Processor.SaveAsync(doc, ms, cancellationToken: TestContext.Current.CancellationToken);
        var saved = ms.ToArray();

        var relsPart = PartNames(saved).Single(static n => n.StartsWith("ppt/charts/_rels/", StringComparison.Ordinal));
        ReadEntry(saved, relsPart).ShouldContain("embeddings/");
    }

    // ── Cross-deck clone ──────────────────────────────────────────────────────

    [Fact]
    public async Task CrossDeckClone_PreservesEmbeddedWorkbook()
    {
        var source = await Processor.LoadAsync(await ChartSampleAsync(), cancellationToken: TestContext.Current.CancellationToken);
        var chartSlide = source.Slides.First(static s => s.Shapes.OfType<ChartShape>().Any());
        var target = PptxFixtures.BlankPresentation();
        target.Slides.AddClone(chartSlide);

        using var ms = new MemoryStream();
        await Processor.SaveAsync(target, ms, cancellationToken: TestContext.Current.CancellationToken);
        var saved = ms.ToArray();

        Embeddings(saved).ShouldNotBeEmpty();
        var relsPart = PartNames(saved).Single(static n => n.StartsWith("ppt/charts/_rels/", StringComparison.Ordinal));
        ReadEntry(saved, relsPart).ShouldContain("embeddings/");
    }

    [Fact]
    public async Task CrossDeckClone_TwoClones_ProduceDistinctEmbeddings()
    {
        var source = await Processor.LoadAsync(await ChartSampleAsync(), cancellationToken: TestContext.Current.CancellationToken);
        var chartSlide = source.Slides.First(static s => s.Shapes.OfType<ChartShape>().Any());
        var target = PptxFixtures.BlankPresentation();
        target.Slides.AddClone(chartSlide);
        target.Slides.AddClone(chartSlide);

        using var ms = new MemoryStream();
        await Processor.SaveAsync(target, ms, cancellationToken: TestContext.Current.CancellationToken);
        var saved = ms.ToArray();

        Embeddings(saved).Count().ShouldBe(2);
    }

    // ── SDK parser → custom writer parity ─────────────────────────────────────

    [Fact]
    public async Task SdkParse_CustomSave_PreservesEmbeddedWorkbook()
    {
        var doc = await Processor.LoadAsync(
            await ChartSampleAsync(),
            new OpenOptions { UseOpenXmlEngine = true },
            TestContext.Current.CancellationToken
        );

        using var ms = new MemoryStream();
        await Processor.SaveAsync(doc, ms, cancellationToken: TestContext.Current.CancellationToken);
        var saved = ms.ToArray();

        Embeddings(saved).ShouldNotBeEmpty();
    }

    [Fact]
    public async Task CrossDeckClone_ChartReloads()
    {
        var source = await Processor.LoadAsync(await ChartSampleAsync(), cancellationToken: TestContext.Current.CancellationToken);
        var chartSlide = source.Slides.First(static s => s.Shapes.OfType<ChartShape>().Any());
        var target = PptxFixtures.BlankPresentation();
        target.Slides.AddClone(chartSlide);

        var reloaded = await PptxFixtures.RoundTripAsync(target);

        reloaded.Slides.SelectMany(static s => s.Shapes).OfType<ChartShape>().ShouldNotBeEmpty();
    }
}
