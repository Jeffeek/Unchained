using System.Text;
using Shouldly;
using Unchained.Xlsx.Engine;
using Unchained.Xlsx.Models;
using Unchained.Xlsx.Tests.Helpers;
using Xunit;

namespace Unchained.Xlsx.Tests.IntegrationTests;

public class CsvTests
{
    [Fact]
    public void ExportCsv_WritesDelimitedRows()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        var sheet = document.Sheets[0];
        sheet.SetValue(1, 1, "Name");
        sheet.SetValue(1, 2, "Age");
        sheet.SetValue(2, 1, "Alice");
        sheet.SetValue(2, 2, 30.0);

        var bytes = sheet.ToCsv();
        var text = Encoding.UTF8.GetString(bytes);
        text.ShouldContain("Name,Age");
        text.ShouldContain("Alice,30");
    }

    [Fact]
    public void ExportCsv_QuotesFieldsWithDelimiter()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        document.Sheets[0].SetValue(1, 1, "Smith, John");

        var text = Encoding.UTF8.GetString(document.Sheets[0].ToCsv());
        text.ShouldContain("\"Smith, John\"");
    }

    [Fact]
    public async Task ImportCsv_ParsesAndInfersTypes()
    {
        const string csv = "Name,Age,Active\r\nAlice,30,TRUE\r\nBob,25,FALSE\r\n";
        using var processor = new SpreadsheetProcessor();
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        using var document = await processor.LoadFromCsvAsync(ms, cancellationToken: TestContext.Current.CancellationToken);
        var sheet = document.Sheets[0];
        sheet.GetCell(1, 1)!.GetString().ShouldBe("Name");
        sheet.GetCell(2, 2)!.GetDouble().ShouldBe(30.0);
        sheet.GetCell(2, 3)!.GetBoolean().ShouldBe(true);
        sheet.GetCell(3, 3)!.GetBoolean().ShouldBe(false);
    }

    [Fact]
    public async Task ImportCsv_HandlesQuotedFields()
    {
        const string csv = "\"Smith, John\",42\r\n";
        using var processor = new SpreadsheetProcessor();
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        using var document = await processor.LoadFromCsvAsync(ms, cancellationToken: TestContext.Current.CancellationToken);
        document.Sheets[0].GetCell(1, 1)!.GetString().ShouldBe("Smith, John");
        document.Sheets[0].GetCell(1, 2)!.GetDouble().ShouldBe(42.0);
    }

    [Fact]
    public async Task CsvRoundTrip_PreservesData()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        var sheet = document.Sheets[0];
        sheet.SetValue(1, 1, "Product");
        sheet.SetValue(1, 2, 9.99);

        using var exported = new MemoryStream();
        await sheet.SaveAsCsvAsync(exported, cancellationToken: TestContext.Current.CancellationToken);

        using var processor = new SpreadsheetProcessor();
        using var reimported = await processor.LoadFromCsvAsync(new MemoryStream(exported.ToArray()), cancellationToken: TestContext.Current.CancellationToken);
        reimported.Sheets[0].GetCell(1, 1)!.GetString().ShouldBe("Product");
        reimported.Sheets[0].GetCell(1, 2)!.GetDouble().ShouldBe(9.99);
    }

    [Fact]
    public async Task ImportCsv_TypeInferenceOff_KeepsStrings()
    {
        const string csv = "123,456\r\n";
        using var processor = new SpreadsheetProcessor();
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        using var document = await processor.LoadFromCsvAsync(ms, new CsvLoadOptions { TypeInference = false }, TestContext.Current.CancellationToken);
        document.Sheets[0].GetCell(1, 1)!.GetString().ShouldBe("123");
    }
}
