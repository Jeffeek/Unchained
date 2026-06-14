using System.Text;
using Moq;
using Shouldly;
using Unchained.Pdf.Abstractions;
using Unchained.Pdf.Core;
using Unchained.Pdf.Engine;
using Unchained.Pdf.Tests.Helpers;
using Xunit;

namespace Unchained.Pdf.Tests.IntegrationTests;

public sealed class DocumentProcessorLoadTests : IDisposable
{
    private readonly DocumentProcessor _processor = new();

    public void Dispose() => _processor.Dispose();

    [Fact]
    public async Task LoadAsync_Stream_SinglePage_PageCountIsOne()
    {
        var stream = new MemoryStream(PdfFixtures.SinglePage());
        await using var doc = await _processor.LoadAsync(stream, TestContext.Current.CancellationToken);
        doc.PageCount.ShouldBe(1);
    }

    [Fact]
    public async Task LoadAsync_Stream_MultiPage_PageCountMatches()
    {
        var stream = new MemoryStream(PdfFixtures.MultiPage(5));
        await using var doc = await _processor.LoadAsync(stream, TestContext.Current.CancellationToken);
        doc.PageCount.ShouldBe(5);
    }

    [Fact]
    public async Task LoadAsync_Stream_NotDisposedOnReturn()
    {
        var stream = new MemoryStream(PdfFixtures.SinglePage());
        await using var doc = await _processor.LoadAsync(stream, TestContext.Current.CancellationToken);
        doc.IsDisposed.ShouldBeFalse();
    }

    [Fact]
    public async Task LoadAsync_FilePath_LoadsCorrectly()
    {
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllBytesAsync(path, PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
            await using var doc = await _processor.LoadAsync(path, TestContext.Current.CancellationToken);
            doc.PageCount.ShouldBe(1);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task LoadAsync_NullStream_Throws() =>
        await Should.ThrowAsync<ArgumentNullException>(() => _processor.LoadAsync((Stream)null!));

    [Fact]
    public async Task LoadAsync_NullPath_Throws() =>
        await Should.ThrowAsync<ArgumentException>(() => _processor.LoadAsync((string)null!));

    [Fact]
    public async Task LoadAsync_EmptyPath_Throws() =>
        await Should.ThrowAsync<ArgumentException>(() => _processor.LoadAsync(string.Empty));

    [Fact]
    public async Task LoadAsync_InvalidPdf_ThrowsPdfException()
    {
        var garbage = new MemoryStream("this is not a pdf"u8.ToArray());
        await Should.ThrowAsync<PdfException>(() => _processor.LoadAsync(garbage));
    }

    [Fact]
    public async Task LoadAsync_NonSeekableStream_StillLoads()
    {
        var bytes = PdfFixtures.SinglePage();
        var nonSeekable = new NonSeekableStream(bytes);
        await using var doc = await _processor.LoadAsync(nonSeekable, TestContext.Current.CancellationToken);
        doc.PageCount.ShouldBe(1);
    }

    [Fact]
    public async Task LoadAsync_ConcurrentLoads_AllSucceed()
    {
        var tasks = Enumerable.Range(0, 8)
            .Select(async _ =>
                {
                    var stream = new MemoryStream(PdfFixtures.SinglePage());
                    await using var doc = await _processor.LoadAsync(stream);
                    return doc.PageCount;
                }
            );
        var results = await Task.WhenAll(tasks);
        results.ShouldAllBe(static n => n == 1);
    }
}

public sealed class DocumentProcessorSaveTests : IDisposable
{
    private readonly DocumentProcessor _processor = new();

    public void Dispose() => _processor.Dispose();

    [Fact]
    public async Task SaveAsync_ToStream_WritesNonEmptyBytes()
    {
        await using var doc = await _processor.LoadAsync(new MemoryStream(PdfFixtures.SinglePage()), TestContext.Current.CancellationToken);
        var output = new MemoryStream();
        await _processor.SaveAsync(doc, output, ct: TestContext.Current.CancellationToken);
        output.Length.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task SaveAsync_ToStream_OutputStartsWithPdfHeader()
    {
        await using var doc = await _processor.LoadAsync(new MemoryStream(PdfFixtures.SinglePage()), TestContext.Current.CancellationToken);
        var output = new MemoryStream();
        await _processor.SaveAsync(doc, output, ct: TestContext.Current.CancellationToken);
        var header = Encoding.Latin1.GetString(output.ToArray(), 0, 7);
        header.ShouldBe("%PDF-1.");
    }

    [Fact]
    public async Task SaveAsync_ToFile_WritesFile()
    {
        var path = Path.GetTempFileName();
        try
        {
            await using var doc = await _processor.LoadAsync(new MemoryStream(PdfFixtures.SinglePage()), TestContext.Current.CancellationToken);
            await _processor.SaveAsync(doc, path, ct: TestContext.Current.CancellationToken);
            new FileInfo(path).Length.ShouldBeGreaterThan(0);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task SaveAsync_WithNullDoc_Throws() =>
        await Should.ThrowAsync<ArgumentNullException>(() =>
            _processor.SaveAsync(null!, new MemoryStream())
        );

    [Fact]
    public async Task SaveAsync_WithNullStream_Throws()
    {
        await using var doc = await _processor.LoadAsync(new MemoryStream(PdfFixtures.SinglePage()), TestContext.Current.CancellationToken);
        await Should.ThrowAsync<ArgumentNullException>(() =>
            _processor.SaveAsync(doc, (Stream)null!)
        );
    }

    [Fact]
    public async Task SaveAsync_ForeignDocument_ThrowsArgumentException()
    {
        // A mock IPdfDocument not produced by this processor
        var foreign = new Mock<IPdfDocument>();
        foreign.Setup(static d => d.IsDisposed).Returns(false);

        await Should.ThrowAsync<ArgumentException>(() =>
            _processor.SaveAsync(foreign.Object, new MemoryStream())
        );
    }
}

public sealed class DocumentProcessorLifetimeTests
{
    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        var processor = new DocumentProcessor();
        processor.Dispose();
        Should.NotThrow(() => processor.Dispose());
    }

    [Fact]
    public void Constructor_CustomConcurrency_Accepted() =>
        Should.NotThrow(static () =>
            {
                using var p = new DocumentProcessor(2);
            }
        );
}

/// <summary>Wraps a byte array in a non-seekable stream to test the copy-to-buffer path.</summary>
file sealed class NonSeekableStream(byte[] data) : Stream
{
    private readonly MemoryStream _inner = new(data);
    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override void Flush() { }
    public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
