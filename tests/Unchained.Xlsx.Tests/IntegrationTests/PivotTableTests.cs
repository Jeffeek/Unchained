using System.IO.Compression;
using Shouldly;
using Unchained.Xlsx.Engine;
using Unchained.Xlsx.Models.Cell;
using Unchained.Xlsx.Models.Pivot;
using Unchained.Xlsx.Tests.Helpers;
using Xunit;

namespace Unchained.Xlsx.Tests.IntegrationTests;

public class PivotTableTests
{
    private static SpreadsheetDocument WithSourceData()
    {
        var document = XlsxFixtures.WithSheets("Data");
        var sheet = document.Sheets[0];
        sheet.SetValue(1, 1, "Region");
        sheet.SetValue(1, 2, "Product");
        sheet.SetValue(1, 3, "Amount");
        var rows = new (string region, string product, double amount)[]
        {
            ("East", "Widget", 100),
            ("East", "Gadget", 200),
            ("West", "Widget", 150),
            ("West", "Gadget", 250)
        };
        for (var i = 0; i < rows.Length; i++)
        {
            sheet.SetValue(i + 2, 1, rows[i].region);
            sheet.SetValue(i + 2, 2, rows[i].product);
            sheet.SetValue(i + 2, 3, rows[i].amount);
        }

        return document;
    }

    [Fact]
    public void AddPivotTable_BuildsFieldsAndCache()
    {
        using var document = WithSourceData();
        var sheet = document.Sheets[0];

        var pivot = sheet.AddPivotTable(CellRange.FromA1("A1:C5"), CellReference.FromA1("E1"), "SalesPivot");

        pivot.Fields.Count.ShouldBe(3);
        pivot.Fields.Select(static f => f.Name).ShouldBe(["Region", "Product", "Amount"]);
        // Region field should have captured distinct items.
        pivot.Fields[0].Items.ShouldBe(["East", "West"], true);
    }

    [Fact]
    public void AddPivotTable_RejectsOverlappingTarget() =>
        Should.Throw<ArgumentException>(static () =>
            {
                using var document = WithSourceData();
                document.Sheets[0].AddPivotTable(CellRange.FromA1("A1:C5"), CellReference.FromA1("B2"), "P");
            }
        );

    [Fact]
    public async Task PivotTable_RoundTrips()
    {
        using var document = WithSourceData();
        var pivot = document.Sheets[0].AddPivotTable(CellRange.FromA1("A1:C5"), CellReference.FromA1("E1"), "SalesPivot");
        pivot.AddRowField("Region");
        pivot.AddColumnField("Product");
        pivot.AddDataField("Amount");

        using var reloaded = await XlsxFixtures.RoundTripAsync(document);
        var pivots = reloaded.Sheets[0].PivotTables;
        pivots.Count.ShouldBe(1);
        pivots[0].Name.ShouldBe("SalesPivot");
        pivots[0].Fields.Count.ShouldBe(3);
    }

    [Fact]
    public async Task PivotTable_ProducesValidParts()
    {
        using var document = WithSourceData();
        var pivot = document.Sheets[0].AddPivotTable(CellRange.FromA1("A1:C5"), CellReference.FromA1("E1"), "P");
        pivot.AddRowField("Region");
        pivot.AddDataField("Amount");

        var bytes = await XlsxFixtures.SaveBytesAsync(document);
#if NET10_0_OR_GREATER
        await using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
#else
        using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
#endif
        archive.Entries.Any(static e => e.FullName.Contains("pivotTables/pivotTable")).ShouldBeTrue();
        archive.Entries.Any(static e => e.FullName.Contains("pivotCache/pivotCacheDefinition")).ShouldBeTrue();
        archive.Entries.Any(static e => e.FullName.Contains("pivotCache/pivotCacheRecords")).ShouldBeTrue();
    }

    [Fact]
    public void Refresh_RebuildsCacheFromSource()
    {
        using var document = WithSourceData();
        var sheet = document.Sheets[0];
        var pivot = sheet.AddPivotTable(CellRange.FromA1("A1:C5"), CellReference.FromA1("E1"), "P");
        pivot.AddRowField("Region");

        // Add a new region in the source, then refresh.
        sheet.SetValue(6, 1, "North");
        sheet.SetValue(6, 2, "Widget");
        sheet.SetValue(6, 3, 999.0);
        pivot.SourceRange = CellRange.FromA1("A1:C6");
        sheet.PivotTables.RefreshAll();

        pivot.Fields[0].Items.ShouldContain("North");
    }

    [Fact]
    public void AddDataField_NamesWithFunction()
    {
        using var document = WithSourceData();
        var pivot = document.Sheets[0].AddPivotTable(CellRange.FromA1("A1:C5"), CellReference.FromA1("E1"), "P");
        var dataField = pivot.AddDataField("Amount", PivotDataFunction.Average);
        dataField.Name.ShouldBe("Average of Amount");
        dataField.Function.ShouldBe(PivotDataFunction.Average);
    }
}
