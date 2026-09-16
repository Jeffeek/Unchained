using Shouldly;
using Unchained.Xlsx.Abstractions;
using Unchained.Xlsx.Engine;
using Unchained.Xlsx.Models.Cell;
using Xunit;

namespace Unchained.Xlsx.Tests.IntegrationTests;

/// <summary>
///     Tests that exercise WorksheetWriter.SheetData by creating documents with edge cases,
///     saving, reloading, and verifying correct XML generation.
///     Targets: cell types, formulas, sparse data, large sheets.
/// </summary>
public sealed class WorksheetWriterRegressionTests
{
    [Fact]
    public async Task Writer_ErrorCells_WritesCorrectly()
    {
        using var processor = new SpreadsheetProcessor();
        byte[] bytes;

        using (var doc = processor.CreateBlank("Errors"))
        {
            var sheet = doc.Sheets[0];

            // Create formulas that result in errors
            sheet.SetFormula(1, 1, "=1/0");        // #DIV/0!
            sheet.SetFormula(2, 1, "=SQRT(-1)");   // #NUM!
            sheet.SetFormula(3, 1, "=1+\"text\""); // #VALUE!

            doc.Recalculate();
            bytes = await SaveToBytes(processor, doc);
        }

        using (var doc = await LoadFromBytes(processor, bytes))
        {
            var sheet = doc.Sheets[0];

            // Errors should be preserved
            sheet.GetCell(1, 1).ShouldNotBeNull();
            sheet.GetCell(2, 1).ShouldNotBeNull();
            sheet.GetCell(3, 1).ShouldNotBeNull();
        }
    }

    [Fact]
    public async Task Writer_ColumnProperties_PreservesSettings()
    {
        using var processor = new SpreadsheetProcessor();
        byte[] bytes;

        using (var doc = processor.CreateBlank("Columns"))
        {
            var sheet = doc.Sheets[0];

            // Set column widths using GetOrCreateColumn
            sheet.Columns.GetOrCreateColumn(1).Width = 25.5;
            sheet.Columns.GetOrCreateColumn(2).Width = 15.0;

            // Add some data so columns are written
            sheet.SetValue(1, 1, "Data1");
            sheet.SetValue(1, 2, "Data2");

            bytes = await SaveToBytes(processor, doc);
        }

        using (var doc = await LoadFromBytes(processor, bytes))
        {
            var sheet = doc.Sheets[0];

            // Access by column number
            var col1 = sheet.Columns.GetColumn(1);
            col1.ShouldNotBeNull();
            col1!.Width.ShouldBe(25.5);

            var col2 = sheet.Columns.GetColumn(2);
            col2.ShouldNotBeNull();
            col2!.Width.ShouldBe(15.0);
        }
    }

    [Fact]
    public async Task Writer_RowProperties_PreservesHeight()
    {
        using var processor = new SpreadsheetProcessor();
        byte[] bytes;

        using (var doc = processor.CreateBlank("Rows"))
        {
            var sheet = doc.Sheets[0];

            // Set row heights using GetOrCreateRow
            sheet.Rows.GetOrCreateRow(1).Height = 30.0;
            sheet.Rows.GetOrCreateRow(2).Height = 20.5;

            // Add data to ensure rows exist
            sheet.SetValue(1, 1, "Row1");
            sheet.SetValue(2, 1, "Row2");

            bytes = await SaveToBytes(processor, doc);
        }

        using (var doc = await LoadFromBytes(processor, bytes))
        {
            var sheet = doc.Sheets[0];

            // Access by row number, not collection index
            var row1 = sheet.Rows.GetRow(1);
            row1.ShouldNotBeNull();
            row1.Height.ShouldBe(30.0);

            var row2 = sheet.Rows.GetRow(2);
            row2.ShouldNotBeNull();
            row2.Height.ShouldBe(20.5);
        }
    }

    [Fact]
    public async Task Writer_EmptyCells_DoesNotWrite()
    {
        using var processor = new SpreadsheetProcessor();
        byte[] bytes;

        using (var doc = processor.CreateBlank("Sparse"))
        {
            var sheet = doc.Sheets[0];

            // Set values in sparse pattern
            sheet.SetValue(1, 1, "A1");
            sheet.SetValue(1, 10, "J1");
            sheet.SetValue(100, 1, "A100");

            bytes = await SaveToBytes(processor, doc);
        }

        // File should be small - empty cells shouldn't be written
        bytes.Length.ShouldBeLessThan(100_000);

        using (var doc = await LoadFromBytes(processor, bytes))
        {
            var sheet = doc.Sheets[0];

            sheet.GetCell(1, 1)!.GetString().ShouldBe("A1");
            sheet.GetCell(1, 10)!.GetString().ShouldBe("J1");
            sheet.GetCell(100, 1)!.GetString().ShouldBe("A100");

            // Empty cells should be null
            sheet.GetCell(1, 2).ShouldBeNull();
            sheet.GetCell(50, 50).ShouldBeNull();
        }
    }

    [Fact]
    public async Task Writer_FormulasWithCachedValues_WritesCache()
    {
        using var processor = new SpreadsheetProcessor();
        byte[] bytes;

        using (var doc = processor.CreateBlank("Cached"))
        {
            var sheet = doc.Sheets[0];

            sheet.SetValue(1, 1, 100);
            sheet.SetFormula(1, 2, "=A1*2");

            doc.Recalculate(); // Calculate so cached value is set

            bytes = await SaveToBytes(processor, doc);
        }

        using (var doc = await LoadFromBytes(processor, bytes))
        {
            var sheet = doc.Sheets[0];

            var cell = sheet.GetCell(1, 2)!;
            cell.Formula.ShouldBe("A1*2");
            cell.GetDouble().ShouldBe(200.0); // Cached value
        }
    }

