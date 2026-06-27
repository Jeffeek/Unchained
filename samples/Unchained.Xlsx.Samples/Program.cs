using Unchained.Ooxml.Charts;
using Unchained.Ooxml.Drawing;
using Unchained.Xlsx.Engine;
using Unchained.Xlsx.Models;
using Unchained.Xlsx.Models.Cell;
using Unchained.Xlsx.Models.Pivot;
using Unchained.Xlsx.Models.Styles;

namespace Unchained.Xlsx.Samples;

/// <summary>
///     Console walkthrough of the <c>Unchained.Xlsx</c> public API. Each demo is self-contained and
///     writes its output into an <c>output/</c> directory next to the executable. Run without
///     arguments for an interactive menu, pass a demo name (e.g. <c>dotnet run -- charts</c>) to run
///     one, or <c>all</c> to run everything.
/// </summary>
internal static class Program
{
    private static readonly string OutputDir =
        Path.Combine(AppContext.BaseDirectory, "output");

    private static readonly (string Key, string Title, Func<Task> Run)[] Demos =
    [
        ("create", "Create a styled workbook", CreateWorkbookAsync),
        ("formulas", "Write and evaluate formulas", FormulasAsync),
        ("tables", "Add a structured table with banding", TableAsync),
        ("validation", "Data validation and named ranges", ValidationAsync),
        ("charts", "Embed a clustered-column chart", ChartAsync),
        ("pivot", "Build a pivot table from raw rows", PivotAsync),
        ("csv", "Import from CSV and export back out", CsvAsync),
        ("encrypt", "Encrypt and re-open a workbook", EncryptAsync),
        ("read", "Read every cell from a workbook", ReadAsync)
    ];

