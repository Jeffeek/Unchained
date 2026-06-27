using System.IO.Compression;
using Shouldly;
using Unchained.Xlsx.Engine;
using Unchained.Xlsx.Models.Cell;
using Unchained.Xlsx.Models.Pivot;
using Unchained.Xlsx.Tests.Helpers;
using Xunit;

namespace Unchained.Xlsx.Tests.IntegrationTests;

/// <summary>Coverage for table and pivot collection operations and pivot-cache value kinds.</summary>
public class TablePivotCoverageTests
{
    // ── ListObjectCollection ─────────────────────────────────────────────────

    [Fact]
    public void Table_NoHeaders_UsesGenericColumnNames()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        var table = document.Sheets[0].AddTable(CellRange.FromA1("A1:C2"), false);
        table.Columns.Select(static c => c.Name).ShouldBe(["Column1", "Column2", "Column3"]);
    }

    [Fact]
    public void Table_DuplicateHeaders_AreMadeUnique()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        var sheet = document.Sheets[0];
        sheet.SetValue(1, 1, "Name");
        sheet.SetValue(1, 2, "Name");
        var table = sheet.AddTable(CellRange.FromA1("A1:B2"));

        table.Columns[0].Name.ShouldBe("Name");
        table.Columns[1].Name.ShouldBe("Name2");
    }

    [Fact]
    public void Table_Remove_DropsTable()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        var sheet = document.Sheets[0];
        sheet.SetValue(1, 1, "H");
        var table = sheet.AddTable(CellRange.FromA1("A1:A2"));
        sheet.Tables.Count.ShouldBe(1);

        sheet.Tables.Remove(table);
        sheet.Tables.Count.ShouldBe(0);
    }

    [Fact]
    public void Table_Find_MissingReturnsNull()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        document.Sheets[0].Tables.Find("nope").ShouldBeNull();
    }

    [Fact]
    public void Table_Enumeration_Works()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        var sheet = document.Sheets[0];
        sheet.SetValue(1, 1, "H");
        sheet.AddTable(CellRange.FromA1("A1:A2"));
        sheet.Tables.AsEnumerable().Count().ShouldBe(1);
    }

    // ── PivotTableCollection ─────────────────────────────────────────────────

    private static SpreadsheetDocument WithSource()
    {
        var document = XlsxFixtures.WithSheets("Data");
        var sheet = document.Sheets[0];
        sheet.SetValue(1, 1, "Region");
        sheet.SetValue(1, 2, "Amount");
        sheet.SetValue(2, 1, "East");
        sheet.SetValue(2, 2, 100.0);
        sheet.SetValue(3, 1, "West");
        sheet.SetValue(3, 2, 200.0);
        return document;
    }

    [Fact]
    public void Pivot_EmptyName_Throws()
    {
        using var document = WithSource();
        Should.Throw<ArgumentException>(() =>
            document.Sheets[0].PivotTables.Add(CellRange.FromA1("A1:B3"), CellReference.FromA1("E1"), "")
        );
    }

    [Fact]
    public void Pivot_BlankHeader_Throws()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        var sheet = document.Sheets[0];
        sheet.SetValue(1, 1, "Region");
        // Column 2 header is blank.
        sheet.SetValue(2, 2, 5.0);
        Should.Throw<ArgumentException>(() =>
            sheet.PivotTables.Add(CellRange.FromA1("A1:B3"), CellReference.FromA1("E1"), "P")
        );
    }

    [Fact]
    public void Pivot_Find_And_Remove()
    {
        using var document = WithSource();
        var sheet = document.Sheets[0];
        var pivot = sheet.AddPivotTable(CellRange.FromA1("A1:B3"), CellReference.FromA1("E1"), "P");

        sheet.PivotTables.Find("P").ShouldBe(pivot);
        sheet.PivotTables.Find("missing").ShouldBeNull();

        sheet.PivotTables.Remove(pivot);
        sheet.PivotTables.Count.ShouldBe(0);
    }

    [Fact]
    public void Pivot_RemoveField()
    {
        using var document = WithSource();
        var pivot = document.Sheets[0].AddPivotTable(CellRange.FromA1("A1:B3"), CellReference.FromA1("E1"), "P");
        pivot.AddRowField("Region");
        pivot.RowFields.Count().ShouldBe(1);

        pivot.RemoveField("Region");
        pivot.RowFields.Count().ShouldBe(0);
    }

    [Fact]
    public void Pivot_PageField_And_DataFunctions()
    {
        using var document = WithSource();
        var pivot = document.Sheets[0].AddPivotTable(CellRange.FromA1("A1:B3"), CellReference.FromA1("E1"), "P");
        pivot.AddPageField("Region");
        pivot.PageFields.Count().ShouldBe(1);

        pivot.AddDataField("Amount", PivotDataFunction.Count).Name.ShouldBe("Count of Amount");
        pivot.DataFields.Count.ShouldBe(1);
    }

    // ── PivotCacheValue (all source-cell kinds) ──────────────────────────────

    [Fact]
    public async Task PivotCache_CapturesAllValueKinds()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        var sheet = document.Sheets[0];
        sheet.SetValue(1, 1, "Text");
        sheet.SetValue(1, 2, "Num");
        sheet.SetValue(1, 3, "Bool");
        sheet.SetValue(2, 1, "alpha");
        sheet.SetValue(2, 2, 12.5);
        sheet.SetValue(2, 3, true);
        sheet[3, 1].SetValue(CellError.Value);
        sheet.SetValue(3, 2, 99.0);
        sheet.SetValue(3, 3, false);

        var pivot = sheet.AddPivotTable(CellRange.FromA1("A1:C3"), CellReference.FromA1("E1"), "P");
        pivot.AddRowField("Text");
        pivot.AddDataField("Num");

        var bytes = await XlsxFixtures.SaveBytesAsync(document);
#if NET10_0_OR_GREATER
        await using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
#else
        using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
#endif
        archive.Entries.Any(static e => e.FullName.Contains("pivotCacheRecords")).ShouldBeTrue();
    }
}
