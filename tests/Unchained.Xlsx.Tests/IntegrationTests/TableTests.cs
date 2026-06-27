using Shouldly;
using Unchained.Xlsx.Models.Cell;
using Unchained.Xlsx.Tests.Helpers;
using Xunit;

namespace Unchained.Xlsx.Tests.IntegrationTests;

public class TableTests
{
    [Fact]
    public async Task AddTable_WithHeaders_RoundTrips()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        var sheet = document.Sheets[0];
        sheet.SetValue(1, 1, "Date");
        sheet.SetValue(1, 2, "Amount");
        sheet.SetValue(2, 1, "Q1");
        sheet.SetValue(2, 2, 100.0);

        var table = sheet.AddTable(CellRange.FromA1("A1:B2"));
        table.Columns.Count.ShouldBe(2);

        using var reloaded = await XlsxFixtures.RoundTripAsync(document);
        var reloadedTable = reloaded.Sheets[0].Tables[0];
        reloadedTable.Columns.Count.ShouldBe(2);
        reloadedTable.Columns[0].Name.ShouldBe("Date");
        reloadedTable.Columns[1].Name.ShouldBe("Amount");
        reloadedTable.Range.ToA1().ShouldBe("A1:B2");
    }

    [Fact]
    public void AddColumn_DuplicateName_Throws()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        var table = document.Sheets[0].AddTable(CellRange.FromA1("A1:A2"), false);
        var existing = table.Columns[0].Name;
        Should.Throw<ArgumentException>(() => table.AddColumn(existing));
    }

    [Fact]
    public async Task TableStyle_RoundTrips()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        var sheet = document.Sheets[0];
        sheet.SetValue(1, 1, "H");
        var table = sheet.AddTable(CellRange.FromA1("A1:A3"));
        table.StyleName = "TableStyleLight1";
        table.ShowTotalsRow = true;

        using var reloaded = await XlsxFixtures.RoundTripAsync(document);
        var reloadedTable = reloaded.Sheets[0].Tables[0];
        reloadedTable.StyleName.ShouldBe("TableStyleLight1");
        reloadedTable.ShowTotalsRow.ShouldBeTrue();
    }

    [Fact]
    public void Find_ReturnsTableByName()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        document.Sheets[0].SetValue(1, 1, "H");
        var table = document.Sheets[0].Tables.Add(CellRange.FromA1("A1:A3"), "Sales");
        document.Sheets[0].Tables.Find("Sales").ShouldBe(table);
    }
}