    [Fact]
    public async Task Writer_MultipleMergedRanges_WritesAll()
    {
        using var processor = new SpreadsheetProcessor();
        byte[] bytes;

        using (var doc = processor.CreateBlank("Merged"))
        {
            var sheet = doc.Sheets[0];

            sheet.SetValue(1, 1, "Merge1");
            sheet.MergeCells(CellRange.FromA1("A1:C1"));

            sheet.SetValue(3, 1, "Merge2");
            sheet.MergeCells(CellRange.FromA1("A3:C5"));

            sheet.SetValue(7, 5, "Merge3");
            sheet.MergeCells(CellRange.FromA1("E7:G7"));

            bytes = await SaveToBytes(processor, doc);
        }

        using (var doc = await LoadFromBytes(processor, bytes))
        {
            var sheet = doc.Sheets[0];

            sheet.GetCell(1, 1)!.GetString().ShouldBe("Merge1");
            sheet.GetCell(3, 1)!.GetString().ShouldBe("Merge2");
            sheet.GetCell(7, 5)!.GetString().ShouldBe("Merge3");
        }
    }

    [Fact]
    public async Task Writer_VeryLargeSheet_HandlesEfficiently()
    {
        using var processor = new SpreadsheetProcessor();
        byte[] bytes;

        using (var doc = processor.CreateBlank("Large"))
        {
            var sheet = doc.Sheets[0];

            // Write 10,000 cells
            for (var row = 1; row <= 100; row++)
            {
                for (var col = 1; col <= 100; col++)
                    sheet.SetValue(row, col, (row * 1000) + col);
            }

            bytes = await SaveToBytes(processor, doc);
        }

        // Should compress well
        bytes.Length.ShouldBeLessThan(5_000_000); // Less than 5MB

        using (var doc = await LoadFromBytes(processor, bytes))
        {
            var sheet = doc.Sheets[0];

            // Spot check
            sheet.GetCell(1, 1)!.GetDouble().ShouldBe(1001.0);
            sheet.GetCell(50, 50)!.GetDouble().ShouldBe(50050.0);
            sheet.GetCell(100, 100)!.GetDouble().ShouldBe(100100.0);
        }
    }

    [Fact]
    public async Task Writer_NestedFormulas_WritesCorrectly()
    {
        using var processor = new SpreadsheetProcessor();
        byte[] bytes;

        using (var doc = processor.CreateBlank("Nested"))
        {
            var sheet = doc.Sheets[0];

            sheet.SetValue(1, 1, 10);
            sheet.SetValue(2, 1, 20);

            sheet.SetFormula(1, 2, "=IF(A1>15,SUM(A1:A2),AVERAGE(A1:A2))");
            sheet.SetFormula(2, 2, "=ROUND(SUM(A1:A2)/3,2)");

            doc.Recalculate();
            bytes = await SaveToBytes(processor, doc);
        }

        using (var doc = await LoadFromBytes(processor, bytes))
        {
            var sheet = doc.Sheets[0];
            doc.Recalculate();

            sheet.GetCell(1, 2)!.GetDouble().ShouldBe(15.0); // AVERAGE(10,20)
            sheet.GetCell(2, 2)!.GetDouble()!.Value.ShouldBe(10.0, 0.01); // ROUND(30/3)
        }
    }

    [Fact]
    public async Task Writer_ZeroValues_WritesZero()
    {
        using var processor = new SpreadsheetProcessor();
        byte[] bytes;

        using (var doc = processor.CreateBlank("Zeros"))
        {
            var sheet = doc.Sheets[0];

            sheet.SetValue(1, 1, 0);
            sheet.SetValue(2, 1, 0.0);
            sheet.SetValue(3, 1, -0.0);

            bytes = await SaveToBytes(processor, doc);
        }

        using (var doc = await LoadFromBytes(processor, bytes))
        {
            var sheet = doc.Sheets[0];

            sheet.GetCell(1, 1)!.GetDouble().ShouldBe(0.0);
            sheet.GetCell(2, 1)!.GetDouble().ShouldBe(0.0);
            sheet.GetCell(3, 1)!.GetDouble().ShouldBe(0.0);
        }
    }

    [Fact]
    public async Task Writer_EmptyStrings_WritesCorrectly()
    {
        using var processor = new SpreadsheetProcessor();
        byte[] bytes;

        using (var doc = processor.CreateBlank("Empty"))
        {
            var sheet = doc.Sheets[0];

            sheet.SetValue(1, 1, "");
            sheet.SetValue(2, 1, string.Empty);
            sheet.SetValue(3, 1, "   "); // Only spaces

            bytes = await SaveToBytes(processor, doc);
        }

        using (var doc = await LoadFromBytes(processor, bytes))
        {
            var sheet = doc.Sheets[0];

            sheet.GetCell(1, 1)!.GetString().ShouldBe("");
            sheet.GetCell(2, 1)!.GetString().ShouldBe("");
            sheet.GetCell(3, 1)!.GetString().ShouldBe("   ");
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
