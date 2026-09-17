using Shouldly;
using Unchained.Xlsx.Abstractions;
using Unchained.Xlsx.Engine;
using Unchained.Xlsx.Models.Cell;
using Unchained.Xlsx.Models.Tables;
using Xunit;

namespace Unchained.Xlsx.Tests.IntegrationTests;

/// <summary>
///     Tests that exercise TableParser by creating tables with various features,
///     saving, reloading, and verifying correct parsing.
///     Targets: table columns, totals functions, style info, formulas.
/// </summary>
public sealed class TableParserRegressionTests
{
    [Fact]
    public async Task Parser_TableWithAllTotalsFunctions_ParsesCorrectly()
    {
        using var processor = new SpreadsheetProcessor();
        byte[] bytes;

        using (var doc = processor.CreateBlank("Tables"))
        {
            var sheet = doc.Sheets[0];

            // Create table with headers
            sheet.SetValue(1, 1, "Col1");
            sheet.SetValue(1, 2, "Col2");
            sheet.SetValue(1, 3, "Col3");
            sheet.SetValue(1, 4, "Col4");
            sheet.SetValue(1, 5, "Col5");
            sheet.SetValue(1, 6, "Col6");
            sheet.SetValue(1, 7, "Col7");
            sheet.SetValue(1, 8, "Col8");

            // Add data rows
            for (var row = 2; row <= 5; row++)
            {
                for (var col = 1; col <= 8; col++)
                    sheet.SetValue(row, col, row * col);
            }

            var table = sheet.Tables.Add(CellRange.FromA1("A1:H5"), "TestTable");
            table.ShowTotalsRow = true;

            // Set different totals functions for each column
            table.Columns[0].TotalsFunction = TotalsRowFunction.Sum;
            table.Columns[1].TotalsFunction = TotalsRowFunction.Average;
            table.Columns[2].TotalsFunction = TotalsRowFunction.Count;
            table.Columns[3].TotalsFunction = TotalsRowFunction.Max;
            table.Columns[4].TotalsFunction = TotalsRowFunction.Min;
            table.Columns[5].TotalsFunction = TotalsRowFunction.StdDev;
            table.Columns[6].TotalsFunction = TotalsRowFunction.Var;
            table.Columns[7].TotalsFunction = TotalsRowFunction.CountNumbers;

            bytes = await SaveToBytes(processor, doc);
        }

        using (var doc = await LoadFromBytes(processor, bytes))
        {
            var sheet = doc.Sheets[0];
            sheet.Tables.Count.ShouldBe(1);

            var table = sheet.Tables[0];
            table.ShowTotalsRow.ShouldBeTrue();
            table.Columns.Count.ShouldBe(8);

            // Verify each totals function was preserved
            table.Columns[0].TotalsFunction.ShouldBe(TotalsRowFunction.Sum);
            table.Columns[1].TotalsFunction.ShouldBe(TotalsRowFunction.Average);
            table.Columns[2].TotalsFunction.ShouldBe(TotalsRowFunction.Count);
            table.Columns[3].TotalsFunction.ShouldBe(TotalsRowFunction.Max);
            table.Columns[4].TotalsFunction.ShouldBe(TotalsRowFunction.Min);
            table.Columns[5].TotalsFunction.ShouldBe(TotalsRowFunction.StdDev);
            table.Columns[6].TotalsFunction.ShouldBe(TotalsRowFunction.Var);
            table.Columns[7].TotalsFunction.ShouldBe(TotalsRowFunction.CountNumbers);
        }
    }

    [Fact]
    public async Task Parser_TableWithCustomTotalsFormula_ParsesCorrectly()
    {
        using var processor = new SpreadsheetProcessor();
        byte[] bytes;

        using (var doc = processor.CreateBlank("CustomTotals"))
        {
            var sheet = doc.Sheets[0];

            sheet.SetValue(1, 1, "Amount");
            sheet.SetValue(1, 2, "Custom");
            sheet.SetValue(2, 1, 100);
            sheet.SetValue(2, 2, 200);

            var table = sheet.Tables.Add(CellRange.FromA1("A1:B2"), "CustomTable");
            table.ShowTotalsRow = true;

            // Set custom function with formula
            table.Columns[1].TotalsFunction = TotalsRowFunction.Custom;
            table.Columns[1].TotalsFormula = "SUM(CustomTable[Custom])*2";

            bytes = await SaveToBytes(processor, doc);
        }

        using (var doc = await LoadFromBytes(processor, bytes))
        {
            var sheet = doc.Sheets[0];
            var table = sheet.Tables[0];

            table.Columns[1].TotalsFunction.ShouldBe(TotalsRowFunction.Custom);
            table.Columns[1].TotalsFormula.ShouldNotBeNullOrEmpty();
        }
    }

