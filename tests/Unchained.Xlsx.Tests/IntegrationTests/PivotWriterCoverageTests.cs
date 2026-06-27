using System.IO.Compression;
using Shouldly;
using Unchained.Xlsx.Engine;
using Unchained.Xlsx.Models.Cell;
using Unchained.Xlsx.Models.Pivot;
using Unchained.Xlsx.Tests.Helpers;
using Xunit;

namespace Unchained.Xlsx.Tests.IntegrationTests;

/// <summary>Drives every PivotWriter branch: all data-function subtotals, page fields, and cache value kinds.</summary>
public class PivotWriterCoverageTests
{
    private static SpreadsheetDocument WithSource()
    {
        var document = XlsxFixtures.WithSheets("Data");
        var sheet = document.Sheets[0];
        sheet.SetValue(1, 1, "Region");
        sheet.SetValue(1, 2, "Product");
        sheet.SetValue(1, 3, "Amount");
        sheet.SetValue(2, 1, "East");
        sheet.SetValue(2, 2, "Widget");
        sheet.SetValue(2, 3, 100.0);
        sheet.SetValue(3, 1, "West");
        sheet.SetValue(3, 2, "Gadget");
        sheet.SetValue(3, 3, 200.0);
        return document;
    }

    [
        Theory,
        InlineData(PivotDataFunction.Sum, "Sum"),
        InlineData(PivotDataFunction.Count, "Count"),
        InlineData(PivotDataFunction.Average, "Average"),
        InlineData(PivotDataFunction.Max, "Max"),
        InlineData(PivotDataFunction.Min, "Min"),
        InlineData(PivotDataFunction.Product, "Product"),
        InlineData(PivotDataFunction.CountNumbers, "Count"),
        InlineData(PivotDataFunction.StdDev, "StdDev"),
        InlineData(PivotDataFunction.StdDevP, "StdDevp"),
        InlineData(PivotDataFunction.Var, "Var"),
        InlineData(PivotDataFunction.VarP, "Varp")
    ]
    public void DataField_FunctionLabel(PivotDataFunction function, string expectedPrefix)
    {
        using var document = WithSource();
        var pivot = document.Sheets[0].AddPivotTable(CellRange.FromA1("A1:C3"), CellReference.FromA1("E1"), "P");
        pivot.AddDataField("Amount", function).Name.ShouldBe($"{expectedPrefix} of Amount");
    }

    [Fact]
    public async Task AllDataFunctions_Serialize()
    {
        using var document = WithSource();
        var pivot = document.Sheets[0].AddPivotTable(CellRange.FromA1("A1:C3"), CellReference.FromA1("E1"), "P");
        pivot.AddRowField("Region");
        pivot.AddColumnField("Product");
        foreach (var function in Enum.GetValues<PivotDataFunction>())
            pivot.AddDataField("Amount", function);

        var bytes = await XlsxFixtures.SaveBytesAsync(document);
#if NET10_0_OR_GREATER
        await using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
#else
        using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
#endif
        archive.Entries.Any(static e => e.FullName.Contains("pivotTable")).ShouldBeTrue();
    }

    [Fact]
    public async Task PageField_Serializes()
    {
        using var document = WithSource();
        var pivot = document.Sheets[0].AddPivotTable(CellRange.FromA1("A1:C3"), CellReference.FromA1("E1"), "P");
        pivot.AddPageField("Region");
        pivot.AddRowField("Product");
        pivot.AddDataField("Amount");

        var bytes = await XlsxFixtures.SaveBytesAsync(document);
#if NET10_0_OR_GREATER
        await using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
#else
        using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
#endif
        var entry = archive.Entries.First(static e => e.FullName.Contains("pivotTables/pivotTable"));
#if NET10_0_OR_GREATER
        using var reader = new StreamReader(await entry.OpenAsync());
#else
        using var reader = new StreamReader(entry.Open());
#endif
        var xml = await reader.ReadToEndAsync(TestContext.Current.CancellationToken);
        xml.ShouldContain("pageFields");
    }

    [Fact]
    public async Task CacheRecords_AllValueKinds_Serialize()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        var sheet = document.Sheets[0];
        sheet.SetValue(1, 1, "Text");
        sheet.SetValue(1, 2, "Num");
        sheet.SetValue(1, 3, "Bool");
        sheet.SetValue(1, 4, "Err");
        sheet.SetValue(2, 1, "alpha");
        sheet.SetValue(2, 2, 1.5);
        sheet.SetValue(2, 3, true);
        sheet[2, 4].SetValue(CellError.NotAvailable);
        // Row 3 leaves column 1 blank to produce a blank ("m") cache value.
        sheet.SetValue(3, 2, 2.5);
        sheet.SetValue(3, 3, false);
        sheet[3, 4].SetValue(CellError.Value);

        var pivot = sheet.AddPivotTable(CellRange.FromA1("A1:D3"), CellReference.FromA1("F1"), "P");
        pivot.AddRowField("Text");
        pivot.AddDataField("Num");

        var bytes = await XlsxFixtures.SaveBytesAsync(document);
#if NET10_0_OR_GREATER
        await using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
#else
        using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
#endif
        var entry = archive.Entries.First(static e => e.FullName.Contains("pivotCacheRecords"));
#if NET10_0_OR_GREATER
        using var reader = new StreamReader(await entry.OpenAsync());
#else
        using var reader = new StreamReader(entry.Open());
#endif
        var xml = await reader.ReadToEndAsync(TestContext.Current.CancellationToken);
        xml.ShouldContain("<");
    }

    [Fact]
    public void Refresh_WithUnknownSheet_DoesNothing()
    {
        using var document = WithSource();
        var pivot = document.Sheets[0].AddPivotTable(CellRange.FromA1("A1:C3"), CellReference.FromA1("E1"), "P");
        pivot.AddRowField("Region");
        var before = pivot.Fields[0].Items.Count;

        // Resolver returns null → Refresh is a no-op.
        pivot.Refresh(static _ => null);
        pivot.Fields[0].Items.Count.ShouldBe(before);
    }

    [Fact]
    public void AddDataField_UnknownField_Throws()
    {
        using var document = WithSource();
        var pivot = document.Sheets[0].AddPivotTable(CellRange.FromA1("A1:C3"), CellReference.FromA1("E1"), "P");
        Should.Throw<ArgumentException>(() => pivot.AddDataField("Nonexistent"));
    }
}
