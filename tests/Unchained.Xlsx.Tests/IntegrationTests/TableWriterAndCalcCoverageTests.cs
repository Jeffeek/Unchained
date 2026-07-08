using Shouldly;
using Unchained.Xlsx.Models.Cell;
using Unchained.Xlsx.Models.Tables;
using Unchained.Xlsx.Tests.Helpers;
using Xunit;

namespace Unchained.Xlsx.Tests.IntegrationTests;

/// <summary>Coverage for table totals-row serialization and formula-calculator value kinds.</summary>
public class TableWriterAndCalcCoverageTests
{
    [Fact]
    public async Task Table_TotalsRowFunctions_RoundTrip()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        var sheet = document.Sheets[0];
        sheet.SetValue(1, 1, "Item");
        sheet.SetValue(1, 2, "Qty");
        sheet.SetValue(2, 1, "A");
        sheet.SetValue(2, 2, 5.0);

        var table = sheet.AddTable(CellRange.FromA1("A1:B2"));
        table.ShowTotalsRow = true;
        table.ShowFirstColumn = true;
        table.ShowLastColumn = true;
        table.ShowBandedColumns = true;
        table.Columns[0].TotalsLabel = "Total";
        table.Columns[1].TotalsFunction = TotalsRowFunction.Sum;

        using var reloaded = await XlsxFixtures.RoundTripAsync(document);
        var reloadedTable = reloaded.Sheets[0].Tables[0];
        reloadedTable.ShowTotalsRow.ShouldBeTrue();
        reloadedTable.Columns[1].TotalsFunction.ShouldBe(TotalsRowFunction.Sum);
        reloadedTable.Columns[0].TotalsLabel.ShouldBe("Total");
        reloadedTable.ShowFirstColumn.ShouldBeTrue();
        reloadedTable.ShowLastColumn.ShouldBeTrue();
        reloadedTable.ShowBandedColumns.ShouldBeTrue();
    }

    [Fact]
    public async Task Table_NoHeaderRow_RoundTrips()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        var sheet = document.Sheets[0];
        sheet.SetValue(1, 1, 1.0);
        sheet.AddTable(CellRange.FromA1("A1:A3"), false);

        using var reloaded = await XlsxFixtures.RoundTripAsync(document);
        reloaded.Sheets[0].Tables[0].ShowHeaderRow.ShouldBeFalse();
    }

    [Fact]
    public async Task Table_CalculatedColumnFormula_RoundTrips()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        var sheet = document.Sheets[0];
        sheet.SetValue(1, 1, "Base");
        sheet.SetValue(1, 2, "Double");
        sheet.SetValue(2, 1, 3.0);
        var table = sheet.AddTable(CellRange.FromA1("A1:B2"));
        table.Columns[1].ColumnFormula = "[Base]*2";
        table.Columns[1].TotalsFunction = TotalsRowFunction.Custom;
        table.Columns[1].TotalsFormula = "SUBTOTAL(109,[Double])";

        using var reloaded = await XlsxFixtures.RoundTripAsync(document);
        var reloadedColumn = reloaded.Sheets[0].Tables[0].Columns[1];
        reloadedColumn.ColumnFormula.ShouldBe("[Base]*2");
        reloadedColumn.TotalsFunction.ShouldBe(TotalsRowFunction.Custom);
    }

    // ── FormulaCalculator value kinds ────────────────────────────────────────

    [Fact]
    public void Recalculate_BooleanResult()
    {
        using var document = XlsxFixtures.WithSheets("S");
        var sheet = document.Sheets[0];
        sheet.SetValue(1, 1, 5.0);
        sheet.SetFormula(2, 1, "=A1>3");
        document.Recalculate();

        var cell = sheet.GetCell(2, 1);
        cell.ShouldNotBeNull();
        cell.CellType.ShouldBe(CellType.Boolean);
        cell.GetBoolean().ShouldBe(true);
    }

    [Fact]
    public void Recalculate_ErrorResult()
    {
        using var document = XlsxFixtures.WithSheets("S");
        var sheet = document.Sheets[0];
        sheet.SetFormula(1, 1, "=1/0");
        document.Recalculate();

        sheet.GetCell(1, 1)!.GetError().ShouldBe(CellError.DivisionByZero);
    }

    [Fact]
    public void Recalculate_ArrayResult_CachesTopLeft()
    {
        using var document = XlsxFixtures.WithSheets("S");
        var sheet = document.Sheets[0];
        sheet.SetValue(1, 1, 10.0);
        sheet.SetValue(2, 1, 20.0);
        // A range reference yields an array; the calculator caches the top-left scalar.
        sheet.SetFormula(1, 2, "=A1:A2");
        document.Recalculate();

        sheet.GetCell(1, 2)!.GetDouble().ShouldBe(10);
    }

    [Fact]
    public void Recalculate_BlankResult()
    {
        using var document = XlsxFixtures.WithSheets("S");
        var sheet = document.Sheets[0];
        sheet.SetFormula(1, 1, "=A5"); // empty cell reference → blank
        document.Recalculate();

        // The calculator caches blank as 0.0 (known limitation — blank results are stored as Number=0).
        var cell = sheet.GetCell(1, 1);
        cell.ShouldNotBeNull();
        cell.GetDouble().ShouldBe(0);
    }

    [Fact]
    public void Recalculate_NoFormulas_ReturnsZero()
    {
        using var document = XlsxFixtures.WithSheets("S");
        document.Sheets[0].SetValue(1, 1, 5.0);
        document.Recalculate().ShouldBe(0);
    }
}
