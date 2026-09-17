using Shouldly;
using Unchained.Pdf.Engine;
using Xunit;

namespace Unchained.Pdf.Tests.IntegrationTests;

/// <summary>
///     Tests that exercise PdfParser robustness with edge cases and malformed input.
///     Validates error handling and resilience when parsing invalid or truncated PDFs.
/// </summary>
public sealed class PdfParserRegressionTests : IDisposable
{
    private readonly DocumentProcessor _processor = new();

    public void Dispose() => _processor.Dispose();

    [Fact]
    public async Task Parser_InvalidPdfSignature_ThrowsOrHandlesGracefully()
    {
        var invalidPdf = "This is not a PDF file"u8.ToArray();

        await Should.ThrowAsync<Exception>(async () =>
            {
                await using var doc = await _processor.LoadAsync(
                    new MemoryStream(invalidPdf),
                    TestContext.Current.CancellationToken
                );
            }
        );
    }

    [Fact]
    public async Task Parser_EmptyStream_ThrowsOrHandlesGracefully()
    {
        var emptyStream = new MemoryStream();

        await Should.ThrowAsync<Exception>(async () =>
            {
                await using var doc = await _processor.LoadAsync(
                    emptyStream,
                    TestContext.Current.CancellationToken
                );
            }
        );
    }

    [Fact]
    public async Task Parser_TruncatedPdf_ThrowsOrHandlesGracefully()
    {
        // PDF cut off in the middle
        var truncated = "%PDF-1.4\n1 0 obj\n<< /Type /"u8.ToArray();

        await Should.ThrowAsync<Exception>(async () =>
            {
                await using var doc = await _processor.LoadAsync(
                    new MemoryStream(truncated),
                    TestContext.Current.CancellationToken
                );
            }
        );
    }

    [Fact]
    public async Task Parser_OnlyPdfHeader_ThrowsOrHandlesGracefully()
    {
        var headerOnly = "%PDF-1.7\n"u8.ToArray();

        await Should.ThrowAsync<Exception>(async () =>
            {
                await using var doc = await _processor.LoadAsync(
                    new MemoryStream(headerOnly),
                    TestContext.Current.CancellationToken
                );
            }
        );
    }

    [Fact]
    public async Task Parser_CorruptedXref_ThrowsOrHandlesGracefully()
    {
        // PDF with corrupted xref section
        var corrupted = "%PDF-1.4\nxref\ngarbage\ntrailer\n<<>>\nstartxref\n0\n%%EOF"u8.ToArray();

        await Should.ThrowAsync<Exception>(async () =>
            {
                await using var doc = await _processor.LoadAsync(
                    new MemoryStream(corrupted),
                    TestContext.Current.CancellationToken
                );
            }
        );
    }

    [Fact]
    public async Task Parser_MissingEof_ThrowsOrHandlesGracefully()
    {
        // PDF without %%EOF marker
        var noEof = "%PDF-1.4\nsome content"u8.ToArray();

        await Should.ThrowAsync<Exception>(async () =>
            {
                await using var doc = await _processor.LoadAsync(
                    new MemoryStream(noEof),
                    TestContext.Current.CancellationToken
                );
            }
        );
    }

    [Fact]
    public async Task Parser_BinaryGarbage_ThrowsOrHandlesGracefully()
    {
        // Random binary data
        var random = new byte[1024];
        new Random(42).NextBytes(random);

        await Should.ThrowAsync<Exception>(async () =>
            {
                await using var doc = await _processor.LoadAsync(
                    new MemoryStream(random),
                    TestContext.Current.CancellationToken
                );
            }
        );
    }

    [Fact]
    public async Task Parser_VeryLargeFile_HandlesGracefully()
    {
        // Create a very large stream (but still invalid PDF)
        var large = new byte[10_000_000]; // 10MB of zeros
        "%PDF-1.4"u8.ToArray().CopyTo(large, 0);

        await Should.ThrowAsync<Exception>(async () =>
            {
                await using var doc = await _processor.LoadAsync(
                    new MemoryStream(large),
                    TestContext.Current.CancellationToken
                );
            }
        );
    }
}
