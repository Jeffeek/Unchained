using System.Text;
using Shouldly;
using Unchained.Xlsx.Engine;
using Unchained.Xlsx.Models;
using Unchained.Xlsx.Models.Cell;
using Unchained.Xlsx.Tests.Helpers;
using Xunit;

namespace Unchained.Xlsx.Tests.IntegrationTests;

/// <summary>Branch coverage for CSV export/import options.</summary>
public class CsvCoverageTests
{
    // ── Export ───────────────────────────────────────────────────────────────

    [Fact]
    public void Export_EmptySheet_ProducesNoBytes()
    {
        using var document = XlsxFixtures.WithSheets("Empty");
        document.Sheets[0].ToCsv().Length.ShouldBe(0);
    }

    [Fact]
    public void Export_CustomDelimiter()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        document.Sheets[0].SetValue(1, 1, "a");
        document.Sheets[0].SetValue(1, 2, "b");

        var text = Encoding.UTF8.GetString(document.Sheets[0].ToCsv(new CsvSaveOptions { Delimiter = ';' }));
        text.ShouldContain("a;b");
    }

    [Fact]
    public void Export_QuoteAllFields()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        document.Sheets[0].SetValue(1, 1, "plain");

        var text = Encoding.UTF8.GetString(document.Sheets[0].ToCsv(new CsvSaveOptions { QuoteAllFields = true }));
        text.ShouldContain("\"plain\"");
    }

    [Fact]
    public void Export_WithBom_PrependsPreamble()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        document.Sheets[0].SetValue(1, 1, "x");

        var bytes = document.Sheets[0].ToCsv(new CsvSaveOptions { WriteBom = true });
        bytes[0].ShouldBe((byte)0xEF);
        bytes[1].ShouldBe((byte)0xBB);
        bytes[2].ShouldBe((byte)0xBF);
    }

    [Fact]
    public void Export_BooleanAndError()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        var sheet = document.Sheets[0];
        sheet.SetValue(1, 1, true);
        sheet.SetValue(1, 2, false);
        sheet[1, 3].SetValue(CellError.DivisionByZero);

        var text = Encoding.UTF8.GetString(sheet.ToCsv());
        text.ShouldContain("TRUE");
        text.ShouldContain("FALSE");
        text.ShouldContain("#DIV/0!");
    }

    [Fact]
    public void Export_CustomBooleanLabels()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        document.Sheets[0].SetValue(1, 1, true);

        var text = Encoding.UTF8.GetString(
            document.Sheets[0].ToCsv(new CsvSaveOptions { TrueValue = "Y", FalseValue = "N" }));
        text.ShouldContain("Y");
    }

    [Fact]
    public void Export_DateUsesConfiguredFormat()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        var sheet = document.Sheets[0];
        sheet.SetValue(1, 1, new DateTime(2023, 6, 15));
        sheet[1, 1].SetNumberFormat("yyyy-mm-dd");

        var text = Encoding.UTF8.GetString(sheet.ToCsv());
        text.ShouldContain("2023-06-15");
    }

    [Fact]
    public void Export_ExplicitRange()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        var sheet = document.Sheets[0];
        sheet.SetValue(1, 1, "in");
        sheet.SetValue(5, 5, "out");

        var options = new CsvSaveOptions { Range = CellRange.FromBounds(1, 1, 1, 1) };
        var text = Encoding.UTF8.GetString(sheet.ToCsv(options));
        text.ShouldContain("in");
        text.ShouldNotContain("out");
    }

    [Fact]
    public async Task SaveAsCsvAsync_ToFile()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        document.Sheets[0].SetValue(1, 1, "hello");
        var path = Path.Combine(Path.GetTempPath(), $"unchained_csv_{Guid.NewGuid():N}.csv");

        try
        {
            await document.Sheets[0].SaveAsCsvAsync(path, cancellationToken: TestContext.Current.CancellationToken);
            (await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken)).ShouldContain("hello");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    // ── Import ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Import_ZeroPaddedId_StaysString()
    {
        var csv = "007,42\r\n";
        using var processor = new SpreadsheetProcessor();
        using var document = await processor.LoadFromCsvAsync(
            new MemoryStream(Encoding.UTF8.GetBytes(csv)), cancellationToken: TestContext.Current.CancellationToken);

        document.Sheets[0].GetCell(1, 1)!.GetString().ShouldBe("007");
        document.Sheets[0].GetCell(1, 2)!.GetDouble().ShouldBe(42.0);
    }

    [Fact]
    public async Task Import_EmptyField_LeavesCellBlank()
    {
        var csv = "a,,c\r\n";
        using var processor = new SpreadsheetProcessor();
        using var document = await processor.LoadFromCsvAsync(
            new MemoryStream(Encoding.UTF8.GetBytes(csv)), cancellationToken: TestContext.Current.CancellationToken);

        document.Sheets[0].GetCell(1, 2).ShouldBeNull();
        document.Sheets[0].GetCell(1, 3)!.GetString().ShouldBe("c");
    }

    [Fact]
    public async Task Import_DateFormat_ParsesDate()
    {
        var csv = "2023-06-15\r\n";
        using var processor = new SpreadsheetProcessor();
        using var document = await processor.LoadFromCsvAsync(
            new MemoryStream(Encoding.UTF8.GetBytes(csv)),
            new CsvLoadOptions { DateFormat = "yyyy-MM-dd" },
            TestContext.Current.CancellationToken);

        document.Sheets[0].GetCell(1, 1)!.GetDateTime().ShouldBe(new DateTime(2023, 6, 15));
    }

    [Fact]
    public async Task Import_StripsUtf8Bom()
    {
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes("hello,1\r\n")).ToArray();
        using var processor = new SpreadsheetProcessor();
        using var document = await processor.LoadFromCsvAsync(
            new MemoryStream(bytes), cancellationToken: TestContext.Current.CancellationToken);

        document.Sheets[0].GetCell(1, 1)!.GetString().ShouldBe("hello");
    }

    [Fact]
    public async Task Import_CustomSheetName()
    {
        using var processor = new SpreadsheetProcessor();
        using var document = await processor.LoadFromCsvAsync(
            new MemoryStream(Encoding.UTF8.GetBytes("x\r\n")),
            new CsvLoadOptions { SheetName = "Imported" },
            TestContext.Current.CancellationToken);

        document.Sheets[0].Name.ShouldBe("Imported");
    }

    [Fact]
    public async Task Import_NoTrailingNewline_ReadsLastRow()
    {
        var csv = "a,b\r\nc,d";
        using var processor = new SpreadsheetProcessor();
        using var document = await processor.LoadFromCsvAsync(
            new MemoryStream(Encoding.UTF8.GetBytes(csv)), cancellationToken: TestContext.Current.CancellationToken);

        document.Sheets[0].GetCell(2, 2)!.GetString().ShouldBe("d");
    }
}