    [Fact]
    public async Task Parser_TableWithTotalsLabel_ParsesCorrectly()
    {
        using var processor = new SpreadsheetProcessor();
        byte[] bytes;

        using (var doc = processor.CreateBlank("Labels"))
        {
            var sheet = doc.Sheets[0];

            sheet.SetValue(1, 1, "Name");
            sheet.SetValue(1, 2, "Value");
            sheet.SetValue(2, 1, "Item1");
            sheet.SetValue(2, 2, 100);

            var table = sheet.Tables.Add(CellRange.FromA1("A1:B2"), "LabelTable");
            table.ShowTotalsRow = true;

            table.Columns[0].TotalsLabel = "Grand Total:";
            table.Columns[1].TotalsFunction = TotalsRowFunction.Sum;

            bytes = await SaveToBytes(processor, doc);
        }

        using (var doc = await LoadFromBytes(processor, bytes))
        {
            var sheet = doc.Sheets[0];
            var table = sheet.Tables[0];

            table.Columns[0].TotalsLabel.ShouldBe("Grand Total:");
            table.Columns[1].TotalsFunction.ShouldBe(TotalsRowFunction.Sum);
        }
    }

    [Fact]
    public async Task Parser_TableWithStyleProperties_ParsesAll()
    {
        using var processor = new SpreadsheetProcessor();
        byte[] bytes;

        using (var doc = processor.CreateBlank("Styled"))
        {
            var sheet = doc.Sheets[0];

            sheet.SetValue(1, 1, "A");
            sheet.SetValue(1, 2, "B");
            sheet.SetValue(1, 3, "C");
            sheet.SetValue(2, 1, 1);
            sheet.SetValue(2, 2, 2);
            sheet.SetValue(2, 3, 3);

            var table = sheet.Tables.Add(CellRange.FromA1("A1:C2"), "StyledTable");

            // Set all style properties
            table.StyleName = "TableStyleMedium2";
            table.ShowFirstColumn = true;
            table.ShowLastColumn = true;
            table.ShowBandedRows = true;
            table.ShowBandedColumns = true;

            bytes = await SaveToBytes(processor, doc);
        }

        using (var doc = await LoadFromBytes(processor, bytes))
        {
            var sheet = doc.Sheets[0];
            var table = sheet.Tables[0];

            table.StyleName.ShouldBe("TableStyleMedium2");
            table.ShowFirstColumn.ShouldBeTrue();
            table.ShowLastColumn.ShouldBeTrue();
            table.ShowBandedRows.ShouldBeTrue();
            table.ShowBandedColumns.ShouldBeTrue();
        }
    }

    [Fact]
    public async Task Parser_TableWithHeaderRowDisabled_ParsesCorrectly()
    {
        using var processor = new SpreadsheetProcessor();
        byte[] bytes;

        using (var doc = processor.CreateBlank("NoHeader"))
        {
            var sheet = doc.Sheets[0];

            sheet.SetValue(1, 1, 1);
            sheet.SetValue(1, 2, 2);
            sheet.SetValue(2, 1, 3);
            sheet.SetValue(2, 2, 4);

            var table = sheet.Tables.Add(CellRange.FromA1("A1:B2"), "NoHeaderTable");
            table.ShowHeaderRow = false;

            bytes = await SaveToBytes(processor, doc);
        }

        using (var doc = await LoadFromBytes(processor, bytes))
        {
            var sheet = doc.Sheets[0];
            var table = sheet.Tables[0];

            table.ShowHeaderRow.ShouldBeFalse();
        }
    }

