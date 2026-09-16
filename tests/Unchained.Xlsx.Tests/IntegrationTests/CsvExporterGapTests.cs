using System.Text;
using Shouldly;
using Unchained.Xlsx.Engine;
using Unchained.Xlsx.Models;
using Unchained.Xlsx.Models.Cell;
using Xunit;

namespace Unchained.Xlsx.Tests.IntegrationTests;

/// <summary>
///     Tests for <see cref="Unchained.Xlsx.Export.CsvExporter" /> branches not exercised elsewhere:
///     a non-UTF-8 encoding, a formula-typed cell, and double-quote escaping.
/// </summary>
public sealed class CsvExporterGapTests
{
    [Fact]
    public void ToCsv_NonUtf8Encoding_EncodesWithConfiguredEncoding()
    {
        using var processor = new SpreadsheetProcessor();
        using var document = processor.CreateBlank("Sheet1");
        document.Sheets[0].SetValue(1, 1, "café");

        var bytes = document.Sheets[0].ToCsv(new CsvSaveOptions { Encoding = Encoding.Latin1 });

        Encoding.Latin1.GetString(bytes).ShouldStartWith("café");
        // Latin1 encodes 'é' as a single 0xE9 byte, unlike UTF-8's two-byte sequence.
        bytes.ShouldContain((byte)0xE9);
    }

    [Fact]
    public void ToCsv_FormulaCell_RendersCachedResult()
    {
        using var processor = new SpreadsheetProcessor();
        using var document = processor.CreateBlank("Sheet1");
        document.Sheets[0].SetFormula(1, 1, "=1+2");
        document.Recalculate();

        var text = Encoding.UTF8.GetString(document.Sheets[0].ToCsv());

        text.ShouldContain("3");
    }

    [Fact]
    public void ToCsv_FieldWithEmbeddedQuote_DoublesQuoteAndWraps()
    {
        using var processor = new SpreadsheetProcessor();
        using var document = processor.CreateBlank("Sheet1");
        document.Sheets[0].SetValue(1, 1, "say \"hi\"");

        var text = Encoding.UTF8.GetString(document.Sheets[0].ToCsv());

        text.ShouldContain("\"say \"\"hi\"\"\"");
    }

    [Fact]
    public void ToCsv_EmptySheet_ReturnsEmptyBytes()
    {
        using var processor = new SpreadsheetProcessor();
        using var document = processor.CreateBlank("Sheet1");

        var bytes = document.Sheets[0].ToCsv();

        bytes.ShouldBeEmpty();
    }

    [Fact]
    public void ToCsv_CustomDelimiter_UsesConfiguredDelimiter()
    {
        using var processor = new SpreadsheetProcessor();
        using var document = processor.CreateBlank("Sheet1");
        document.Sheets[0].SetValue(1, 1, "a");
        document.Sheets[0].SetValue(1, 2, "b");

        var text = Encoding.UTF8.GetString(document.Sheets[0].ToCsv(new CsvSaveOptions { Delimiter = ';' }));

        text.ShouldContain("a;b");
    }

    [Fact]
    public void ToCsv_QuoteAllFields_QuotesAllFields()
    {
        using var processor = new SpreadsheetProcessor();
        using var document = processor.CreateBlank("Sheet1");
        document.Sheets[0].SetValue(1, 1, "simple");

        var text = Encoding.UTF8.GetString(document.Sheets[0].ToCsv(new CsvSaveOptions { QuoteAllFields = true }));

        text.ShouldContain("\"simple\"");
    }

    [Fact]
    public void ToCsv_FieldWithNewline_QuotesField()
    {
        using var processor = new SpreadsheetProcessor();
        using var document = processor.CreateBlank("Sheet1");
        document.Sheets[0].SetValue(1, 1, "line1\nline2");

        var text = Encoding.UTF8.GetString(document.Sheets[0].ToCsv());

        text.ShouldContain("\"line1\nline2\"");
    }

    [Fact]
    public void ToCsv_NumberCell_FormatsAsNumber()
    {
        using var processor = new SpreadsheetProcessor();
        using var document = processor.CreateBlank("Sheet1");
        document.Sheets[0].SetValue(1, 1, 1234.5);

        var text = Encoding.UTF8.GetString(document.Sheets[0].ToCsv());

        text.ShouldContain("1234.5");
    }

    [Fact]
    public void ToCsv_CustomRange_ExportsOnlyRange()
    {
        using var processor = new SpreadsheetProcessor();
        using var document = processor.CreateBlank("Sheet1");
        document.Sheets[0].SetValue(1, 1, "A1");
        document.Sheets[0].SetValue(2, 2, "B2");
        document.Sheets[0].SetValue(3, 3, "C3");

        var text = Encoding.UTF8.GetString(
            document.Sheets[0].ToCsv(new CsvSaveOptions { Range = CellRange.FromA1("B2") }));

        text.Trim().ShouldBe("B2");
    }

    [Fact]
    public void ToCsv_BooleanCell_RendersBoolean()
    {
        using var processor = new SpreadsheetProcessor();
        using var document = processor.CreateBlank("Sheet1");
        document.Sheets[0].SetValue(1, 1, true);

        var text = Encoding.UTF8.GetString(document.Sheets[0].ToCsv());

        text.ShouldContain("TRUE");
    }

    [Fact]
    public void ToCsv_FieldWithCarriageReturn_QuotesField()
    {
        using var processor = new SpreadsheetProcessor();
        using var document = processor.CreateBlank("Sheet1");
        document.Sheets[0].SetValue(1, 1, "line1\r\nline2");

        var text = Encoding.UTF8.GetString(document.Sheets[0].ToCsv());

        text.ShouldContain("\"line1\r\nline2\"");
    }
}
