using Unchained.Studio.Models;
using Unchained.Xlsx.Abstractions;
using Unchained.Xlsx.Drawings;
using Unchained.Xlsx.Engine;
using Unchained.Xlsx.Models.Sheets;
using Unchained.Xlsx.Tables;
using Unchained.Xlsx.Worksheets;

namespace Unchained.Studio.Studio.Xlsx;

/// <summary>
///     Builds the navigable <see cref="TreeNode" /> hierarchy for a loaded
///     <see cref="SpreadsheetDocument" />: document → properties, worksheets (with tables), defined
///     names, and a styles summary.
/// </summary>
public static class XlsxTreeBuilder
{
    public static TreeNode Build(SpreadsheetDocument document, string fileName) => new()
    {
        Label = fileName,
        Icon = Icons.Workbook,
        NodeType = TreeNodeType.Document,
        Payload = document,
        IsExpanded = true,
        Children =
        [
            BuildPropertiesNode(document),
            BuildSheetsNode(document),
            BuildDefinedNamesNode(document),
            BuildStylesNode(document)
        ]
    };

    private static TreeNode BuildPropertiesNode(ISpreadsheetDocument document) => new()
    {
        Label = "Properties",
        Icon = Icons.Info,
        NodeType = TreeNodeType.Metadata,
        Payload = document.Properties
    };

    private static TreeNode BuildSheetsNode(ISpreadsheetDocument document)
    {
        var node = new TreeNode
        {
            Label = $"Sheets ({document.Sheets.Count})",
            Icon = Icons.Collections,
            NodeType = TreeNodeType.Pages,
            Payload = document,
            IsExpanded = true
        };

        foreach (var sheet in document.Sheets)
            node.Children.Add(BuildSheetNode(sheet));

        return node;
    }

    private static TreeNode BuildSheetNode(Worksheet sheet)
    {
        var stateSuffix = sheet.State switch
        {
            SheetState.Hidden => " (hidden)",
            SheetState.VeryHidden => " (very hidden)",
            _ => string.Empty
        };

        var node = new TreeNode
        {
            Label = $"{sheet.Name}{stateSuffix}",
            Icon = Icons.Sheet,
            NodeType = TreeNodeType.Sheet,
            Payload = sheet
        };

        foreach (var table in sheet.Tables)
            node.Children.Add(BuildTableNode(table));

        foreach (var drawing in sheet.Drawings)
            node.Children.Add(BuildDrawingNode(drawing));

        return node;
    }

    private static TreeNode BuildDrawingNode(WorksheetDrawing drawing)
    {
        var (label, icon) = drawing switch
        {
            PictureDrawing pic =>
                ($"Image ({pic.Image.ContentType}) @ {pic.Anchor.From.ToA1()}", Icons.Image),
            ChartDrawing chart =>
                ($"{chart.Chart.Type} chart @ {chart.Anchor.From.ToA1()}", Icons.Chart),
            _ => ("Drawing", Icons.Image)
        };

        return new TreeNode
        {
            Label = label,
            Icon = icon,
            NodeType = TreeNodeType.Generic,
            Payload = drawing
        };
    }

    private static TreeNode BuildTableNode(ListObject table) => new()
    {
        Label = $"{table.DisplayName} ({table.Range.ToA1()})",
        Icon = Icons.Table,
        NodeType = TreeNodeType.Generic,
        Payload = table
    };

    private static TreeNode BuildDefinedNamesNode(ISpreadsheetDocument document)
    {
        var names = document.DefinedNames;
        var node = new TreeNode
        {
            Label = $"Defined Names ({names.Count})",
            Icon = Icons.Tag,
            NodeType = TreeNodeType.NamedDestinationGroup,
            Payload = document
        };

        foreach (var name in names)
        {
            node.Children.Add(
                new TreeNode
                {
                    Label = name.Name,
                    Icon = Icons.Tag,
                    NodeType = TreeNodeType.NamedDestination,
                    Payload = name
                }
            );
        }

        return node;
    }

    private static TreeNode BuildStylesNode(ISpreadsheetDocument document)
    {
        var styles = document.Styles;
        return new TreeNode
        {
            Label = $"Styles ({styles.CellXfs.Count} formats)",
            Icon = Icons.Palette,
            NodeType = TreeNodeType.Generic,
            Payload = styles
        };
    }

    // Local icon constants keep the builder free of MudBlazor markup dependencies.
    private static class Icons
    {
        internal const string Workbook = MudBlazor.Icons.Material.Outlined.TableChart;
        internal const string Collections = MudBlazor.Icons.Material.Outlined.Collections;
        internal const string Info = MudBlazor.Icons.Material.Outlined.Info;
        internal const string Sheet = MudBlazor.Icons.Material.Outlined.GridOn;
        internal const string Table = MudBlazor.Icons.Material.Outlined.TableRows;
        internal const string Tag = MudBlazor.Icons.Material.Outlined.Sell;
        internal const string Palette = MudBlazor.Icons.Material.Outlined.Palette;
        internal const string Image = MudBlazor.Icons.Material.Outlined.Image;
        internal const string Chart = MudBlazor.Icons.Material.Outlined.BarChart;
    }
}
