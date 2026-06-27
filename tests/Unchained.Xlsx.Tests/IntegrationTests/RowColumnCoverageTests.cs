using Shouldly;
using Unchained.Xlsx.Models.Cell;
using Unchained.Xlsx.Tests.Helpers;
using Xunit;

namespace Unchained.Xlsx.Tests.IntegrationTests;

/// <summary>Branch coverage for row/column shifting, properties, and used-range/text helpers.</summary>
public class RowColumnCoverageTests
{
    [Fact]
    public async Task DeleteColumn_CollapsesCells()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        var sheet = document.Sheets[0];
        sheet.SetValue(1, 1, "A");
        sheet.SetValue(1, 2, "B");
        sheet.SetValue(1, 3, "C");
        sheet.DeleteColumn(2);

        using var reloaded = await XlsxFixtures.RoundTripAsync(document);
        var result = reloaded.Sheets[0];
        result.GetCell(1, 1)!.GetString().ShouldBe("A");
        result.GetCell(1, 2)!.GetString().ShouldBe("C");
        result.GetCell(1, 3).ShouldBeNull();
    }

    [Fact]
    public void DeleteColumn_ShiftsFormulaReferences()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        var sheet = document.Sheets[0];
        sheet.SetFormula(1, 1, "=E1*2");
        sheet.DeleteColumn(3);

        sheet.GetCell(1, 1)!.FormulaText.ShouldBe("=D1*2");
    }

    [Fact]
    public void InsertRow_PreservesRowProperties()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        var sheet = document.Sheets[0];
        sheet.SetValue(2, 1, "x");
        sheet.SetRowHeight(2, 40);
        sheet.HideRow(2);
        sheet.InsertRow(1);

        var moved = sheet.GetRow(3);
        moved.ShouldNotBeNull();
        moved.Height.ShouldBe(40);
        moved.IsHidden.ShouldBeTrue();
    }

    [Fact]
    public void ShowRow_UnhidesRow()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        var sheet = document.Sheets[0];
        sheet.HideRow(1);
        sheet.ShowRow(1);
        sheet.GetRow(1)!.IsHidden.ShouldBeFalse();
    }

    [Fact]
    public void ShowRow_OnMissingRow_DoesNothing()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        Should.NotThrow(() => document.Sheets[0].ShowRow(99));
    }

    [Fact]
    public void ShowColumn_UnhidesColumn()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        var sheet = document.Sheets[0];
        sheet.HideColumn(2);
        sheet.ShowColumn(2);
        sheet.GetColumn(2)!.IsHidden.ShouldBeFalse();
    }

    [Fact]
    public void GetAllText_ReturnsTabSeparatedGrid()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        var sheet = document.Sheets[0];
        sheet.SetValue(1, 1, "a");
        sheet.SetValue(1, 2, "b");
        sheet.SetValue(2, 1, 1.0);

        var text = sheet.GetAllText();
        text.ShouldContain("a\tb");
    }

    [Fact]
    public void GetAllText_EmptySheet_ReturnsEmpty()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        document.Sheets[0].GetAllText().ShouldBe(string.Empty);
    }

    [Fact]
    public void GetUsedRange_EmptySheet_ReturnsNull()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        document.Sheets[0].GetUsedRange().ShouldBeNull();
    }

    [Fact]
    public void ClearRange_RemovesAllCells()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        var sheet = document.Sheets[0];
        sheet.SetValue(1, 1, 1.0);
        sheet.SetValue(2, 2, 2.0);
        sheet.ClearRange(CellRange.FromA1("A1:B2"));

        sheet.GetUsedRange().ShouldBeNull();
    }

    [Fact]
    public void ImportRows_WritesSequence()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        var sheet = document.Sheets[0];
        sheet.ImportRows(
            [
                ["Name", "Age"],
                ["Alice", 30.0]
            ],
            1,
            1
        );

        sheet.GetCell(1, 1)!.GetString().ShouldBe("Name");
        sheet.GetCell(2, 2)!.GetDouble().ShouldBe(30.0);
    }

    [Fact]
    public void SetValue_ObjectOverload_DispatchesByType()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        var sheet = document.Sheets[0];
        sheet.SetValue(1, 1, (object?)"text");
        sheet.SetValue(2, 1, (object?)true);
        sheet.SetValue(3, 1, (object?)42);
        sheet.SetValue(4, 1, (object?)new DateTime(2023, 1, 1));
        sheet.SetValue(5, 1, (object?)null);

        sheet.GetCell(1, 1)!.GetString().ShouldBe("text");
        sheet.GetCell(2, 1)!.GetBoolean().ShouldBe(true);
        sheet.GetCell(3, 1)!.GetDouble().ShouldBe(42.0);
        sheet.GetCell(4, 1)!.GetDateTime().ShouldNotBeNull();
        sheet.GetCell(5, 1).ShouldBeNull();
    }

    [Fact]
    public void SetValue_UnsupportedType_Throws()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        Should.Throw<ArgumentException>(() => document.Sheets[0].SetValue(1, 1, (object?)new object()));
    }

    [Fact]
    public void SetFormula_EmptyThrows()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        Should.Throw<ArgumentException>(() => document.Sheets[0].SetFormula(1, 1, ""));
    }
}
