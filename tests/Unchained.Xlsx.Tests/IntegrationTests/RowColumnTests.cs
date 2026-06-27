using Shouldly;
using Unchained.Xlsx.Tests.Helpers;
using Xunit;

namespace Unchained.Xlsx.Tests.IntegrationTests;

public class RowColumnTests
{
    [Fact]
    public async Task InsertRow_PushesCellsDown()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        var sheet = document.Sheets[0];
        sheet.SetValue(1, 1, "Top");
        sheet.SetValue(2, 1, "Below");
        sheet.InsertRow(2);

        using var reloaded = await XlsxFixtures.RoundTripAsync(document);
        var result = reloaded.Sheets[0];
        result.GetCell(1, 1)!.GetString().ShouldBe("Top");
        result.GetCell(2, 1).ShouldBeNull();
        result.GetCell(3, 1)!.GetString().ShouldBe("Below");
    }

    [Fact]
    public async Task DeleteRow_CollapsesCells()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        var sheet = document.Sheets[0];
        sheet.SetValue(1, 1, "A");
        sheet.SetValue(2, 1, "B");
        sheet.SetValue(3, 1, "C");
        sheet.DeleteRow(2);

        using var reloaded = await XlsxFixtures.RoundTripAsync(document);
        var result = reloaded.Sheets[0];
        result.GetCell(1, 1)!.GetString().ShouldBe("A");
        result.GetCell(2, 1)!.GetString().ShouldBe("C");
        result.GetCell(3, 1).ShouldBeNull();
    }

    [Fact]
    public void InsertRow_ShiftsFormulaReferences()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        var sheet = document.Sheets[0];
        sheet.SetFormula(1, 1, "=A5*2");
        sheet.InsertRow(3);

        sheet.GetCell(1, 1)!.FormulaText.ShouldBe("=A6*2");
    }

    [Fact]
    public async Task InsertColumn_PushesCellsRight()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        var sheet = document.Sheets[0];
        sheet.SetValue(1, 1, "A");
        sheet.SetValue(1, 2, "B");
        sheet.InsertColumn(2);

        using var reloaded = await XlsxFixtures.RoundTripAsync(document);
        var result = reloaded.Sheets[0];
        result.GetCell(1, 1)!.GetString().ShouldBe("A");
        result.GetCell(1, 2).ShouldBeNull();
        result.GetCell(1, 3)!.GetString().ShouldBe("B");
    }

    [Fact]
    public async Task SetRowHeight_RoundTrips()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        document.Sheets[0].SetRowHeight(1, 30.0);

        using var reloaded = await XlsxFixtures.RoundTripAsync(document);
        reloaded.Sheets[0].GetRow(1)!.Height.ShouldBe(30.0);
    }

    [Fact]
    public async Task SetColumnWidth_RoundTrips()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        document.Sheets[0].SetColumnWidth(2, 15.5);

        using var reloaded = await XlsxFixtures.RoundTripAsync(document);
        reloaded.Sheets[0].GetColumn(2)!.Width.ShouldBe(15.5);
    }

    [Fact]
    public async Task HideRow_RoundTrips()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        document.Sheets[0].SetValue(1, 1, "x");
        document.Sheets[0].HideRow(1);

        using var reloaded = await XlsxFixtures.RoundTripAsync(document);
        reloaded.Sheets[0].GetRow(1)!.IsHidden.ShouldBeTrue();
    }

    [Fact]
    public async Task HideColumn_RoundTrips()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        document.Sheets[0].HideColumn(3);

        using var reloaded = await XlsxFixtures.RoundTripAsync(document);
        reloaded.Sheets[0].GetColumn(3)!.IsHidden.ShouldBeTrue();
    }
}
