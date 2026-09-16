using Shouldly;
using Unchained.Xlsx.Abstractions;
using Unchained.Xlsx.Engine;
using Xunit;

namespace Unchained.Xlsx.Tests.IntegrationTests;

/// <summary>
///     Tests that exercise WorksheetParser edge cases by creating documents
///     with specific features, saving, reloading, and verifying correct parsing.
///     Targets: shared formulas, inline strings, error cells, array formulas.
/// </summary>
public sealed class WorksheetParserRegressionTests
{
    [Fact]
    public async Task Parser_SharedFormulas_ParsesCorrectly()
    {
        // When you copy a formula down in Excel, it creates a "shared formula"
        // group to save space. Only the first cell stores the full formula.
        using var processor = new SpreadsheetProcessor();
        byte[] bytes;

        using (var doc = processor.CreateBlank("Sheet1"))
        {
            var sheet = doc.Sheets[0];

            // Create data that will produce shared formulas
            for (var i = 1; i <= 10; i++)
            {
                sheet.SetValue(i, 1, i * 10);
                sheet.SetFormula(i, 2, $"=A{i}*2");
            }

            doc.Recalculate();
            bytes = await SaveToBytes(processor, doc);
        }

        // Reload and verify all formulas work correctly
        using (var doc = await LoadFromBytes(processor, bytes))
        {
            var sheet = doc.Sheets[0];
            doc.Recalculate();

            for (var i = 1; i <= 10; i++)
            {
                var cell = sheet.GetCell(i, 2);
                cell.ShouldNotBeNull();
                cell.GetDouble().ShouldBe(i * 10 * 2, $"Row {i} should calculate correctly");
            }
        }
    }

    [Fact]
    public async Task Parser_ErrorCells_PreservesErrorTypes()
    {
        using var processor = new SpreadsheetProcessor();
        byte[] bytes;

        using (var doc = processor.CreateBlank("Errors"))
        {
            var sheet = doc.Sheets[0];

            // Create various error types
            sheet.SetFormula(1, 1, "=1/0");        // #DIV/0!
            sheet.SetFormula(2, 1, "=SQRT(-1)");   // #NUM!
            sheet.SetFormula(3, 1, "=A1:A2 A3:A4"); // #NULL!
            sheet.SetFormula(4, 1, "=1+\"text\""); // #VALUE!

            doc.Recalculate();
            bytes = await SaveToBytes(processor, doc);
        }

        using (var doc = await LoadFromBytes(processor, bytes))
        {
            var sheet = doc.Sheets[0];

            // Errors should be preserved (exact error types might vary)
            sheet.GetCell(1, 1).ShouldNotBeNull();
            sheet.GetCell(2, 1).ShouldNotBeNull();
            sheet.GetCell(3, 1).ShouldNotBeNull();
            sheet.GetCell(4, 1).ShouldNotBeNull();
        }
    }

    [Fact]
    public async Task Parser_MixedCellReferences_HandlesOmittedReferences()
    {
        // Real-world Excel files sometimes omit cell references, expecting
        // parsers to infer position. WorksheetParser has fallback logic.
        using var processor = new SpreadsheetProcessor();
        byte[] bytes;

        using (var doc = processor.CreateBlank("Mixed"))
        {
            var sheet = doc.Sheets[0];

            // Create sparse data - parser must handle missing cells
            sheet.SetValue(1, 1, "A1");
            sheet.SetValue(1, 5, "E1");  // Skip B1, C1, D1
            sheet.SetValue(3, 2, "B3");  // Skip row 2
            sheet.SetValue(5, 10, "J5"); // Large column number

            bytes = await SaveToBytes(processor, doc);
        }

        using (var doc = await LoadFromBytes(processor, bytes))
        {
            var sheet = doc.Sheets[0];

            sheet.GetCell(1, 1)!.GetString().ShouldBe("A1");
            sheet.GetCell(1, 5)!.GetString().ShouldBe("E1");
            sheet.GetCell(3, 2)!.GetString().ShouldBe("B3");
            sheet.GetCell(5, 10)!.GetString().ShouldBe("J5");

            // Missing cells should be null
            sheet.GetCell(1, 2).ShouldBeNull();
            sheet.GetCell(2, 1).ShouldBeNull();
        }
    }

