using Unchained.Studio.Models;
using Unchained.Studio.Studio.Xlsx;
using Unchained.Xlsx.Engine;
using Unchained.Xlsx.Models.Cell;
using Unchained.Xlsx.Models.Sheets;

namespace Unchained.Studio.Tests.Studio;

/// <summary>
///     Tests for <see cref="XlsxTreeBuilder" /> — builds the navigable tree for a loaded workbook:
///     document → Properties, Sheets (with tables), Defined Names, Styles.
/// </summary>
public sealed class XlsxTreeBuilderTests
{
    private static SpreadsheetDocument NewDocument(string firstSheet = "Data")
    {
        using var processor = new SpreadsheetProcessor();
        return processor.CreateBlank(firstSheet);
    }

    [Fact]
    public void Build_Root_UsesFileNameAndIsExpandedDocumentNode()
    {
        var document = NewDocument();

        var root = XlsxTreeBuilder.Build(document, "report.xlsx");

        root.Label.ShouldBe("report.xlsx");
        root.NodeType.ShouldBe(TreeNodeType.Document);
        root.IsExpanded.ShouldBeTrue();
        root.Payload.ShouldBe(document);
    }

    [Fact]
    public void Build_Root_HasPropertiesSheetsDefinedNamesAndStylesChildren()
    {
        var document = NewDocument();

        var root = XlsxTreeBuilder.Build(document, "book.xlsx");

        root.Children.Count.ShouldBe(4);
        root.Children[0].NodeType.ShouldBe(TreeNodeType.Metadata);
        root.Children[0].Label.ShouldBe("Properties");
        root.Children[1].NodeType.ShouldBe(TreeNodeType.Pages);
        root.Children[3].Label.ShouldStartWith("Styles (");
    }

    [Fact]
    public void Build_SheetsNode_ReportsSheetCountAndListsSheets()
    {
        var document = NewDocument("First");
        document.Sheets.Add("Second");

        var sheetsNode = XlsxTreeBuilder.Build(document, "book.xlsx").Children[1];

        sheetsNode.Label.ShouldBe("Sheets (2)");
        sheetsNode.Children.Count.ShouldBe(2);
        sheetsNode.Children[0].Label.ShouldBe("First");
        sheetsNode.Children[0].NodeType.ShouldBe(TreeNodeType.Sheet);
    }

    [
        Theory,
        InlineData(SheetState.Hidden, " (hidden)"),
        InlineData(SheetState.VeryHidden, " (very hidden)"),
        InlineData(SheetState.Visible, "")
    ]
    public void Build_SheetNode_AppendsStateSuffix(SheetState state, string expectedSuffix)
    {
        var document = NewDocument("Sheet1");
        document.Sheets[0].State = state;

        var sheetNode = XlsxTreeBuilder.Build(document, "book.xlsx").Children[1].Children[0];

        sheetNode.Label.ShouldBe($"Sheet1{expectedSuffix}");
    }

    [Fact]
    public void Build_SheetWithTable_AddsTableChildNode()
    {
        var document = NewDocument();
        var table = document.Sheets[0].AddTable(CellRange.FromA1("A1:C2"));

        var sheetNode = XlsxTreeBuilder.Build(document, "book.xlsx").Children[1].Children[0];

        var tableNode = sheetNode.Children.ShouldHaveSingleItem();
        tableNode.Payload.ShouldBe(table);
        tableNode.Label.ShouldContain("A1:C2");
    }

    [Fact]
    public void Build_DefinedNames_ReportsCountAndListsEachName()
    {
        var document = NewDocument();
        document.DefinedNames.Add("MyRange", "Data!$A$1:$C$10");

        var namesNode = XlsxTreeBuilder.Build(document, "book.xlsx").Children[2];

        namesNode.NodeType.ShouldBe(TreeNodeType.NamedDestinationGroup);
        namesNode.Label.ShouldBe("Defined Names (1)");
        var nameNode = namesNode.Children.ShouldHaveSingleItem();
        nameNode.Label.ShouldBe("MyRange");
        nameNode.NodeType.ShouldBe(TreeNodeType.NamedDestination);
    }

    [Fact]
    public void Build_NoDefinedNames_ReportsZeroAndHasNoChildren()
    {
        var document = NewDocument();

        var namesNode = XlsxTreeBuilder.Build(document, "book.xlsx").Children[2];

        namesNode.Label.ShouldBe("Defined Names (0)");
        namesNode.Children.ShouldBeEmpty();
    }
}
