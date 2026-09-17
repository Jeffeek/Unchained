using Shouldly;
using Unchained.Xlsx.Engine;
using Unchained.Xlsx.Pivot;
using Xunit;

namespace Unchained.Xlsx.Tests.UnitTests.Pivot;

/// <summary>Tests for <see cref="PivotCacheValue" /> struct and conversion.</summary>
public sealed class PivotCacheValueTests
{
    [Fact]
    public void Blank_IsBlankKind() =>
        PivotCacheValue.Blank.Kind.ShouldBe(PivotCacheValueKind.Blank);

    [Fact]
    public void FromCell_Null_ReturnsBlank()
    {
        var value = PivotCacheValue.FromCell(null);
        value.Kind.ShouldBe(PivotCacheValueKind.Blank);
    }

    [Fact]
    public void FromCell_NumberCell_ReturnsNumber()
    {
        using var processor = new SpreadsheetProcessor();
        using var doc = processor.CreateBlank("Sheet1");
        var sheet = doc.Sheets[0];
        sheet.SetValue(1, 1, 42.5);

        var cell = sheet.GetCell(1, 1);
        var value = PivotCacheValue.FromCell(cell);

        value.Kind.ShouldBe(PivotCacheValueKind.Number);
        value.Number.ShouldBe(42.5);
    }

    [Fact]
    public void FromCell_BooleanCell_ReturnsBoolean()
    {
        using var processor = new SpreadsheetProcessor();
        using var doc = processor.CreateBlank("Sheet1");
        var sheet = doc.Sheets[0];
        sheet.SetValue(1, 1, true);

        var cell = sheet.GetCell(1, 1);
        var value = PivotCacheValue.FromCell(cell);

        value.Kind.ShouldBe(PivotCacheValueKind.Boolean);
        value.Boolean.ShouldBeTrue();
    }

    [Fact]
    public void FromCell_StringCell_ReturnsText()
    {
        using var processor = new SpreadsheetProcessor();
        using var doc = processor.CreateBlank("Sheet1");
        var sheet = doc.Sheets[0];
        sheet.SetValue(1, 1, "Hello");

        var cell = sheet.GetCell(1, 1);
        var value = PivotCacheValue.FromCell(cell);

        value.Kind.ShouldBe(PivotCacheValueKind.Text);
        value.Text.ShouldBe("Hello");
    }

    [Fact]
    public void FromCell_FormulaWithNumberResult_ReturnsNumber()
    {
        using var processor = new SpreadsheetProcessor();
        using var doc = processor.CreateBlank("Sheet1");
        var sheet = doc.Sheets[0];
        sheet.SetFormula(1, 1, "=2+3");
        doc.Recalculate();

        var cell = sheet.GetCell(1, 1);
        var value = PivotCacheValue.FromCell(cell);

        value.Kind.ShouldBe(PivotCacheValueKind.Number);
        value.Number.ShouldBe(5.0);
    }

    [Fact]
    public void FromCell_FormulaWithTextResult_ReturnsText()
    {
        using var processor = new SpreadsheetProcessor();
        using var doc = processor.CreateBlank("Sheet1");
        var sheet = doc.Sheets[0];
        sheet.SetFormula(1, 1, "=\"test\"");
        doc.Recalculate();

        var cell = sheet.GetCell(1, 1);
        var value = PivotCacheValue.FromCell(cell);

        value.Kind.ShouldBe(PivotCacheValueKind.Text);
        value.Text.ShouldBe("test");
    }

    [Fact]
    public void ToString_Number_ReturnsNumberString()
    {
        using var processor = new SpreadsheetProcessor();
        using var doc = processor.CreateBlank("Sheet1");
        var sheet = doc.Sheets[0];
        sheet.SetValue(1, 1, 123.45);

        var cell = sheet.GetCell(1, 1);
        var value = PivotCacheValue.FromCell(cell);

        value.ToString().ShouldBe("123.45");
    }

    [Fact]
    public void ToString_Text_ReturnsText()
    {
        using var processor = new SpreadsheetProcessor();
        using var doc = processor.CreateBlank("Sheet1");
        var sheet = doc.Sheets[0];
        sheet.SetValue(1, 1, "Hello");

        var cell = sheet.GetCell(1, 1);
        var value = PivotCacheValue.FromCell(cell);

        value.ToString().ShouldBe("Hello");
    }

    [Fact]
    public void ToString_BooleanTrue_Returns1()
    {
        using var processor = new SpreadsheetProcessor();
        using var doc = processor.CreateBlank("Sheet1");
        var sheet = doc.Sheets[0];
        sheet.SetValue(1, 1, true);

        var cell = sheet.GetCell(1, 1);
        var value = PivotCacheValue.FromCell(cell);

        value.ToString().ShouldBe("1");
    }

    [Fact]
    public void ToString_BooleanFalse_Returns0()
    {
        using var processor = new SpreadsheetProcessor();
        using var doc = processor.CreateBlank("Sheet1");
        var sheet = doc.Sheets[0];
        sheet.SetValue(1, 1, false);

        var cell = sheet.GetCell(1, 1);
        var value = PivotCacheValue.FromCell(cell);

        value.ToString().ShouldBe("0");
    }

    [Fact]
    public void ToString_Blank_ReturnsEmptyString() =>
        PivotCacheValue.Blank.ToString().ShouldBe(string.Empty);
}
