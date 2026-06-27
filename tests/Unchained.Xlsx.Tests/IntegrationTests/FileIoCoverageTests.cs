using System.Text;
using Shouldly;
using Unchained.Xlsx.Engine;
using Unchained.Xlsx.Models;
using Unchained.Xlsx.Tests.Helpers;
using Xunit;

namespace Unchained.Xlsx.Tests.IntegrationTests;

/// <summary>Covers the file-path I/O overloads on the processor and worksheet CSV export.</summary>
// ReSharper disable once ClassCanBeSealed.Global
public class FileIoCoverageTests
{
    private static string TempPath(string extension) =>
        Path.Combine(Path.GetTempPath(), $"unchained_{Guid.NewGuid():N}{extension}");

    [Fact]
    public async Task SaveAndLoad_ByFilePath()
    {
        var path = TempPath(".xlsx");
        try
        {
            using var document = XlsxFixtures.WithSheets("Data");
            document.Sheets[0].SetValue(1, 1, "fromFile");

            using var processor = new SpreadsheetProcessor();
            await processor.SaveAsync(document, path, cancellationToken: TestContext.Current.CancellationToken);
            File.Exists(path).ShouldBeTrue();

            using var reloaded = await processor.LoadAsync(path, cancellationToken: TestContext.Current.CancellationToken);
            reloaded.Sheets[0].GetCell(1, 1)!.GetString().ShouldBe("fromFile");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task LoadFromCsv_ByFilePath()
    {
        var path = TempPath(".csv");
        try
        {
            await File.WriteAllTextAsync(path, "Name,Age\r\nAlice,30\r\n", TestContext.Current.CancellationToken);

            using var processor = new SpreadsheetProcessor();
            using var document = await processor.LoadFromCsvAsync(path, cancellationToken: TestContext.Current.CancellationToken);
            document.Sheets[0].GetCell(2, 1)!.GetString().ShouldBe("Alice");
            document.Sheets[0].GetCell(2, 2)!.GetDouble().ShouldBe(30.0);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task LoadFromCsv_EmptyPath_Throws()
    {
        using var processor = new SpreadsheetProcessor();
        await Should.ThrowAsync<ArgumentException>(async () => await processor.LoadFromCsvAsync("  ", cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SaveAsync_EmptyPath_Throws()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        using var processor = new SpreadsheetProcessor();
        await Should.ThrowAsync<ArgumentException>(() => processor.SaveAsync(document, "  ", cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SaveAsync_NullDocumentToFile_Throws()
    {
        using var processor = new SpreadsheetProcessor();
        await Should.ThrowAsync<ArgumentNullException>(() => processor.SaveAsync(null!, "x.xlsx", cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Worksheet_SaveAsCsv_ToStream()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        document.Sheets[0].SetValue(1, 1, "streamed");

        using var ms = new MemoryStream();
        await document.Sheets[0].SaveAsCsvAsync(ms, cancellationToken: TestContext.Current.CancellationToken);
        Encoding.UTF8.GetString(ms.ToArray()).ShouldContain("streamed");
    }

    [Fact]
    public async Task Worksheet_SaveAsCsv_EmptyPath_Throws()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        await Should.ThrowAsync<ArgumentException>(() => document.Sheets[0].SaveAsCsvAsync("  ", cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task LoadFromCsv_WithCustomDelimiter()
    {
        var path = TempPath(".csv");
        try
        {
            await File.WriteAllTextAsync(path, "a;b;c\r\n", TestContext.Current.CancellationToken);
            using var processor = new SpreadsheetProcessor();
            using var document = await processor.LoadFromCsvAsync(
                path,
                new CsvLoadOptions { Delimiter = ';' },
                TestContext.Current.CancellationToken
            );
            document.Sheets[0].GetCell(1, 3)!.GetString().ShouldBe("c");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
