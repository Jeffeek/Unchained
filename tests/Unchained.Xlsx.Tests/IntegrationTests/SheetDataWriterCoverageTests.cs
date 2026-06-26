using System.IO.Compression;
using Shouldly;
using Unchained.Xlsx.Models.Cell;
using Unchained.Xlsx.Tests.Helpers;
using Xunit;

namespace Unchained.Xlsx.Tests.IntegrationTests;

/// <summary>Targets the <c>WorksheetWriter.SheetData</c> branches: row style, array/cached-error formulas, re-save.</summary>
public class SheetDataWriterCoverageTests
{
    [Fact]
    public async Task RowStyleIndex_RoundTrips()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        var sheet = document.Sheets[0];
        sheet.SetValue(1, 1, 1.0);
        sheet.Rows.GetOrCreateRow(1).StyleIndex = 0;
        // Row 2 carries an explicit style + custom format flag.
        var row2 = sheet.Rows.GetOrCreateRow(2);
        sheet.SetValue(2, 1, 2.0);
        row2.StyleIndex = 0;

        using var reloaded = await XlsxFixtures.RoundTripAsync(document);
        reloaded.Sheets[0].GetCell(2, 1)!.GetDouble().ShouldBe(2.0);
    }

    [Fact]
    public async Task ArrayFormula_Write_RoundTrips()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        var sheet = document.Sheets[0];
        var cell = sheet[1, 1];
        cell.FormulaText = "=SUM(B1:B2)";
        cell.IsArrayFormula = true;
        cell.ArrayFormulaRange = CellRange.FromA1("A1:A2");

        using var reloaded = await XlsxFixtures.RoundTripAsync(document);
        var result = reloaded.Sheets[0].GetCell(1, 1);
        result!.IsArrayFormula.ShouldBeTrue();
        result.ArrayFormulaRange.ShouldNotBeNull();
    }

    [Fact]
    public async Task CachedErrorFormula_Write_RoundTrips()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        var cell = document.Sheets[0][1, 1];
        cell.FormulaText = "=1/0";
        cell.SetFormulaCachedError(CellError.DivisionByZero);

        using var reloaded = await XlsxFixtures.RoundTripAsync(document);
        var result = reloaded.Sheets[0].GetCell(1, 1);
        result!.CellType.ShouldBe(CellType.Formula);
        result.GetError().ShouldBe(CellError.DivisionByZero);
    }

    [Fact]
    public async Task CachedTextFormula_Write_RoundTrips()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        var cell = document.Sheets[0][1, 1];
        cell.FormulaText = "=\"x\"";
        cell.SetFormulaCachedText("x");

        using var reloaded = await XlsxFixtures.RoundTripAsync(document);
        reloaded.Sheets[0].GetCell(1, 1)!.GetString().ShouldBe("x");
    }

    [Fact]
    public async Task ReSave_OfReloadedDocument_UpdatesExistingElements()
    {
        // First round-trip writes fresh elements; re-saving the reloaded doc (which has the
        // existing <dimension>/<sheetData> from the prior save) exercises the update-existing branches.
        using var document = XlsxFixtures.WithSheets("Data");
        var sheet = document.Sheets[0];
        sheet.SetValue(1, 1, "first");
        sheet.SetColumnWidth(1, 15);
        sheet.MergeCells(CellRange.FromA1("A1:B1"));

        using var once = await XlsxFixtures.RoundTripAsync(document);
        // Touch the materialised collections so the writer rewrites them again.
        var onceSheet = once.Sheets[0];
        onceSheet.SetValue(2, 1, "second");
        _ = onceSheet.Columns;
        _ = onceSheet.MergedCells;

        using var twice = await XlsxFixtures.RoundTripAsync(once);
        var result = twice.Sheets[0];
        result.GetCell(1, 1)!.GetString().ShouldBe("first");
        result.GetCell(2, 1)!.GetString().ShouldBe("second");
    }

    [Fact]
    public async Task MaterialisedButEmpty_Collections_ProduceValidFile()
    {
        // Materialise columns/merge/validations without adding anything → writer takes the empty paths.
        using var document = XlsxFixtures.WithSheets("Data");
        var sheet = document.Sheets[0];
        _ = sheet.Columns;        // materialise, leave empty
        _ = sheet.MergedCells;    // materialise, leave empty
        _ = sheet.DataValidations;
        _ = sheet.Rows;           // cells materialised but empty

        var bytes = await XlsxFixtures.SaveBytesAsync(document);
        using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        archive.GetEntry("xl/worksheets/sheet1.xml").ShouldNotBeNull();
    }

    [Fact]
    public async Task ColumnProperties_AllFlags_RoundTrip()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        var sheet = document.Sheets[0];
        var column = sheet.Columns.GetOrCreateColumn(2);
        column.Width = 18;
        column.IsCustomWidth = true;
        column.IsHidden = true;
        column.IsCollapsed = true;
        column.OutlineLevel = 1;
        column.StyleIndex = 0;

        using var reloaded = await XlsxFixtures.RoundTripAsync(document);
        var result = reloaded.Sheets[0].GetColumn(2);
        result.ShouldNotBeNull();
        result.Width.ShouldBe(18);
        result.IsHidden.ShouldBeTrue();
        result.OutlineLevel.ShouldBe(1);
    }
}
