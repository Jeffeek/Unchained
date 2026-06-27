using Shouldly;
using Unchained.Xlsx.Models.Cell;
using Unchained.Xlsx.Tests.Helpers;
using Xunit;

namespace Unchained.Xlsx.Tests.IntegrationTests;

public class CellReadWriteTests
{
    [Fact]
    public async Task RoundTrip_NumberCell()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        document.Sheets[0].SetValue(1, 1, 42.5);

        using var reloaded = await XlsxFixtures.RoundTripAsync(document);

        var cell = reloaded.Sheets[0].GetCell(1, 1);
        cell.ShouldNotBeNull();
        cell.CellType.ShouldBe(CellType.Number);
        cell.GetDouble().ShouldBe(42.5);
    }

    [Fact]
    public async Task RoundTrip_StringCell_UsesSharedStrings()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        var sheet = document.Sheets[0];
        sheet.SetValue(1, 1, "Hello");
        sheet.SetValue(2, 1, "Hello"); // duplicate — interned once

        using var reloaded = await XlsxFixtures.RoundTripAsync(document);

        reloaded.Sheets[0].GetCell(1, 1)!.GetString().ShouldBe("Hello");
        reloaded.Sheets[0].GetCell(2, 1)!.GetString().ShouldBe("Hello");
    }

    [Fact]
    public async Task RoundTrip_BooleanCell()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        document.Sheets[0].SetValue(1, 1, true);

        using var reloaded = await XlsxFixtures.RoundTripAsync(document);

        var cell = reloaded.Sheets[0].GetCell(1, 1);
        cell!.CellType.ShouldBe(CellType.Boolean);
        cell.GetBoolean().ShouldBe(true);
    }

    [Fact]
    public async Task RoundTrip_ErrorCell()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        document.Sheets[0][1, 1].SetValue(CellError.DivisionByZero);

        using var reloaded = await XlsxFixtures.RoundTripAsync(document);

        reloaded.Sheets[0].GetCell(1, 1)!.GetError().ShouldBe(CellError.DivisionByZero);
    }

    [Fact]
    public async Task RoundTrip_DateCell()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        var date = new DateTime(2023, 6, 15);
        document.Sheets[0].SetValue(1, 1, date);

        using var reloaded = await XlsxFixtures.RoundTripAsync(document);

        reloaded.Sheets[0].GetCell(1, 1)!.GetDateTime().ShouldBe(date);
    }

    [Fact]
    public async Task RoundTrip_FormulaWithCachedValue()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        document.Sheets[0].SetFormulaWithCache(1, 1, "=SUM(A2:A10)", 55);

        using var reloaded = await XlsxFixtures.RoundTripAsync(document);

        var cell = reloaded.Sheets[0].GetCell(1, 1);
        cell!.CellType.ShouldBe(CellType.Formula);
        cell.FormulaText.ShouldBe("=SUM(A2:A10)");
        cell.GetDouble().ShouldBe(55);
    }

    [Fact]
    public async Task GetUsedRange_ReturnsBoundingBox()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        var sheet = document.Sheets[0];
        sheet.SetValue(2, 2, 1.0);
        sheet.SetValue(5, 4, 2.0);

        using var reloaded = await XlsxFixtures.RoundTripAsync(document);

        reloaded.Sheets[0].GetUsedRange()!.Value.ToA1().ShouldBe("B2:D5");
    }

    [Fact]
    public void GetCell_EmptyCell_ReturnsNull()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        document.Sheets[0].GetCell(99, 99).ShouldBeNull();
    }

    [Fact]
    public async Task ClearCell_RemovesValue()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        var sheet = document.Sheets[0];
        sheet.SetValue(1, 1, 5.0);
        sheet.ClearCell(1, 1);

        using var reloaded = await XlsxFixtures.RoundTripAsync(document);
        reloaded.Sheets[0].GetCell(1, 1).ShouldBeNull();
    }

    [Fact]
    public async Task Indexer_ByA1_Works()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        document.Sheets[0]["C3"].SetValue(7.0);

        using var reloaded = await XlsxFixtures.RoundTripAsync(document);
        reloaded.Sheets[0].GetCell(3, 3)!.GetDouble().ShouldBe(7.0);
    }

    [Fact]
    public async Task ImportArray_WritesGrid()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        var data = new object?[,] { { "Name", "Age" }, { "Alice", 30.0 } };
        document.Sheets[0].ImportArray(data, 1, 1);

        using var reloaded = await XlsxFixtures.RoundTripAsync(document);
        var sheet = reloaded.Sheets[0];
        sheet.GetCell(1, 1)!.GetString().ShouldBe("Name");
        sheet.GetCell(2, 2)!.GetDouble().ShouldBe(30.0);
    }
}
