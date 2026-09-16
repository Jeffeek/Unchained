using Shouldly;
using Unchained.Xlsx.Engine;
using Unchained.Xlsx.Models.Cell;
using Unchained.Xlsx.Models.Tables;
using Xunit;

namespace Unchained.Xlsx.Tests.IntegrationTests;

/// <summary>
///     Integration tests for Excel Tables (ListObject) functionality.
///     Exercises table creation, column management, and totals row features.
/// </summary>
public sealed class ExcelTablesTests
{
    [Fact]
    public void AddTable_BasicTable_CreatesTableWithColumns()
    {
        using var processor = new SpreadsheetProcessor();
        using var doc = processor.CreateBlank("Sheet1");
        var sheet = doc.Sheets[0];

        // Set up data
        sheet.SetValue(1, 1, "Name");
        sheet.SetValue(1, 2, "Value");
        sheet.SetValue(2, 1, "A");
        sheet.SetValue(2, 2, 100);

        var table = sheet.Tables.Add(CellRange.FromA1("A1:B2"), "MyTable");

        table.Name.ShouldBe("MyTable");
        table.Range.ToString().ShouldBe("A1:B2");
        table.Columns.Count.ShouldBe(2);
        table.Columns[0].Name.ShouldBe("Name");
        table.Columns[1].Name.ShouldBe("Value");
    }

    [Fact]
    public void AddTable_WithTotalsRow_EnablesTotalsFeatures()
    {
        using var processor = new SpreadsheetProcessor();
        using var doc = processor.CreateBlank("Sheet1");
        var sheet = doc.Sheets[0];

        sheet.SetValue(1, 1, "Item");
        sheet.SetValue(1, 2, "Amount");
        sheet.SetValue(2, 1, "A");
        sheet.SetValue(2, 2, 10);
        sheet.SetValue(3, 1, "B");
        sheet.SetValue(3, 2, 20);

        var table = sheet.Tables.Add(CellRange.FromA1("A1:B3"), "Sales");
        table.ShowTotalsRow = true;
        table.Columns[1].TotalsFunction = TotalsRowFunction.Sum;

        table.ShowTotalsRow.ShouldBeTrue();
        table.Columns[1].TotalsFunction.ShouldBe(TotalsRowFunction.Sum);
    }

    [Fact]
    public void TableColumn_SetTotalsFunction_UpdatesColumn()
    {
        using var processor = new SpreadsheetProcessor();
        using var doc = processor.CreateBlank("Sheet1");
        var sheet = doc.Sheets[0];

        sheet.SetValue(1, 1, "Values");
        sheet.SetValue(2, 1, 100);

        var table = sheet.Tables.Add(CellRange.FromA1("A1:A2"), "Data");
        var column = table.Columns[0];

        column.TotalsFunction = TotalsRowFunction.Average;
        column.TotalsFunction.ShouldBe(TotalsRowFunction.Average);

        column.TotalsFunction = TotalsRowFunction.Count;
        column.TotalsFunction.ShouldBe(TotalsRowFunction.Count);

        column.TotalsFunction = TotalsRowFunction.Max;
        column.TotalsFunction.ShouldBe(TotalsRowFunction.Max);

        column.TotalsFunction = TotalsRowFunction.Min;
        column.TotalsFunction.ShouldBe(TotalsRowFunction.Min);
    }

    [Fact]
    public void TableColumn_SetCustomTotals_UsesCustomFunction()
    {
        using var processor = new SpreadsheetProcessor();
        using var doc = processor.CreateBlank("Sheet1");
        var sheet = doc.Sheets[0];

        sheet.SetValue(1, 1, "Values");
        sheet.SetValue(2, 1, 100);

        var table = sheet.Tables.Add(CellRange.FromA1("A1:A2"), "Data");
        table.ShowTotalsRow = true;

        var column = table.Columns[0];
        column.TotalsFunction = TotalsRowFunction.Custom;
        column.TotalsFormula = "=SUM(Data[Values])*2";

        column.TotalsFunction.ShouldBe(TotalsRowFunction.Custom);
        column.TotalsFormula.ShouldBe("=SUM(Data[Values])*2");
    }

    [Fact]
    public void Table_StyleProperties_CanBeSet()
    {
        using var processor = new SpreadsheetProcessor();
        using var doc = processor.CreateBlank("Sheet1");
        var sheet = doc.Sheets[0];

        sheet.SetValue(1, 1, "Col1");
        sheet.SetValue(1, 2, "Col2");

        var table = sheet.Tables.Add(CellRange.FromA1("A1:B1"), "Styled");

        table.StyleName = "TableStyleMedium2";
        table.ShowFirstColumn = true;
        table.ShowLastColumn = true;
        table.ShowBandedRows = false;
        table.ShowBandedColumns = true;

        table.StyleName.ShouldBe("TableStyleMedium2");
        table.ShowFirstColumn.ShouldBeTrue();
        table.ShowLastColumn.ShouldBeTrue();
        table.ShowBandedRows.ShouldBeFalse();
        table.ShowBandedColumns.ShouldBeTrue();
    }

    [Fact]
    public void Table_MultipleColumns_AllTotalsFunctions()
    {
        using var processor = new SpreadsheetProcessor();
        using var doc = processor.CreateBlank("Sheet1");
        var sheet = doc.Sheets[0];

        sheet.SetValue(1, 1, "Col1");
        sheet.SetValue(1, 2, "Col2");
        sheet.SetValue(1, 3, "Col3");
        sheet.SetValue(1, 4, "Col4");
        sheet.SetValue(1, 5, "Col5");
        sheet.SetValue(1, 6, "Col6");

        var table = sheet.Tables.Add(CellRange.FromA1("A1:F1"), "AllFunctions");
        table.ShowTotalsRow = true;

        table.Columns[0].TotalsFunction = TotalsRowFunction.Sum;
        table.Columns[1].TotalsFunction = TotalsRowFunction.Average;
        table.Columns[2].TotalsFunction = TotalsRowFunction.StdDev;
        table.Columns[3].TotalsFunction = TotalsRowFunction.Var;
        table.Columns[4].TotalsFunction = TotalsRowFunction.CountNumbers;
        table.Columns[5].TotalsFunction = TotalsRowFunction.None;

        table.Columns[0].TotalsFunction.ShouldBe(TotalsRowFunction.Sum);
        table.Columns[1].TotalsFunction.ShouldBe(TotalsRowFunction.Average);
        table.Columns[2].TotalsFunction.ShouldBe(TotalsRowFunction.StdDev);
        table.Columns[3].TotalsFunction.ShouldBe(TotalsRowFunction.Var);
        table.Columns[4].TotalsFunction.ShouldBe(TotalsRowFunction.CountNumbers);
        table.Columns[5].TotalsFunction.ShouldBe(TotalsRowFunction.None);
    }

    [Fact]
    public void Table_HeaderRow_CanBeDisabled()
    {
        using var processor = new SpreadsheetProcessor();
        using var doc = processor.CreateBlank("Sheet1");
        var sheet = doc.Sheets[0];

        sheet.SetValue(1, 1, "Data");

        var table = sheet.Tables.Add(CellRange.FromA1("A1"), "NoHeader", hasHeaders: false);

        table.ShowHeaderRow.ShouldBeFalse();
    }
}
