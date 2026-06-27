using Bunit;
using Microsoft.AspNetCore.Components;
using MudBlazor.Services;
using Unchained.Studio.Components.Xlsx;
using Unchained.Xlsx.Worksheets;

namespace Unchained.Studio.Tests.Components;

public sealed class FormulaBarTests : MudTestContext
{
    public FormulaBarTests()
    {
    }

    [Fact]
    public void Render_NoSheet_NoPreview()
    {
        var cut = Render<FormulaBar>();

        cut.FindAll(".formula-bar-preview").ShouldBeEmpty();
    }

    [Fact]
    public void Render_CellValue_SimpleValueShown()
    {
        var cut = Render<FormulaBar>(pb => pb.Add(c => c.CellText, "42"));

        cut.Find(".formula-bar-input").GetAttribute("value").ShouldBe("42");
    }

    [Fact]
    public void Render_BlankCell_EmptyShown()
    {
        var cut = Render<FormulaBar>(pb => pb.Add(c => c.CellText, string.Empty));

        var value = cut.Find(".formula-bar-input").GetAttribute("value");
        (value ?? string.Empty).ShouldBe(string.Empty);
    }

    [Fact]
    public void Input_ValidFormula_Evaluates()
    {
        var cut = Render<FormulaBar>(pb =>
            pb.Add(c => c.Sheet, CreateSheet())
              .Add(c => c.CellText, "=10+5"));

        cut.Find(".formula-bar-preview").TextContent.ShouldContain("15");
    }

    [Fact]
    public void Input_ErrorFormula_ShowsError()
    {
        var cut = Render<FormulaBar>(pb =>
            pb.Add(c => c.Sheet, CreateSheet())
              .Add(c => c.CellText, "=1/0"));

        cut.Find(".formula-bar-preview").ClassList.ShouldContain("formula-bar-preview--error");
    }

    [Fact]
    public void Input_Commits_OnEnter()
    {
        var committed = false;
        var cut = Render<FormulaBar>(pb =>
            pb.Add(c => c.CellText, "hello")
              .Add(c => c.Committed, EventCallback.Factory.Create<string>(this, _ => committed = true)));

        var input = cut.Find(".formula-bar-input");
        input.Input("world");
        input.KeyDown("Enter");

        committed.ShouldBeTrue();
    }

    [Fact]
    public void Input_Cancelled_OnEscape()
    {
        var cut = Render<FormulaBar>(pb =>
            pb.Add(c => c.CellText, "original")
              .Add(c => c.Committed, EventCallback.Factory.Create<string>(this, _ => { })));

        var input = cut.Find(".formula-bar-input");
        input.Input("changed");
        input.KeyDown("Escape");

        cut.Find(".formula-bar-input").GetAttribute("value").ShouldBe("original");
    }

    private static Worksheet CreateSheet()
    {
        using var processor = new Unchained.Xlsx.Engine.SpreadsheetProcessor();
        var doc = processor.CreateBlank("Sheet1");
        return doc.Sheets[0];
    }
}
