using System.IO.Compression;
using System.Text;
using Shouldly;
using Unchained.Xlsx.Models;
using Unchained.Xlsx.Models.Cell;
using Unchained.Xlsx.Tests.Helpers;
using Xunit;

namespace Unchained.Xlsx.Tests.IntegrationTests;

/// <summary>Final push: CSV formula/date export, pivot cache from formula cells, and pivot collection enumeration.</summary>
public class CsvAndPivotValueCoverageTests
{
    // ── CsvExporter formula & date branches ──────────────────────────────────

    [Fact]
    public void Export_FormulaCell_WithCachedString()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        var cell = document.Sheets[0][1, 1];
        cell.FormulaText = "=\"hi\"";
        cell.SetFormulaCachedText("hi");

        var text = Encoding.UTF8.GetString(document.Sheets[0].ToCsv());
        text.ShouldContain("hi");
    }

    [Fact]
    public void Export_FormulaCell_WithCachedNumber()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        document.Sheets[0].SetFormulaWithCache(1, 1, "=1+1", 2);

        var text = Encoding.UTF8.GetString(document.Sheets[0].ToCsv());
        text.ShouldContain("2");
    }

    [Fact]
    public void Export_DateTimeWithTime_UsesDateTimeFormat()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        var sheet = document.Sheets[0];
        sheet.SetValue(
            1,
            1,
            new DateTime(
                2023,
                6,
                15,
                14,
                30,
                0
            )
        );
        sheet[1, 1].SetNumberFormat("yyyy-mm-dd hh:mm:ss");

        var text = Encoding.UTF8.GetString(sheet.ToCsv());
        text.ShouldContain("2023-06-15 14:"); // date + time portion (serial rounding may shift seconds)
    }

    [Fact]
    public void Export_QuoteAllFields_WrapsEverything()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        var sheet = document.Sheets[0];
        sheet.SetValue(1, 1, 5.0);
        sheet.SetValue(1, 2, true);

        var text = Encoding.UTF8.GetString(sheet.ToCsv(new CsvSaveOptions { QuoteAllFields = true }));
        text.ShouldContain("\"5\"");
        text.ShouldContain("\"TRUE\"");
    }

    [Fact]
    public void Export_FieldWithEmbeddedNewlineAndQuote_IsEscaped()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        document.Sheets[0].SetValue(1, 1, "line1\nhas \"quote\"");

        var text = Encoding.UTF8.GetString(document.Sheets[0].ToCsv());
        text.ShouldContain("\"\"quote\"\""); // doubled quotes
    }

    // ── PivotCacheValue formula-cell branches ────────────────────────────────

    [Fact]
    public async Task PivotCache_FormulaCells_AllResultKinds()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        var sheet = document.Sheets[0];
        sheet.SetValue(1, 1, "Key");
        sheet.SetValue(1, 2, "Value");

        // Formula with cached number.
        sheet.SetValue(2, 1, "a");
        sheet.SetFormulaWithCache(2, 2, "=1+1", 2);

        // Formula with cached text in the key column.
        var textFormula = sheet[3, 1];
        textFormula.FormulaText = "=\"b\"";
        textFormula.SetFormulaCachedText("b");
        sheet.SetValue(3, 2, 10.0);

        // Formula with cached error in the key column → blank cache value.
        var errFormula = sheet[4, 1];
        errFormula.FormulaText = "=1/0";
        errFormula.SetFormulaCachedError(CellError.DivisionByZero);
        sheet.SetValue(4, 2, 20.0);

        var pivot = sheet.AddPivotTable(CellRange.FromA1("A1:B4"), CellReference.FromA1("E1"), "P");
        pivot.AddRowField("Key");
        pivot.AddDataField("Value");

        var bytes = await XlsxFixtures.SaveBytesAsync(document);
#if NET10_0_OR_GREATER
        await using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
#else
        using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
#endif
        archive.Entries.Any(static e => e.FullName.Contains("pivotCacheRecords")).ShouldBeTrue();
    }

    [Fact]
    public void PivotCollection_Enumeration()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        var sheet = document.Sheets[0];
        sheet.SetValue(1, 1, "K");
        sheet.SetValue(1, 2, "V");
        sheet.SetValue(2, 1, "a");
        sheet.SetValue(2, 2, 1.0);
        sheet.AddPivotTable(CellRange.FromA1("A1:B2"), CellReference.FromA1("E1"), "P1");

        // ReSharper disable once UseCollectionCountProperty
        var count = sheet.PivotTables.Count();
        count.ShouldBe(1);
        sheet.PivotTables.AsEnumerable().Count().ShouldBe(1);
    }
}