    [Fact]
    public async Task Parser_TableWithDisplayName_ParsesCorrectly()
    {
        using var processor = new SpreadsheetProcessor();
        byte[] bytes;

        using (var doc = processor.CreateBlank("DisplayName"))
        {
            var sheet = doc.Sheets[0];

            sheet.SetValue(1, 1, "Col1");
            sheet.SetValue(2, 1, "Data");

            var table = sheet.Tables.Add(CellRange.FromA1("A1:A2"), "InternalName");
            table.DisplayName = "User Friendly Name";

            bytes = await SaveToBytes(processor, doc);
        }

        using (var doc = await LoadFromBytes(processor, bytes))
        {
            var sheet = doc.Sheets[0];
            var table = sheet.Tables[0];

            table.Name.ShouldBe("InternalName");
            table.DisplayName.ShouldBe("User Friendly Name");
        }
    }

    [Fact]
    public async Task Parser_TableWithCalculatedColumn_ParsesFormula()
    {
        using var processor = new SpreadsheetProcessor();
        byte[] bytes;

        using (var doc = processor.CreateBlank("Calculated"))
        {
            var sheet = doc.Sheets[0];

            sheet.SetValue(1, 1, "Price");
            sheet.SetValue(1, 2, "Tax");
            sheet.SetValue(2, 1, 100);
            sheet.SetFormula(2, 2, "=[@Price]*0.1");

            var table = sheet.Tables.Add(CellRange.FromA1("A1:B2"), "CalcTable");
            table.Columns[1].ColumnFormula = "[@Price]*0.1";

            bytes = await SaveToBytes(processor, doc);
        }

        using (var doc = await LoadFromBytes(processor, bytes))
        {
            var sheet = doc.Sheets[0];
            var table = sheet.Tables[0];

            table.Columns[1].ColumnFormula.ShouldNotBeNullOrEmpty();
        }
    }

    [Fact]
    public async Task Parser_MultipleTablesOnSheet_ParsesAll()
    {
        using var processor = new SpreadsheetProcessor();
        byte[] bytes;

        using (var doc = processor.CreateBlank("MultipleTables"))
        {
            var sheet = doc.Sheets[0];

            // Table 1
            sheet.SetValue(1, 1, "T1Col");
            sheet.SetValue(2, 1, "Data1");
            sheet.Tables.Add(CellRange.FromA1("A1:A2"), "Table1");

            // Table 2
            sheet.SetValue(1, 3, "T2Col");
            sheet.SetValue(2, 3, "Data2");
            sheet.Tables.Add(CellRange.FromA1("C1:C2"), "Table2");

            // Table 3
            sheet.SetValue(4, 1, "T3Col");
            sheet.SetValue(5, 1, "Data3");
            sheet.Tables.Add(CellRange.FromA1("A4:A5"), "Table3");

            bytes = await SaveToBytes(processor, doc);
        }

        using (var doc = await LoadFromBytes(processor, bytes))
        {
            var sheet = doc.Sheets[0];

            sheet.Tables.Count.ShouldBe(3);
            sheet.Tables[0].Name.ShouldBe("Table1");
            sheet.Tables[1].Name.ShouldBe("Table2");
            sheet.Tables[2].Name.ShouldBe("Table3");
        }
    }

    [Fact]
    public async Task Parser_TableWithNoBandedRows_ParsesCorrectly()
    {
        using var processor = new SpreadsheetProcessor();
        byte[] bytes;

        using (var doc = processor.CreateBlank("NoBands"))
        {
            var sheet = doc.Sheets[0];

            sheet.SetValue(1, 1, "Col");
            sheet.SetValue(2, 1, "Data");

            var table = sheet.Tables.Add(CellRange.FromA1("A1:A2"), "NoBandTable");
            table.ShowBandedRows = false;
            table.ShowBandedColumns = false;

            bytes = await SaveToBytes(processor, doc);
        }

        using (var doc = await LoadFromBytes(processor, bytes))
        {
            var sheet = doc.Sheets[0];
            var table = sheet.Tables[0];

            table.ShowBandedRows.ShouldBeFalse();
            table.ShowBandedColumns.ShouldBeFalse();
        }
    }

    // Helper methods
    private static async Task<byte[]> SaveToBytes(ISpreadsheetProcessor processor, SpreadsheetDocument doc)
    {
        using var stream = new MemoryStream();
        await processor.SaveAsync(doc, stream, cancellationToken: TestContext.Current.CancellationToken);
        return stream.ToArray();
    }

    private static async Task<SpreadsheetDocument> LoadFromBytes(ISpreadsheetProcessor processor, byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        return await processor.LoadAsync(stream, cancellationToken: TestContext.Current.CancellationToken);
    }
}
