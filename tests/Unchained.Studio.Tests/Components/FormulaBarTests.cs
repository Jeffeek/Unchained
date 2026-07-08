using System.Reflection;
using Bunit;
using Microsoft.AspNetCore.Components;
using Unchained.Studio.Components.Xlsx;
using Unchained.Xlsx.Engine;
using Unchained.Xlsx.Models.Cell;
using Unchained.Xlsx.Worksheets;

namespace Unchained.Studio.Tests.Components;

public sealed class FormulaBarTests : MudTestContext
{
    private SpreadsheetProcessor? _processor;

    [Fact]
    public void Render_NoSheet_NoPreview()
    {
        var cut = Render<FormulaBar>();

        cut.FindAll(".formula-bar-preview").ShouldBeEmpty();
    }

    [Fact]
    public void Render_CellValue_SimpleValueShown()
    {
        var cut = Render<FormulaBar>(static pb => pb.Add(static c => c.CellText, "42"));

        cut.Find(".formula-bar-input").GetAttribute("value").ShouldBe("42");
    }

    [Fact]
    public void Render_BlankCell_EmptyShown()
    {
        var cut = Render<FormulaBar>(static pb => pb.Add(static c => c.CellText, string.Empty));

        cut.Find(".formula-bar-input").GetAttribute("value").ShouldBe(string.Empty);
    }

    [Fact]
    public void Input_ValidFormula_Evaluates()
    {
        var cut = Render<FormulaBar>(pb =>
            pb.Add(static c => c.Sheet, CreateSheet())
                .Add(static c => c.CellText, "=10+5")
        );

        cut.Find(".formula-bar-preview").TextContent.ShouldContain("15");
    }

    [Fact]
    public void Input_ErrorFormula_ShowsError()
    {
        var cut = Render<FormulaBar>(pb =>
            pb.Add(static c => c.Sheet, CreateSheet())
                .Add(static c => c.CellText, "=1/0")
        );

        cut.Find(".formula-bar-preview").ClassList.ShouldContain("formula-bar-preview--error");
    }

    [Fact]
    public void Input_Commits_OnEnter()
    {
        var committed = false;
        var cut = Render<FormulaBar>(pb =>
            pb.Add(static c => c.CellText, "hello")
                .Add(static c => c.Committed, EventCallback.Factory.Create<string>(this, _ => committed = true))
        );

        var input = cut.Find(".formula-bar-input");
        input.Input("world");
        input.KeyDown("Enter");

        committed.ShouldBeTrue();
    }

    [Fact]
    public void Input_Cancelled_OnEscape_ResetsToOriginalCellText()
    {
        var cut = Render<FormulaBar>(pb =>
            pb.Add(static c => c.CellText, "original")
                .Add(static c => c.Committed, EventCallback.Factory.Create<string>(this, static _ => { }))
        );

        var input = cut.Find(".formula-bar-input");
        input.Input("changed");
        input.KeyDown("Escape");

        cut.Find(".formula-bar-input").GetAttribute("value").ShouldBe("original");
    }

    [Fact]
    public void Input_CellReference_InsertedIntoInternalText()
    {
        var cut = Render<FormulaBar>(pb =>
            pb.Add(static c => c.Sheet, CreateSheet())
                .Add(static c => c.CellText, "=SUM(A")
                .Add(static c => c.IsActive, true)
                .Add(static c => c.Reference, new CellReference(2, 3))
        );

        // OnCellClicked mutates the internal text buffer.
        cut.Instance.OnCellClicked(new CellReference(2, 3), 6);

        // Verify internal state (the component's OnParametersSet would normally
        // sync this to the input on the next parameter change cycle).
        var textField = cut.Instance.GetType().GetField("_text", BindingFlags.NonPublic | BindingFlags.Instance);
        textField.ShouldNotBeNull();
        textField.GetValue(cut.Instance).ShouldBe("=SUM(A$C$2");
    }

    private Worksheet CreateSheet()
    {
        _processor?.Dispose();
        _processor = new SpreadsheetProcessor();
        var doc = _processor.CreateBlank("Sheet1");
        return doc.Sheets[0];
    }
}
