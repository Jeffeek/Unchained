using System.Text;
using Shouldly;
using Unchained.Pdf.Engine;
using Unchained.Pdf.Models;
using Unchained.Pdf.Tests.Helpers;
using Xunit;

namespace Unchained.Pdf.Tests.IntegrationTests;

/// <summary>
///     Tests for <see cref="Engine.EmbeddedFileEditor" /> — reading and writing
///     file attachments via the PDF <c>/Names /EmbeddedFiles</c> name tree (ISO 32000-1 §7.11.4),
///     and PDF Portfolio (<c>/Collection</c>) support.
/// </summary>
public sealed class EmbeddedFilesTests : PdfTestBase
{
    // ── Read ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetEmbeddedFiles_WhenNoneExist_ReturnsEmpty()
    {
        var editor = new EmbeddedFileEditor();
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        editor.GetEmbeddedFiles(doc).ShouldBeEmpty();
    }

    // ── Add ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AddEmbeddedFile_SingleFile_CanBeRetrieved()
    {
        var editor = new EmbeddedFileEditor();
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);

        var file = new EmbeddedFile(
            "report",
            "report.txt",
            "Monthly report",
            "text/plain",
            "Hello embedded world"u8.ToArray()
        );

        await editor.AddEmbeddedFileAsync(doc, file, TestContext.Current.CancellationToken);

        var result = editor.GetEmbeddedFiles(doc);
        result.Count.ShouldBe(1);
        result[0].Name.ShouldBe("report");
        result[0].FileName.ShouldBe("report.txt");
    }

    [Fact]
    public async Task AddEmbeddedFile_TwoFiles_BothRetrieved()
    {
        var editor = new EmbeddedFileEditor();
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);

        await editor.AddEmbeddedFileAsync(
            doc,
            new EmbeddedFile("file1", "a.txt", null, null, "AAA"u8.ToArray()),
            TestContext.Current.CancellationToken
        );
        await editor.AddEmbeddedFileAsync(
            doc,
            new EmbeddedFile("file2", "b.txt", null, null, "BBB"u8.ToArray()),
            TestContext.Current.CancellationToken
        );

        editor.GetEmbeddedFiles(doc).Count.ShouldBe(2);
    }

    [Fact]
    public async Task AddEmbeddedFile_FileData_RoundTripsAfterSaveAndReload()
    {
        var editor = new EmbeddedFileEditor();
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);

        var data = "Hello from embedded file!"u8.ToArray();
        await editor.AddEmbeddedFileAsync(
            doc,
            new EmbeddedFile("myfile", "hello.txt", null, "text/plain", data),
            TestContext.Current.CancellationToken
        );

        await using var reloaded = await SaveAndReloadAsync(doc, TestContext.Current.CancellationToken);
        var files = editor.GetEmbeddedFiles(reloaded);
        files.Count.ShouldBe(1);
        files[0].Data.ShouldBe(data);
    }

    // ── Remove ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RemoveEmbeddedFile_ExistingFile_RemovesIt()
    {
        var editor = new EmbeddedFileEditor();
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);

        await editor.AddEmbeddedFileAsync(
            doc,
            new EmbeddedFile("myfile", "test.txt", null, null, "data"u8.ToArray()),
            TestContext.Current.CancellationToken
        );
        await editor.RemoveEmbeddedFileAsync(doc, "myfile", TestContext.Current.CancellationToken);

        editor.GetEmbeddedFiles(doc).ShouldBeEmpty();
    }

    [Fact]
    public async Task RemoveEmbeddedFile_NonExistentName_DoesNotThrow()
    {
        var editor = new EmbeddedFileEditor();
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);

        // Should complete without throwing even when file doesn't exist.
        await editor.RemoveEmbeddedFileAsync(doc, "nonexistent", TestContext.Current.CancellationToken);
    }

    // ── PDF Portfolio ─────────────────────────────────────────────────────────

    [Fact]
    public async Task EnablePortfolioMode_AddsCollectionToCatalog()
    {
        var editor = new EmbeddedFileEditor();
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);

        await editor.EnablePortfolioModeAsync(doc, TestContext.Current.CancellationToken);

        using var ms = new MemoryStream();
        await Processor.SaveAsync(doc, ms, ct: TestContext.Current.CancellationToken);
        Encoding.Latin1.GetString(ms.ToArray()).ShouldContain("/Collection");
    }

    [Fact]
    public async Task DisablePortfolioMode_RemovesCollectionFromCatalog()
    {
        var editor = new EmbeddedFileEditor();
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);

        await editor.EnablePortfolioModeAsync(doc, TestContext.Current.CancellationToken);
        await editor.DisablePortfolioModeAsync(doc, TestContext.Current.CancellationToken);

        using var ms = new MemoryStream();
        await Processor.SaveAsync(doc, ms, ct: TestContext.Current.CancellationToken);
        Encoding.Latin1.GetString(ms.ToArray()).ShouldNotContain("/Collection");
    }

    [Fact]
    public async Task EnablePortfolioMode_CalledTwice_StillLoadable()
    {
        var editor = new EmbeddedFileEditor();
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);

        await editor.EnablePortfolioModeAsync(doc, TestContext.Current.CancellationToken);
        await editor.EnablePortfolioModeAsync(doc, TestContext.Current.CancellationToken);

        // Document must remain parseable after a double-enable call.
        await using var reloaded = await SaveAndReloadAsync(doc, TestContext.Current.CancellationToken);
        reloaded.PageCount.ShouldBe(1);
    }
}
