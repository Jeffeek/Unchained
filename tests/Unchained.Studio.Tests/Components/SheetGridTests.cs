using Unchained.Studio.Components.Xlsx;
using Unchained.Xlsx.Engine;

namespace Unchained.Studio.Tests.Components;

/// <summary>Render tests for the <see cref="SheetGrid" /> component.</summary>
public sealed class SheetGridTests : MudTestContext
{
    private readonly SpreadsheetProcessor _processor = new();

    [Fact]
    public void Render_BlankSheet_RendersGridMarkup()
    {
        var document = _processor.CreateBlank("Sheet1");

        var cut = Render<SheetGrid>(pb => pb.Add(static c => c.Sheet, document.Sheets[0]));

        cut.Markup.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Render_SheetWithData_IncludesCellValue()
    {
        var document = _processor.CreateBlank("Sheet1");
        document.Sheets[0][1, 1].SetValue("VISIBLE_VALUE");

        var cut = Render<SheetGrid>(pb => pb.Add(static c => c.Sheet, document.Sheets[0]));

        cut.Markup.ShouldContain("VISIBLE_VALUE");
    }
}
