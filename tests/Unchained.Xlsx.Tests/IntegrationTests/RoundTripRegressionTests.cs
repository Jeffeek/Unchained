using Shouldly;
using Unchained.Xlsx.Abstractions;
using Unchained.Xlsx.Engine;
using Unchained.Xlsx.Models.Cell;
using Xunit;

namespace Unchained.Xlsx.Tests.IntegrationTests;

/// <summary>
///     Round-trip regression tests: create documents with various features,
///     save, reload, and verify all features survive the cycle.
///     Exercises parsers, writers, and ensures data integrity.
/// </summary>
public sealed class RoundTripRegressionTests
{
    [Fact]
    public async Task RoundTrip_BasicFormulas_PreservesCalculatedValues()
    {
        using var processor = new SpreadsheetProcessor();
        byte[] bytes;

        // Create document with formulas
        using (var doc = processor.CreateBlank("Sheet1"))
        {
            var sheet = doc.Sheets[0];
            sheet.SetValue(1, 1, 10);
            sheet.SetValue(1, 2, 20);
            sheet.SetFormula(1, 3, "=A1+B1");
            sheet.SetFormula(2, 1, "=SUM(A1:B1)");
            sheet.SetFormula(2, 2, "=AVERAGE(A1:B1)");

            doc.Recalculate();
            bytes = await SaveToBytes(processor, doc);
        }

        // Reload and verify
        using (var doc = await LoadFromBytes(processor, bytes))
        {
            var sheet = doc.Sheets[0];

            sheet.GetCell(1, 1)!.GetDouble().ShouldBe(10.0);
            sheet.GetCell(1, 2)!.GetDouble().ShouldBe(20.0);
            sheet.GetCell(1, 3)!.Formula.ShouldBe("A1+B1");
            sheet.GetCell(1, 3)!.GetDouble().ShouldBe(30.0);

            sheet.GetCell(2, 1)!.Formula.ShouldBe("SUM(A1:B1)");
            sheet.GetCell(2, 1)!.GetDouble().ShouldBe(30.0);

            sheet.GetCell(2, 2)!.Formula.ShouldBe("AVERAGE(A1:B1)");
            sheet.GetCell(2, 2)!.GetDouble().ShouldBe(15.0);
        }
    }

    [Fact]
    public async Task RoundTrip_MixedDataTypes_PreservesTypes()
    {
        using var processor = new SpreadsheetProcessor();
        byte[] bytes;

        using (var doc = processor.CreateBlank("Data"))
        {
            var sheet = doc.Sheets[0];
            sheet.SetValue(1, 1, 42.5);
            sheet.SetValue(1, 2, "Hello");
            sheet.SetValue(1, 3, true);
            sheet.SetValue(1, 4, new DateTime(2024, 1, 15));

            bytes = await SaveToBytes(processor, doc);
        }

        using (var doc = await LoadFromBytes(processor, bytes))
        {
            var sheet = doc.Sheets[0];

            sheet.GetCell(1, 1)!.GetDouble().ShouldBe(42.5);
            sheet.GetCell(1, 2)!.GetString().ShouldBe("Hello");
            sheet.GetCell(1, 4)!.GetDateTime().ShouldBe(new DateTime(2024, 1, 15));
        }
    }

    [Fact]
    public async Task RoundTrip_MultipleSheets_PreservesStructure()
    {
        using var processor = new SpreadsheetProcessor();
        byte[] bytes;

        using (var doc = processor.CreateBlank("First"))
        {
            doc.Sheets.Add("Second");
            doc.Sheets.Add("Third");

            doc.Sheets[0].SetValue(1, 1, "Sheet1Data");
            doc.Sheets[1].SetValue(1, 1, "Sheet2Data");
            doc.Sheets[2].SetValue(1, 1, "Sheet3Data");

            bytes = await SaveToBytes(processor, doc);
        }

        using (var doc = await LoadFromBytes(processor, bytes))
        {
            doc.Sheets.Count.ShouldBe(3);
            doc.Sheets[0].Name.ShouldBe("First");
            doc.Sheets[1].Name.ShouldBe("Second");
            doc.Sheets[2].Name.ShouldBe("Third");

            doc.Sheets[0].GetCell(1, 1)!.GetString().ShouldBe("Sheet1Data");
            doc.Sheets[1].GetCell(1, 1)!.GetString().ShouldBe("Sheet2Data");
            doc.Sheets[2].GetCell(1, 1)!.GetString().ShouldBe("Sheet3Data");
        }
    }

    [Fact]
    public async Task RoundTrip_SharedFormulas_PreservesCalculations()
    {
        using var processor = new SpreadsheetProcessor();
        byte[] bytes;

        using (var doc = processor.CreateBlank("Shared"))
        {
            var sheet = doc.Sheets[0];

            // Set up data in column A
            for (var i = 1; i <= 5; i++)
                sheet.SetValue(i, 1, i * 10);

            // Formulas in column B referencing column A
            for (var i = 1; i <= 5; i++)
                sheet.SetFormula(i, 2, $"=A{i}*2");

            doc.Recalculate();
            bytes = await SaveToBytes(processor, doc);
        }

        using (var doc = await LoadFromBytes(processor, bytes))
        {
            var sheet = doc.Sheets[0];
            doc.Recalculate();

            // Verify all formulas calculate correctly
            for (var i = 1; i <= 5; i++)
                sheet.GetCell(i, 2)!.GetDouble().ShouldBe(i * 10 * 2);
        }
    }

