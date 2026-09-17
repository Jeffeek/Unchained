using Shouldly;
using Unchained.Xlsx.Models.Cell;
using Unchained.Xlsx.Tests.Helpers;
using Xunit;

namespace Unchained.Xlsx.Tests.UnitTests.Cell;

/// <summary>Tests for <see cref="Unchained.Xlsx.Cell.Cell" /> value getters/setters and type conversion.</summary>
public sealed class CellTests
{
    [Fact]
    public void NumberValue_NumberCell_ReturnsDouble()
    {
        using var doc = XlsxFixtures.WithSheets("Sheet1");
        var sheet = doc.Sheets[0];
        sheet.SetValue(1, 1, 42.5);

        var cell = sheet.GetCell(1, 1);
        cell.ShouldNotBeNull();
        cell.NumberValue.ShouldBe(42.5);
        cell.CellType.ShouldBe(CellType.Number);
    }

    [Fact]
    public void StringValue_StringCell_ReturnsString()
    {
        using var doc = XlsxFixtures.WithSheets("Sheet1");
        var sheet = doc.Sheets[0];
        sheet.SetValue(1, 1, "test");

        var cell = sheet.GetCell(1, 1);
        cell.ShouldNotBeNull();
        cell.StringValue.ShouldBe("test");
        cell.CellType.ShouldBe(CellType.String);
    }

    [Fact]
    public void BooleanValue_BooleanCell_ReturnsBool()
    {
        using var doc = XlsxFixtures.WithSheets("Sheet1");
        var sheet = doc.Sheets[0];
        sheet.SetValue(1, 1, true);

        var cell = sheet.GetCell(1, 1);
        cell.ShouldNotBeNull();
        cell.BooleanValue.ShouldBe(true);
        cell.CellType.ShouldBe(CellType.Boolean);
    }

    [Fact]
    public void NumberValue_DateCell_ReturnsSerialNumber()
    {
        using var doc = XlsxFixtures.WithSheets("Sheet1");
        var sheet = doc.Sheets[0];
        var date = new DateTime(2024, 1, 15);
        sheet.SetValue(1, 1, date);

        var cell = sheet.GetCell(1, 1);
        cell.ShouldNotBeNull();
        cell.NumberValue.ShouldNotBeNull();
        cell.CellType.ShouldBe(CellType.Number);
    }

    [Fact]
    public void GetDouble_NumberCell_ReturnsValue()
    {
        using var doc = XlsxFixtures.WithSheets("Sheet1");
        var sheet = doc.Sheets[0];
        sheet.SetValue(1, 1, 123.45);

        var cell = sheet.GetCell(1, 1);
        cell.ShouldNotBeNull();
        cell.GetDouble().ShouldBe(123.45);
    }

    [Fact]
    public void GetString_StringCell_ReturnsValue()
    {
        using var doc = XlsxFixtures.WithSheets("Sheet1");
        var sheet = doc.Sheets[0];
        sheet.SetValue(1, 1, "hello");

        var cell = sheet.GetCell(1, 1);
        cell.ShouldNotBeNull();
        cell.GetString().ShouldBe("hello");
    }

    [Fact]
    public void GetBoolean_BooleanCell_ReturnsValue()
    {
        using var doc = XlsxFixtures.WithSheets("Sheet1");
        var sheet = doc.Sheets[0];
        sheet.SetValue(1, 1, false);

        var cell = sheet.GetCell(1, 1);
        cell.ShouldNotBeNull();
        cell.GetBoolean().ShouldBe(false);
    }

    [Fact]
    public void SetValue_OverwritesExistingValue()
    {
        using var doc = XlsxFixtures.WithSheets("Sheet1");
        var sheet = doc.Sheets[0];
        sheet.SetValue(1, 1, "first");
        sheet.SetValue(1, 1, 123.0);

        var cell = sheet.GetCell(1, 1);
        cell.ShouldNotBeNull();
        cell.NumberValue.ShouldBe(123.0);
        cell.StringValue.ShouldBeNull();
    }

    [Fact]
    public void SetFormula_StoresFormula()
    {
        using var doc = XlsxFixtures.WithSheets("Sheet1");
        var sheet = doc.Sheets[0];
        sheet.SetFormula(1, 1, "=1+2");

        var cell = sheet.GetCell(1, 1);
        cell.ShouldNotBeNull();
        cell.Formula.ShouldBe("1+2"); // Formula stored without leading =
    }

    [Fact]
    public void Reference_ReturnsCorrectA1Address()
    {
        using var doc = XlsxFixtures.WithSheets("Sheet1");
        var sheet = doc.Sheets[0];
        sheet.SetValue(5, 10, "test"); // Row 5, Col 10 = J5

        var cell = sheet.GetCell(5, 10);
        cell.ShouldNotBeNull();
        cell.Reference.ToString().ShouldBe("J5");
    }

    [Fact]
    public void Row_ReturnsCorrectRowNumber()
    {
        using var doc = XlsxFixtures.WithSheets("Sheet1");
        var sheet = doc.Sheets[0];
        sheet.SetValue(10, 5, "test");

        var cell = sheet.GetCell(10, 5);
        cell.ShouldNotBeNull();
        cell.Row.ShouldBe(10);
    }

    [Fact]
    public void Column_ReturnsCorrectColumnNumber()
    {
        using var doc = XlsxFixtures.WithSheets("Sheet1");
        var sheet = doc.Sheets[0];
        sheet.SetValue(5, 15, "test");

        var cell = sheet.GetCell(5, 15);
        cell.ShouldNotBeNull();
        cell.Column.ShouldBe(15);
    }

    [Fact]
    public void NumberValue_NonNumberCell_ReturnsNull()
    {
        using var doc = XlsxFixtures.WithSheets("Sheet1");
        var sheet = doc.Sheets[0];
        sheet.SetValue(1, 1, "text");

        var cell = sheet.GetCell(1, 1);
        cell.ShouldNotBeNull();
        cell.NumberValue.ShouldBeNull();
    }

    [Fact]
    public void StringValue_NonStringCell_ReturnsNull()
    {
        using var doc = XlsxFixtures.WithSheets("Sheet1");
        var sheet = doc.Sheets[0];
        sheet.SetValue(1, 1, 42.0);

        var cell = sheet.GetCell(1, 1);
        cell.ShouldNotBeNull();
        cell.StringValue.ShouldBeNull();
    }

    [Fact]
    public void BooleanValue_NonBooleanCell_ReturnsNull()
    {
        using var doc = XlsxFixtures.WithSheets("Sheet1");
        var sheet = doc.Sheets[0];
        sheet.SetValue(1, 1, "text");

        var cell = sheet.GetCell(1, 1);
        cell.ShouldNotBeNull();
        cell.BooleanValue.ShouldBeNull();
    }
}
