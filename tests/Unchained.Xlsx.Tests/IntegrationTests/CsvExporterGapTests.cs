using System.Text;
using Shouldly;
using Unchained.Xlsx.Engine;
using Unchained.Xlsx.Models;
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
}
