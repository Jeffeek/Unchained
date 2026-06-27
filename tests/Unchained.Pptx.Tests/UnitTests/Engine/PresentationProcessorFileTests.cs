using Shouldly;
using Unchained.Pptx.Engine;
using Unchained.Pptx.Tests.Helpers;
using Xunit;

namespace Unchained.Pptx.Tests.UnitTests.Engine;

/// <summary>
///     Coverage for <see cref="PresentationProcessor" /> paths not hit by stream-based round-trips:
///     the concurrency-limit guard, and the file-path overloads of load/save/export (PPTX, ODP, PDF).
/// </summary>
public sealed class PresentationProcessorFileTests
{
    [Fact]
    public void Constructor_ZeroConcurrency_Throws() =>
        Should.Throw<ArgumentOutOfRangeException>(static () => new PresentationProcessor(0));

    [Fact]
    public void Constructor_ExplicitConcurrency_Succeeds()
    {
        using var processor = new PresentationProcessor(2);
        processor.ShouldNotBeNull();
    }

    [Fact]
    public async Task SaveAsync_AndLoadAsync_ByFilePath_RoundTrips()
    {
        using var processor = new PresentationProcessor();
        await using var doc = PptxFixtures.WithSlides(2);

        var dir = Directory.CreateTempSubdirectory("pptx-proc-test");
        try
        {
            var path = Path.Combine(dir.FullName, "deck.pptx");
            await processor.SaveAsync(doc, path, cancellationToken: TestContext.Current.CancellationToken);
            File.Exists(path).ShouldBeTrue();

            var reloaded = await processor.LoadAsync(path, cancellationToken: TestContext.Current.CancellationToken);
            reloaded.Slides.Count.ShouldBe(2);
            await reloaded.DisposeAsync();
        }
        finally
        {
            dir.Delete(true);
        }
    }

    [Fact]
    public async Task SaveAsOdpAsync_ByFilePath_WritesFile()
    {
        using var processor = new PresentationProcessor();
        await using var doc = PptxFixtures.WithSlides(1);

        var dir = Directory.CreateTempSubdirectory("pptx-odp-test");
        try
        {
            var path = Path.Combine(dir.FullName, "deck.odp");
            await processor.SaveAsOdpAsync(doc, path, cancellationToken: TestContext.Current.CancellationToken);
            new FileInfo(path).Length.ShouldBeGreaterThan(0);
        }
        finally
        {
            dir.Delete(true);
        }
    }

    [Fact]
    public async Task SaveAsPdfAsync_ByFilePath_WritesFile()
    {
        using var processor = new PresentationProcessor();
        await using var doc = PptxFixtures.WithSlides(1);

        var dir = Directory.CreateTempSubdirectory("pptx-pdf-test");
        try
        {
            var path = Path.Combine(dir.FullName, "deck.pdf");
            await processor.SaveAsPdfAsync(doc, path, cancellationToken: TestContext.Current.CancellationToken);
            new FileInfo(path).Length.ShouldBeGreaterThan(0);
        }
        finally
        {
            dir.Delete(true);
        }
    }

    [Fact]
    public async Task LoadAsync_BlankPath_Throws()
    {
        using var processor = new PresentationProcessor();
        await Should.ThrowAsync<ArgumentException>(() => processor.LoadAsync("   ", cancellationToken: TestContext.Current.CancellationToken));
    }
}
