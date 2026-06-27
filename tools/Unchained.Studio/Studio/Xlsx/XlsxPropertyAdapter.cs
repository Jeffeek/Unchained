using Unchained.Ooxml.Properties;
using Unchained.Studio.Models;
using Unchained.Xlsx.Cell;
using Unchained.Xlsx.DefinedNames;
using Unchained.Xlsx.Models;
using Unchained.Xlsx.Styles;
using Unchained.Xlsx.Worksheets;

namespace Unchained.Studio.Studio.Xlsx;

/// <summary>
///     Converts a selected <see cref="TreeNode" /> from an XLSX document tree into a
///     <see cref="PropertyBag" /> for display in the properties panel.
/// </summary>
public static class XlsxPropertyAdapter
{
    public static PropertyBag Build(TreeNode node) =>
        node.NodeType switch
        {
            TreeNodeType.Metadata when node.Payload is WorkbookProperties props => ForProperties(props),
            TreeNodeType.Sheet when node.Payload is Worksheet sheet => ForSheet(sheet),
            TreeNodeType.NamedDestination when node.Payload is DefinedName name => ForDefinedName(name),
            TreeNodeType.Generic when node.Payload is StyleBook styles => ForStyles(styles),
            _ => PropertyBag.Empty(node.Label)
        };

    private static PropertyBag ForProperties(OoXmlCoreProperties props) => new()
    {
        Title = "Workbook Properties",
        Groups =
        [
            new PropertyGroup
            {
                Header = "Core",
                Entries =
                [
                    Entry("Title", props.Title ?? "(absent)"),
                    Entry("Author", props.Author ?? "(absent)"),
                    Entry("Subject", props.Subject ?? "(absent)"),
                    Entry("Keywords", props.Keywords ?? "(absent)"),
                    Entry("Description", props.Description ?? "(absent)"),
                    Entry("Category", props.Category ?? "(absent)"),
                    Entry("Last modified by", props.LastModifiedBy ?? "(absent)")
                ]
            },
            new PropertyGroup
            {
                Header = "Dates",
                Entries =
                [
                    Entry("Created", props.Created?.ToString("yyyy-MM-dd HH:mm:ss zzz") ?? "(absent)", PropertyValueKind.Date),
                    Entry("Modified", props.Modified?.ToString("yyyy-MM-dd HH:mm:ss zzz") ?? "(absent)", PropertyValueKind.Date)
                ]
            },
            new PropertyGroup
            {
                Header = "Application",
                Entries =
                [
                    Entry("Application", props.ApplicationName ?? "(absent)"),
                    Entry("Company", props.Company ?? "(absent)"),
                    Entry("Manager", props.Manager ?? "(absent)")
                ]
            }
        ]
    };

    private static PropertyBag ForSheet(Worksheet sheet)
    {
        var used = sheet.GetUsedRange();
        return new PropertyBag
        {
            Title = sheet.Name,
            Subtitle = "Worksheet",
            Groups =
            [
                new PropertyGroup
                {
                    Header = "Identity",
                    Entries =
                    [
                        Entry("Name", sheet.Name),
                        Entry("Sheet ID", sheet.SheetId.ToString(), PropertyValueKind.Number),
                        Entry("Tab index", sheet.TabIndex.ToString(), PropertyValueKind.Number),
                        Entry("State", sheet.State.ToString()),
                        Entry("Tab colour", sheet.TabColor is null ? "(none)" : sheet.TabColor.ToString()!, PropertyValueKind.Hex)
                    ]
                },
                new PropertyGroup
                {
                    Header = "Content",
                    Entries =
                    [
                        Entry("Used range", used?.ToA1() ?? "(empty)"),
                        Entry("Rows with data", sheet.Rows.Count.ToString(), PropertyValueKind.Number),
                        Entry("Column definitions", sheet.Columns.Count.ToString(), PropertyValueKind.Number),
                        Entry("Merged ranges", sheet.MergedCells.Count.ToString(), PropertyValueKind.Number),
                        Entry("Tables", sheet.Tables.Count.ToString(), PropertyValueKind.Number),
                        Entry("Data validations", sheet.DataValidations.Count.ToString(), PropertyValueKind.Number),
                        Entry("Auto-filter", sheet.AutoFilter?.ToA1() ?? "(none)")
                    ]
                }
            ]
        };
    }

    private static PropertyBag ForDefinedName(DefinedName name) => new()
    {
        Title = name.Name,
        Subtitle = "Defined Name",
        Groups =
        [
            new PropertyGroup
            {
                Entries =
                [
                    Entry("Name", name.Name),
                    Entry("Refers to", name.Formula),
                    Entry("Scope", name.IsWorkbookScoped ? "Workbook" : $"Sheet #{name.LocalSheetId}"),
                    Entry("Comment", name.Comment ?? "(none)"),
                    Entry("Hidden", name.IsHidden.ToString(), PropertyValueKind.Boolean),
                    Entry("Built-in", name.IsBuiltIn.ToString(), PropertyValueKind.Boolean)
                ]
            }
        ]
    };

    private static PropertyBag ForStyles(StyleBook styles) => new()
    {
        Title = "Styles",
        Subtitle = "Workbook style tables",
        Groups =
        [
            new PropertyGroup
            {
                Entries =
                [
                    Entry("Fonts", styles.Fonts.Count.ToString(), PropertyValueKind.Number),
                    Entry("Fills", styles.Fills.Count.ToString(), PropertyValueKind.Number),
                    Entry("Borders", styles.Borders.Count.ToString(), PropertyValueKind.Number),
                    Entry("Number formats", styles.NumberFormats.Count.ToString(), PropertyValueKind.Number),
                    Entry("Cell formats (cellXfs)", styles.CellXfs.Count.ToString(), PropertyValueKind.Number),
                    Entry("Named styles", styles.NamedStyles.Count.ToString(), PropertyValueKind.Number)
                ]
            }
        ]
    };

    /// <summary>Builds a property bag for a single selected cell.</summary>
    public static PropertyBag ForCell(Cell cell) => new()
    {
        Title = cell.Reference.ToA1(),
        Subtitle = "Cell",
        Groups =
        [
            new PropertyGroup
            {
                Entries =
                [
                    Entry("Reference", cell.Reference.ToA1()),
                    Entry("Type", cell.CellType.ToString()),
                    Entry("Value", cell.GetFormattedString()),
                    Entry("Formula", cell.FormulaText ?? "(none)"),
                    Entry("Number format", cell.NumberFormatCode),
                    Entry("Style index", cell.StyleIndex.ToString(), PropertyValueKind.Number),
                    Entry("Merged", cell.IsMerged.ToString(), PropertyValueKind.Boolean)
                ]
            }
        ]
    };

    private static PropertyEntry Entry(
        string key,
        string value,
        PropertyValueKind kind = PropertyValueKind.Text
    ) =>
        new()
        {
            Key = key,
            DisplayValue = value,
            Kind = kind,
            CopyValue = value
        };
}
