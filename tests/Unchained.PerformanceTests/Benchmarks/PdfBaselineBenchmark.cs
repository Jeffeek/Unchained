using BenchmarkDotNet.Attributes;
using Unchained.Pdf.Abstractions;
using Unchained.Pdf.Engine;
using Unchained.PerformanceTests.Infrastructure;

namespace Unchained.PerformanceTests.Benchmarks;

/// <summary>
///     Baseline benchmarks for Unchained's own PDF parser.
///     Measures the cost of parsing and structural traversal using fixtures
///     produced by <see cref="MinimalPdfFactory" /> — no external library dependency.
/// </summary>
[
    MemoryDiagnoser,
    ThreadingDiagnoser,
    GcServer,
    HideColumns("StdDev", "RatioSD", "Median")
]
// ReSharper disable once ClassCanBeSealed.Global
public class PdfBaselineBenchmark
{
    private IDocumentProcessor _processor = null!;
    private byte[] _singlePageBytes = null!;
    private string _singlePagePath = null!;
    private byte[] _tenPageBytes = null!;

    [GlobalSetup]
    public void Setup()
    {
        _singlePageBytes = MinimalPdfFactory.Build();
        _tenPageBytes = MinimalPdfFactory.Build(10);

        _singlePagePath = Path.Combine(Path.GetTempPath(), $"unchained_bench_{Guid.NewGuid():N}.pdf");
        File.WriteAllBytes(_singlePagePath, _singlePageBytes);

        _processor = new DocumentProcessor();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _processor.Dispose();
        if (File.Exists(_singlePagePath)) File.Delete(_singlePagePath);
    }

    [Benchmark(Baseline = true, Description = "Parse 1-page PDF from byte[]")]
    public async Task<int> ParseFromBytes_1Page()
    {
        await using var doc = await _processor.LoadAsync(
            new MemoryStream(_singlePageBytes));
        return doc.PageCount;
    }

    [Benchmark(Description = "Parse 1-page PDF from disk")]
    public async Task<int> ParseFromDisk_1Page()
    {
        await using var doc = await _processor.LoadAsync(_singlePagePath);
        return doc.PageCount;
    }

    [Benchmark(Description = "Parse 10-page PDF from byte[]")]
    public async Task<int> ParseFromBytes_10Pages()
    {
        await using var doc = await _processor.LoadAsync(
            new MemoryStream(_tenPageBytes));
        return doc.PageCount;
    }

    [Benchmark(Description = "Parse + iterate pages (10 pages)")]
    public async Task<double> ParseAndIteratePages()
    {
        await using var doc = await _processor.LoadAsync(
            new MemoryStream(_tenPageBytes));
        var total = 0.0;
        for (var i = 1; i <= doc.PageCount; i++)
            total += doc.Pages[i].Width;
        return total;
    }

    [Benchmark(Description = "Parse + save round-trip")]
    public async Task<int> RoundTrip()
    {
        await using var doc = await _processor.LoadAsync(
            new MemoryStream(_singlePageBytes));
        var ms = new MemoryStream();
        await _processor.SaveAsync(doc, ms);
        return (int)ms.Length;
    }
}
