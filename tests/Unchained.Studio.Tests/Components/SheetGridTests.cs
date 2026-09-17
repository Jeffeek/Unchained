using Bunit;
using Unchained.Studio.Components.Xlsx;
using Unchained.Xlsx.Engine;

namespace Unchained.Studio.Tests.Components;

/// <summary>Tests for the <see cref="SheetGrid" /> component.</summary>
public sealed class SheetGridTests : MudTestContext
{
    private readonly SpreadsheetProcessor _processor = new();

    [Fact]
    public void Render_BlankSheet_RendersGridMarkup()
    {
        var document = _processor.CreateBlank("Sheet1");

        var cut = Render<SheetGrid>(pb => pb.Add(static c => c.Sheet, document.Sheets[0]));

        cut.Markup.ShouldNotBeNullOrWhiteSpace();
        cut.Markup.ShouldContain("sheet-grid-wrapper");
    }

    [Fact]
    public void Render_SheetWithData_IncludesCellValue()
    {
        var document = _processor.CreateBlank("Sheet1");
        document.Sheets[0][1, 1].SetValue("VISIBLE_VALUE");

        var cut = Render<SheetGrid>(pb => pb.Add(static c => c.Sheet, document.Sheets[0]));

        cut.Markup.ShouldContain("VISIBLE_VALUE");
    }

    [Fact]
    public void Render_SheetName_DisplayedInToolbar()
    {
        var document = _processor.CreateBlank("MySheet");

        var cut = Render<SheetGrid>(pb => pb.Add(static c => c.Sheet, document.Sheets[0]));

        cut.Markup.ShouldContain("MySheet");
    }

    [Fact]
    public void Render_SheetWithNumbers_DisplaysValues()
    {
        var document = _processor.CreateBlank("Sheet1");
        document.Sheets[0][1, 1].SetValue(10);
        document.Sheets[0][1, 2].SetValue(20);
        document.Sheets[0][1, 3].SetValue(30);

        var cut = Render<SheetGrid>(pb => pb.Add(static c => c.Sheet, document.Sheets[0]));

        cut.Markup.ShouldContain("10");
        cut.Markup.ShouldContain("20");
        cut.Markup.ShouldContain("30");
    }

    [Fact]
    public void Render_ToolbarButtons_Present()
    {
        var document = _processor.CreateBlank("Sheet1");

        var cut = Render<SheetGrid>(pb => pb.Add(static c => c.Sheet, document.Sheets[0]));

        // Check toolbar has action buttons
        var buttons = cut.FindAll("button");
        buttons.Count.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void Render_MultipleRows_DisplaysGrid()
    {
        var document = _processor.CreateBlank("Sheet1");
        for (var i = 1; i <= 5; i++)
            document.Sheets[0][i, 1].SetValue($"Row{i}");

        var cut = Render<SheetGrid>(pb => pb.Add(static c => c.Sheet, document.Sheets[0]));

        cut.Markup.ShouldContain("Row1");
        cut.Markup.ShouldContain("Row2");
        cut.Markup.ShouldContain("Row5");
    }

    [Fact]
    public void Render_MultipleColumns_DisplaysGrid()
    {
        var document = _processor.CreateBlank("Sheet1");
        for (var i = 1; i <= 5; i++)
            document.Sheets[0][1, i].SetValue($"Col{i}");

        var cut = Render<SheetGrid>(pb => pb.Add(static c => c.Sheet, document.Sheets[0]));

        cut.Markup.ShouldContain("Col1");
        cut.Markup.ShouldContain("Col5");
    }

    [Fact]
    public void Render_MixedDataTypes_DisplaysAll()
    {
        var document = _processor.CreateBlank("Sheet1");
        document.Sheets[0][1, 1].SetValue("Text");
        document.Sheets[0][1, 2].SetValue(42);
        document.Sheets[0][1, 3].SetValue(3.14);
        document.Sheets[0][1, 4].SetValue(true);

        var cut = Render<SheetGrid>(pb => pb.Add(static c => c.Sheet, document.Sheets[0]));

        cut.Markup.ShouldContain("Text");
        cut.Markup.ShouldContain("42");
        cut.Markup.ShouldContain("3.14");
        // ReSharper disable once RedundantArgumentDefaultValue
        cut.Markup.ShouldContain("TRUE", Case.Insensitive);
    }
}
