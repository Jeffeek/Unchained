using Unchained.Studio.Models;
using Unchained.Studio.Studio.Xlsx;
using Unchained.Xlsx.Engine;

namespace Unchained.Studio.Tests.Studio;

/// <summary>
///     Tests for <see cref="XlsxPropertyAdapter" /> — turns a selected tree node (or cell) into a
///     <see cref="PropertyBag" /> for the properties panel.
/// </summary>
public sealed class XlsxPropertyAdapterTests
{
    private static SpreadsheetDocument NewDocument(string firstSheet = "Data")
    {
        using var processor = new SpreadsheetProcessor();
        return processor.CreateBlank(firstSheet);
    }

    private static PropertyEntry Entry(PropertyBag bag, string key) =>
        bag.Groups.SelectMany(static g => g.Entries).First(e => e.Key == key);

    [Fact]
    public void Build_MetadataNode_ReturnsWorkbookPropertiesWithThreeGroups()
    {
        var document = NewDocument();
        document.Properties.Title = "Quarterly";
        var node = new TreeNode { NodeType = TreeNodeType.Metadata, Payload = document.Properties };

        var bag = XlsxPropertyAdapter.Build(node);

        bag.Title.ShouldBe("Workbook Properties");
        bag.Groups.Select(static g => g.Header).ShouldBe(["Core", "Dates", "Application"]);
        Entry(bag, "Title").DisplayValue.ShouldBe("Quarterly");
        Entry(bag, "Author").DisplayValue.ShouldBe("(absent)");
    }

    [Fact]
    public void Build_SheetNode_ReturnsWorksheetIdentityAndContent()
    {
        var document = NewDocument("Report");
        document.Sheets[0][1, 1].SetValue(42.0);
        var node = new TreeNode { NodeType = TreeNodeType.Sheet, Payload = document.Sheets[0] };

        var bag = XlsxPropertyAdapter.Build(node);

        bag.Title.ShouldBe("Report");
        bag.Subtitle.ShouldBe("Worksheet");
        Entry(bag, "Name").DisplayValue.ShouldBe("Report");
        Entry(bag, "Used range").DisplayValue.ShouldBe("A1:A1");
        Entry(bag, "Sheet ID").Kind.ShouldBe(PropertyValueKind.Number);
    }

    [Fact]
    public void Build_DefinedNameNode_ReturnsFormulaAndWorkbookScope()
    {
        var document = NewDocument();
        var name = document.DefinedNames.Add("MyRange", "Data!$A$1:$C$10");
        var node = new TreeNode { NodeType = TreeNodeType.NamedDestination, Payload = name };

        var bag = XlsxPropertyAdapter.Build(node);

        bag.Title.ShouldBe("MyRange");
        bag.Subtitle.ShouldBe("Defined Name");
        Entry(bag, "Refers to").DisplayValue.ShouldBe("Data!$A$1:$C$10");
        Entry(bag, "Scope").DisplayValue.ShouldBe("Workbook");
    }

    [Fact]
    public void Build_StylesNode_ReturnsNumericStyleCounts()
    {
        var document = NewDocument();
        var node = new TreeNode { NodeType = TreeNodeType.Generic, Payload = document.Styles };

        var bag = XlsxPropertyAdapter.Build(node);

        bag.Title.ShouldBe("Styles");
        Entry(bag, "Fonts").Kind.ShouldBe(PropertyValueKind.Number);
        Entry(bag, "Cell formats (cellXfs)").Kind.ShouldBe(PropertyValueKind.Number);
    }

    [Fact]
    public void Build_UnmatchedNodeType_ReturnsEmptyBagWithNodeLabel()
    {
        var document = NewDocument();
        var node = new TreeNode { NodeType = TreeNodeType.Document, Payload = document, Label = "book.xlsx" };

        var bag = XlsxPropertyAdapter.Build(node);

        bag.Title.ShouldBe("book.xlsx");
        bag.Groups.ShouldBeEmpty();
    }

    [Fact]
    public void Build_MatchingTypeButWrongPayload_ReturnsEmptyBag()
    {
        // NodeType says Sheet, but the payload is not a Worksheet — must fall through to Empty.
        var node = new TreeNode { NodeType = TreeNodeType.Sheet, Payload = "not a sheet", Label = "bogus" };

        var bag = XlsxPropertyAdapter.Build(node);

        bag.Title.ShouldBe("bogus");
        bag.Groups.ShouldBeEmpty();
    }

    [Fact]
    public void ForCell_ReturnsReferenceValueAndTypedEntries()
    {
        var document = NewDocument();
        var cell = document.Sheets[0][1, 1];
        cell.SetValue("hello");

        var bag = XlsxPropertyAdapter.ForCell(cell);

        bag.Title.ShouldBe("A1");
        bag.Subtitle.ShouldBe("Cell");
        Entry(bag, "Reference").DisplayValue.ShouldBe("A1");
        Entry(bag, "Value").DisplayValue.ShouldBe("hello");
        Entry(bag, "Merged").Kind.ShouldBe(PropertyValueKind.Boolean);
    }
}