    private static async Task<int> Main(string[] args)
    {
        Directory.CreateDirectory(OutputDir);
        Console.WriteLine("Unchained.Xlsx samples");
        Console.WriteLine($"Output directory: {OutputDir}");
        Console.WriteLine();

        var selection = args.Length > 0 ? args[0].ToLowerInvariant() : Prompt();

        try
        {
            if (selection is "all")
            {
                foreach (var demo in Demos)
                    await RunOneAsync(demo);
            }
            else if (Demos.FirstOrDefault(d => d.Key == selection) is { Run: not null } match)
                await RunOneAsync(match);
            else
            {
                Console.WriteLine($"Unknown demo '{selection}'. Valid: {string.Join(", ", Demos.Select(static d => d.Key))}, all.");
                return 1;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Demo failed: {ex.Message}");
            return 1;
        }

        Console.WriteLine();
        Console.WriteLine("Done.");
        return 0;
    }

    private static string Prompt()
    {
        Console.WriteLine("Available demos:");
        for (var i = 0; i < Demos.Length; i++)
            Console.WriteLine($"  {i + 1}. {Demos[i].Key,-10} — {Demos[i].Title}");
        Console.WriteLine("  a. all");
        Console.Write("Select (number, name, or 'a'): ");

        var input = Console.ReadLine()?.Trim() ?? string.Empty;
        if (input is "a" or "all") return "all";
        if (int.TryParse(input, out var n) && n >= 1 && n <= Demos.Length)
            return Demos[n - 1].Key;

        return input.ToLowerInvariant();
    }

    private static async Task RunOneAsync((string Key, string Title, Func<Task> Run) demo)
    {
        Console.WriteLine($"▶ {demo.Title}");
        await demo.Run();
        Console.WriteLine();
    }

    // ── Demos ─────────────────────────────────────────────────────────────────

    /// <summary>
    ///     Builds a workbook with a bold, filled header row and a currency-formatted column, sets
    ///     document properties, then saves it as an .xlsx file.
    /// </summary>
    private static async Task CreateWorkbookAsync()
    {
        using var processor = new SpreadsheetProcessor();
        using var doc = processor.CreateBlank("Revenue");

        doc.Properties.Title = "Quarterly Revenue";
        doc.Properties.Author = "Unchained Samples";
        doc.Properties.Company = "Unchained";

        var sheet = doc.Sheets[0];

        string[] headers = ["Quarter", "Region", "Revenue"];
        for (var c = 0; c < headers.Length; c++)
        {
            var cell = sheet[1, c + 1];
            cell.SetValue(headers[c]);
            cell.ApplyFont(static f =>
                {
                    f.Bold = true;
                    f.Color = ColorSpec.FromRgb(255, 255, 255);
                }
            );
            cell.ApplyFill(static f =>
                {
                    f.PatternType = FillPattern.Solid;
                    f.ForegroundColor = ColorSpec.FromRgb(31, 78, 121);
                }
            );
            cell.ApplyAlignment(static a => a.Horizontal = HorizontalAlignment.Center);
        }

        (string Quarter, string Region, double Revenue)[] rows =
        [
            ("Q1", "North", 12_400),
            ("Q2", "North", 15_100),
            ("Q1", "South", 9_800),
            ("Q2", "South", 11_300)
        ];

        for (var r = 0; r < rows.Length; r++)
        {
            var row = r + 2;
            sheet.SetValue(row, 1, rows[r].Quarter);
            sheet.SetValue(row, 2, rows[r].Region);

            var revenue = sheet[row, 3];
            revenue.SetValue(rows[r].Revenue);
            revenue.SetNumberFormat("$#,##0");
        }

        var path = Path.Combine(OutputDir, "workbook.xlsx");
        await processor.SaveAsync(doc, path);
        Console.WriteLine($"  Wrote {rows.Length}-row styled workbook → {Rel(path)}");
    }

    /// <summary>
    ///     Writes a column of numbers plus SUM/AVERAGE formulas, recalculates the workbook so the
    ///     cached results are stored, and evaluates an ad-hoc formula with the built-in engine.
    /// </summary>
    private static async Task FormulasAsync()
    {
        using var processor = new SpreadsheetProcessor();
        using var doc = processor.CreateBlank("Numbers");
        var sheet = doc.Sheets[0];

        double[] values = [10, 20, 30, 40];
        for (var i = 0; i < values.Length; i++)
            sheet.SetValue(i + 1, 1, values[i]);

        sheet.SetFormula(5, 1, "=SUM(A1:A4)");
        sheet.SetFormula(6, 1, "=AVERAGE(A1:A4)");

        var evaluated = doc.Recalculate();
        Console.WriteLine($"  Recalculated {evaluated} formula cell(s).");
        Console.WriteLine($"  A5 =SUM(A1:A4)     → {sheet[5, 1].GetDouble()}");
        Console.WriteLine($"  A6 =AVERAGE(A1:A4) → {sheet[6, 1].GetDouble()}");

        var adHoc = SpreadsheetDocument.EvaluateFormula(sheet, "=MAX(A1:A4)*2");
        Console.WriteLine($"  Ad-hoc =MAX(A1:A4)*2 → {adHoc}");

        var path = Path.Combine(OutputDir, "formulas.xlsx");
        await processor.SaveAsync(doc, path);
        Console.WriteLine($"  Wrote workbook with cached formula results → {Rel(path)}");
    }

    /// <summary>Fills a range with data and promotes it to a styled, banded Excel table (ListObject).</summary>
    private static async Task TableAsync()
    {
        using var processor = new SpreadsheetProcessor();
        using var doc = processor.CreateBlank("Inventory");
        var sheet = doc.Sheets[0];

        string[] headers = ["SKU", "Product", "In Stock"];
        for (var c = 0; c < headers.Length; c++)
            sheet.SetValue(1, c + 1, headers[c]);

        (string Sku, string Product, double Stock)[] items =
        [
            ("A-100", "Widget", 240),
            ("A-200", "Gadget", 75),
            ("A-300", "Gizmo", 0)
        ];
        for (var r = 0; r < items.Length; r++)
        {
            sheet.SetValue(r + 2, 1, items[r].Sku);
            sheet.SetValue(r + 2, 2, items[r].Product);
            sheet.SetValue(r + 2, 3, items[r].Stock);
        }

        var range = new CellRange(new CellReference(1, 1), new CellReference(items.Length + 1, 3));
        var table = sheet.AddTable(range, hasHeaders: true);
        table.Name = "Inventory";
        table.ShowBandedRows = true;
        table.ShowTotalsRow = true;
        table.StyleName = "TableStyleMedium9";

        var path = Path.Combine(OutputDir, "table.xlsx");
        await processor.SaveAsync(doc, path);
        Console.WriteLine($"  Wrote table '{table.Name}' over {table.Range.ToA1()} → {Rel(path)}");
    }

    /// <summary>
    ///     Adds a drop-down (list) data validation to a range and defines a workbook-scoped named
    ///     range that points at the priced cells.
    /// </summary>
    private static async Task ValidationAsync()
    {
        using var processor = new SpreadsheetProcessor();
        using var doc = processor.CreateBlank("Orders");
        var sheet = doc.Sheets[0];

        sheet.SetValue(1, 1, "Item");
        sheet.SetValue(1, 2, "Status");
        sheet.SetValue(1, 3, "Price");

        sheet.SetValue(2, 1, "Cable");
        sheet.SetValue(2, 3, 9.99);
        sheet.SetValue(3, 1, "Adapter");
        sheet.SetValue(3, 3, 14.50);

        // Drop-down on the Status column (B2:B3).
        var statusRange = new CellRange(new CellReference(2, 2), new CellReference(3, 2));
        sheet.AddDropdownValidation(statusRange, "New", "Shipped", "Closed");

        // Workbook-scoped named range over the prices.
        var priceRange = new CellRange(new CellReference(2, 3), new CellReference(3, 3));
        doc.DefinedNames.Add("Prices", priceRange.ToSheetQualifiedA1(sheet.Name));

        Console.WriteLine($"  Drop-down on {statusRange.ToA1()} (New / Shipped / Closed)");
        Console.WriteLine($"  Named range 'Prices' → {priceRange.ToA1()}");

        var path = Path.Combine(OutputDir, "validation.xlsx");
        await processor.SaveAsync(doc, path);
        Console.WriteLine($"  Wrote workbook with validation + named range → {Rel(path)}");
    }

    /// <summary>Writes a small category/value table and embeds a clustered-column chart anchored over it.</summary>
    private static async Task ChartAsync()
    {
        using var processor = new SpreadsheetProcessor();
        using var doc = processor.CreateBlank("Sales");
        var sheet = doc.Sheets[0];

        sheet.SetValue(1, 1, "Month");
        sheet.SetValue(1, 2, "Units");

        (string Month, double Units)[] data =
        [
            ("Jan", 120),
            ("Feb", 150),
            ("Mar", 180),
            ("Apr", 140)
        ];
        for (var r = 0; r < data.Length; r++)
        {
            sheet.SetValue(r + 2, 1, data[r].Month);
            sheet.SetValue(r + 2, 2, data[r].Units);
        }

        var dataRange = new CellRange(new CellReference(1, 1), new CellReference(data.Length + 1, 2));
        var anchor = Drawings.DrawingAnchor.TwoCell(new CellReference(1, 4), new CellReference(16, 12));
        sheet.AddChart(ChartType.ColumnClustered, dataRange, anchor, "Units by Month");

        var path = Path.Combine(OutputDir, "chart.xlsx");
        await processor.SaveAsync(doc, path);
        Console.WriteLine($"  Wrote workbook with a clustered-column chart → {Rel(path)}");
    }

    /// <summary>
    ///     Builds a raw transaction table on one sheet, then summarises it with a pivot table that
    ///     groups by region (rows) and sums the amount.
    /// </summary>
    private static async Task PivotAsync()
    {
        using var processor = new SpreadsheetProcessor();
        using var doc = processor.CreateBlank("Data");
        var data = doc.Sheets[0];

        string[] headers = ["Region", "Product", "Amount"];
        for (var c = 0; c < headers.Length; c++)
            data.SetValue(1, c + 1, headers[c]);

        (string Region, string Product, double Amount)[] rows =
        [
            ("North", "Widget", 1_200),
            ("North", "Gadget", 800),
            ("South", "Widget", 950),
            ("South", "Gadget", 1_050)
        ];
        for (var r = 0; r < rows.Length; r++)
        {
            data.SetValue(r + 2, 1, rows[r].Region);
            data.SetValue(r + 2, 2, rows[r].Product);
            data.SetValue(r + 2, 3, rows[r].Amount);
        }

        var summary = doc.Sheets.Add("Summary");
        var sourceRange = new CellRange(new CellReference(1, 1), new CellReference(rows.Length + 1, 3));
        var pivot = summary.PivotTables.Add(sourceRange, new CellReference(1, 1), "RegionTotals", sourceSheet: data);
        pivot.AddRowField("Region");
        // ReSharper disable once RedundantArgumentDefaultValue
        pivot.AddDataField("Amount", PivotDataFunction.Sum);

        var path = Path.Combine(OutputDir, "pivot.xlsx");
        await processor.SaveAsync(doc, path);
        Console.WriteLine($"  Wrote pivot '{pivot.Name}' (Region → SUM of Amount) → {Rel(path)}");
    }

    /// <summary>
    ///     Parses a CSV string into a workbook (with type inference), then exports the first sheet
    ///     straight back out to CSV.
    /// </summary>
    private static async Task CsvAsync()
    {
        const string csv = """
            Name,Score,Passed
            Ada,92,true
            Linus,88,true
            Grace,74,false
            """;

        var csvPath = Path.Combine(OutputDir, "scores.csv");
        await File.WriteAllTextAsync(csvPath, csv);

        using var processor = new SpreadsheetProcessor();
        using var doc = await processor.LoadFromCsvAsync(
            csvPath,
            new CsvLoadOptions { HasHeaders = true, TypeInference = true }
        );

        var sheet = doc.Sheets[0];
        var used = sheet.GetUsedRange();
        Console.WriteLine($"  Imported CSV → sheet '{sheet.Name}', used range {used?.ToA1()}");

        var xlsxPath = Path.Combine(OutputDir, "scores.xlsx");
        await processor.SaveAsync(doc, xlsxPath);

        var roundTripPath = Path.Combine(OutputDir, "scores-roundtrip.csv");
        await sheet.SaveAsCsvAsync(roundTripPath);
        Console.WriteLine($"  Wrote {Rel(xlsxPath)} and re-exported CSV → {Rel(roundTripPath)}");
    }

    /// <summary>Saves a workbook with AES-256 encryption, then re-opens it with the password.</summary>
    private static async Task EncryptAsync()
    {
        using var processor = new SpreadsheetProcessor();
        using var doc = processor.CreateBlank("Secret");
        doc.Sheets[0].SetValue(1, 1, "Confidential");

        var path = Path.Combine(OutputDir, "encrypted.xlsx");
        await processor.SaveAsync(doc, path, new XlsxSaveOptions { Password = "open-sesame" });
        Console.WriteLine($"  Wrote AES-256 encrypted workbook → {Rel(path)}");

        using var reopened = await processor.LoadAsync(path, new OpenOptions { Password = "open-sesame" });
        Console.WriteLine($"  Re-opened with password — {reopened.Sheets.Count} sheet(s), A1 = \"{reopened.Sheets[0][1, 1].GetString()}\".");
    }

    /// <summary>Loads a workbook and prints the value of every non-empty cell, sheet by sheet.</summary>
    private static async Task ReadAsync()
    {
        var source = Path.Combine(OutputDir, "workbook.xlsx");
        if (!File.Exists(source)) await CreateWorkbookAsync();

        using var processor = new SpreadsheetProcessor();
        using var doc = await processor.LoadAsync(source);

        foreach (var sheet in doc.Sheets)
        {
            Console.WriteLine($"  Sheet '{sheet.Name}':");
            var used = sheet.GetUsedRange();
            if (used is null)
            {
                Console.WriteLine("    (empty)");
                continue;
            }

            foreach (var reference in used.Value.Cells())
            {
                var cell = sheet.GetCell(reference);
                if (cell is null || cell.CellType == CellType.Empty) continue;

                Console.WriteLine($"    {reference.ToA1(),-4} = {cell.GetFormattedString()}");
            }
        }
    }

    private static string Rel(string path) => Path.GetRelativePath(AppContext.BaseDirectory, path);
}