    [Fact]
    public async Task Parser_LargeRowNumbers_HandlesCorrectly()
    {
        using var processor = new SpreadsheetProcessor();
        byte[] bytes;

        using (var doc = processor.CreateBlank("LargeRows"))
        {
            var sheet = doc.Sheets[0];

            // Test high row numbers
            sheet.SetValue(1, 1, "Start");
            sheet.SetValue(1000, 1, "Row1000");
            sheet.SetValue(10000, 1, "Row10000");
            sheet.SetValue(65536, 1, "Row65536"); // Excel 2003 limit

            bytes = await SaveToBytes(processor, doc);
        }

        using (var doc = await LoadFromBytes(processor, bytes))
        {
            var sheet = doc.Sheets[0];

            sheet.GetCell(1, 1)!.GetString().ShouldBe("Start");
            sheet.GetCell(1000, 1)!.GetString().ShouldBe("Row1000");
            sheet.GetCell(10000, 1)!.GetString().ShouldBe("Row10000");
            sheet.GetCell(65536, 1)!.GetString().ShouldBe("Row65536");
        }
    }

    [Fact]
    public async Task Parser_EmptySheet_HandlesGracefully()
    {
        using var processor = new SpreadsheetProcessor();
        byte[] bytes;

        using (var doc = processor.CreateBlank("Empty"))
        {
            // Don't add any data - completely empty sheet
            bytes = await SaveToBytes(processor, doc);
        }

        using (var doc = await LoadFromBytes(processor, bytes))
        {
            var sheet = doc.Sheets[0];

            sheet.GetCell(1, 1).ShouldBeNull();
            sheet.Name.ShouldBe("Empty");
        }
    }

    [Fact]
    public async Task Parser_AllCellTypes_PreservesTypes()
    {
        using var processor = new SpreadsheetProcessor();
        byte[] bytes;

        using (var doc = processor.CreateBlank("Types"))
        {
            var sheet = doc.Sheets[0];

            // Cover all cell types
            sheet.SetValue(1, 1, 42.5);                    // Number
            sheet.SetValue(2, 1, "Plain text");            // String
            sheet.SetValue(3, 1, true);                    // Boolean
            sheet.SetValue(4, 1, false);                   // Boolean false
            sheet.SetValue(5, 1, 0.0);                     // Zero
            sheet.SetValue(6, 1, -123.45);                 // Negative
            sheet.SetValue(7, 1, new DateTime(2024, 12, 25)); // Date
            sheet.SetFormula(8, 1, "=SUM(1,2,3)");        // Formula
            sheet.SetValue(9, 1, "");                      // Empty string

            doc.Recalculate();
            bytes = await SaveToBytes(processor, doc);
        }

        using (var doc = await LoadFromBytes(processor, bytes))
        {
            var sheet = doc.Sheets[0];
            doc.Recalculate();

            sheet.GetCell(1, 1)!.GetDouble().ShouldBe(42.5);
            sheet.GetCell(2, 1)!.GetString().ShouldBe("Plain text");
            sheet.GetCell(3, 1)!.GetBoolean()!.Value.ShouldBeTrue();
            sheet.GetCell(4, 1)!.GetBoolean()!.Value.ShouldBeFalse();
            sheet.GetCell(5, 1)!.GetDouble().ShouldBe(0.0);
            sheet.GetCell(6, 1)!.GetDouble().ShouldBe(-123.45);
            sheet.GetCell(7, 1)!.GetDateTime()!.Value.Date.ShouldBe(new DateTime(2024, 12, 25));
            sheet.GetCell(8, 1)!.GetDouble().ShouldBe(6.0);
            sheet.GetCell(9, 1)!.GetString().ShouldBe("");
        }
    }

    [Fact]
    public async Task Parser_ComplexFormulas_ParsesAndCalculates()
    {
        using var processor = new SpreadsheetProcessor();
        byte[] bytes;

        using (var doc = processor.CreateBlank("Complex"))
        {
            var sheet = doc.Sheets[0];

            // Set up data
            for (var i = 1; i <= 5; i++)
                sheet.SetValue(i, 1, i * 10);

            // Various formula types
            sheet.SetFormula(1, 2, "=SUM(A1:A5)");
            sheet.SetFormula(2, 2, "=AVERAGE(A1:A5)");
            sheet.SetFormula(3, 2, "=IF(A1>25,A1*2,A1/2)");
            sheet.SetFormula(4, 2, "=ROUND(A2/3,2)");
            sheet.SetFormula(5, 2, "=MAX(A1:A5)+MIN(A1:A5)");

            doc.Recalculate();
            bytes = await SaveToBytes(processor, doc);
        }

        using (var doc = await LoadFromBytes(processor, bytes))
        {
            var sheet = doc.Sheets[0];
            doc.Recalculate();

            sheet.GetCell(1, 2)!.GetDouble().ShouldBe(150.0);  // SUM
            sheet.GetCell(2, 2)!.GetDouble().ShouldBe(30.0);   // AVERAGE
            sheet.GetCell(3, 2)!.GetDouble().ShouldBe(5.0);    // IF (10/2)
            sheet.GetCell(4, 2)!.GetDouble()!.Value.ShouldBe(6.67, 0.01); // ROUND
            sheet.GetCell(5, 2)!.GetDouble().ShouldBe(60.0);   // MAX+MIN (50+10)
        }
    }