    [Fact]
    public async Task RoundTrip_TablesWithTotals_PreservesTableStructure()
    {
        using var processor = new SpreadsheetProcessor();
        byte[] bytes;

        using (var doc = processor.CreateBlank("Tables"))
        {
            var sheet = doc.Sheets[0];
            sheet.SetValue(1, 1, "Product");
            sheet.SetValue(1, 2, "Price");
            sheet.SetValue(2, 1, "Widget");
            sheet.SetValue(2, 2, 10);
            sheet.SetValue(3, 1, "Gadget");
            sheet.SetValue(3, 2, 20);

            var table = sheet.Tables.Add(CellRange.FromA1("A1:B3"), "Sales");
            table.ShowTotalsRow = true;
            table.Columns[1].TotalsFunction = Models.Tables.TotalsRowFunction.Sum;

            bytes = await SaveToBytes(processor, doc);
        }

        using (var doc = await LoadFromBytes(processor, bytes))
        {
            var sheet = doc.Sheets[0];
            sheet.Tables.Count.ShouldBe(1);

            var table = sheet.Tables[0];
            table.Name.ShouldBe("Sales");
            table.ShowTotalsRow.ShouldBeTrue();
            table.Columns.Count.ShouldBe(2);
            table.Columns[1].TotalsFunction.ShouldBe(Models.Tables.TotalsRowFunction.Sum);
        }
    }

    [Fact]
    public async Task RoundTrip_EmptyAndNullCells_HandledCorrectly()
    {
        using var processor = new SpreadsheetProcessor();
        byte[] bytes;

        using (var doc = processor.CreateBlank("Sparse"))
        {
            var sheet = doc.Sheets[0];
            sheet.SetValue(1, 1, "A1");
            sheet.SetValue(1, 3, "C1"); // Skip B1
            sheet.SetValue(3, 1, "A3"); // Skip row 2

            bytes = await SaveToBytes(processor, doc);
        }

        using (var doc = await LoadFromBytes(processor, bytes))
        {
            var sheet = doc.Sheets[0];

            sheet.GetCell(1, 1)!.GetString().ShouldBe("A1");
            sheet.GetCell(1, 2).ShouldBeNull(); // B1 should be null
            sheet.GetCell(1, 3)!.GetString().ShouldBe("C1");
            sheet.GetCell(2, 1).ShouldBeNull(); // A2 should be null
            sheet.GetCell(3, 1)!.GetString().ShouldBe("A3");
        }
    }

    [Fact]
    public async Task RoundTrip_LargeDataSet_PreservesAllValues()
    {
        using var processor = new SpreadsheetProcessor();
        byte[] bytes;

        using (var doc = processor.CreateBlank("Large"))
        {
            var sheet = doc.Sheets[0];

            // Write 1000 rows of data
            for (var row = 1; row <= 1000; row++)
            {
                sheet.SetValue(row, 1, row);
                sheet.SetValue(row, 2, $"Row{row}");
                sheet.SetValue(row, 3, row * 1.5);
            }

            bytes = await SaveToBytes(processor, doc);
        }

        using (var doc = await LoadFromBytes(processor, bytes))
        {
            var sheet = doc.Sheets[0];

            // Spot check various rows
            sheet.GetCell(1, 1)!.GetDouble().ShouldBe(1.0);
            sheet.GetCell(1, 2)!.GetString().ShouldBe("Row1");

            sheet.GetCell(500, 1)!.GetDouble().ShouldBe(500.0);
            sheet.GetCell(500, 2)!.GetString().ShouldBe("Row500");
            sheet.GetCell(500, 3)!.GetDouble().ShouldBe(750.0);

            sheet.GetCell(1000, 1)!.GetDouble().ShouldBe(1000.0);
            sheet.GetCell(1000, 2)!.GetString().ShouldBe("Row1000");
        }
    }

    [Fact]
    public async Task RoundTrip_SheetProtection_PreservesSecuritySettings()
    {
        using var processor = new SpreadsheetProcessor();
        byte[] bytes;

        using (var doc = processor.CreateBlank("Protected"))
        {
            var sheet = doc.Sheets[0];
            sheet.Protection.Protect("test123");
            sheet.Protection.AllowInsertRows = true;
            sheet.Protection.AllowFormatCells = true;

            bytes = await SaveToBytes(processor, doc);
        }

        using (var doc = await LoadFromBytes(processor, bytes))
        {
            var sheet = doc.Sheets[0];

            sheet.Protection.IsProtected.ShouldBeTrue();
            sheet.Protection.PasswordHash.ShouldNotBeNull();
            sheet.Protection.AllowInsertRows.ShouldBeTrue();
            sheet.Protection.AllowFormatCells.ShouldBeTrue();
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