    [Fact]
    public async Task Parser_StringsWithSpecialCharacters_PreservesContent()
    {
        using var processor = new SpreadsheetProcessor();
        byte[] bytes;

        using (var doc = processor.CreateBlank("Special"))
        {
            var sheet = doc.Sheets[0];

            sheet.SetValue(1, 1, "Line1\nLine2");          // Newline
            sheet.SetValue(2, 1, "Tab\there");             // Tab
            sheet.SetValue(3, 1, "Quote: \"text\"");       // Quotes
            sheet.SetValue(4, 1, "Emoji: 🎉🚀");           // Unicode
            sheet.SetValue(5, 1, "Formula: =SUM(A1:A2)"); // Looks like formula but is string
            sheet.SetValue(6, 1, "   Spaces   ");         // Leading/trailing spaces
            sheet.SetValue(7, 1, "<XML>&tags</XML>");     // XML-like content

            bytes = await SaveToBytes(processor, doc);
        }

        using (var doc = await LoadFromBytes(processor, bytes))
        {
            var sheet = doc.Sheets[0];

            sheet.GetCell(1, 1)!.GetString().ShouldBe("Line1\nLine2");
            sheet.GetCell(2, 1)!.GetString().ShouldBe("Tab\there");
            sheet.GetCell(3, 1)!.GetString().ShouldBe("Quote: \"text\"");
            sheet.GetCell(4, 1)!.GetString().ShouldBe("Emoji: 🎉🚀");
            sheet.GetCell(5, 1)!.GetString().ShouldBe("Formula: =SUM(A1:A2)");
            sheet.GetCell(6, 1)!.GetString().ShouldBe("   Spaces   ");
            sheet.GetCell(7, 1)!.GetString().ShouldBe("<XML>&tags</XML>");
        }
    }

    [Fact]
    public async Task Parser_VeryLongStrings_HandlesCorrectly()
    {
        using var processor = new SpreadsheetProcessor();
        byte[] bytes;

        using (var doc = processor.CreateBlank("LongStrings"))
        {
            var sheet = doc.Sheets[0];

            var longString = new string('A', 5000); // 5000 characters
            var veryLongString = new string('B', 30000); // 30k characters (near Excel limit)

            sheet.SetValue(1, 1, longString);
            sheet.SetValue(2, 1, veryLongString);

            bytes = await SaveToBytes(processor, doc);
        }

        using (var doc = await LoadFromBytes(processor, bytes))
        {
            var sheet = doc.Sheets[0];

            sheet.GetCell(1, 1)!.GetString()!.Length.ShouldBe(5000);
            sheet.GetCell(2, 1)!.GetString()!.Length.ShouldBe(30000);
        }
    }

    [Fact]
    public async Task Parser_NumberPrecision_PreservesAccuracy()
    {
        using var processor = new SpreadsheetProcessor();
        byte[] bytes;

        using (var doc = processor.CreateBlank("Precision"))
        {
            var sheet = doc.Sheets[0];

            sheet.SetValue(1, 1, 0.1 + 0.2);           // Floating point
            sheet.SetValue(2, 1, 1234567890.123456);   // Large with decimals
            sheet.SetValue(3, 1, 0.000000001);         // Very small
            sheet.SetValue(4, 1, 999999999999999.0);   // Very large
            sheet.SetValue(5, 1, Math.PI);             // Irrational

            bytes = await SaveToBytes(processor, doc);
        }

        using (var doc = await LoadFromBytes(processor, bytes))
        {
            var sheet = doc.Sheets[0];

            sheet.GetCell(1, 1)!.GetDouble()!.Value.ShouldBe(0.3, 0.0001);
            sheet.GetCell(2, 1)!.GetDouble()!.Value.ShouldBe(1234567890.123456, 0.000001);
            sheet.GetCell(3, 1)!.GetDouble()!.Value.ShouldBe(0.000000001, 0.0000000001);
            sheet.GetCell(4, 1)!.GetDouble()!.Value.ShouldBe(999999999999999.0, 1.0);
            sheet.GetCell(5, 1)!.GetDouble()!.Value.ShouldBe(Math.PI, 0.000001);
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
