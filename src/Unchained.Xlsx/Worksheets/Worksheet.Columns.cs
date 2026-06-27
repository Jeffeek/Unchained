using Unchained.Ooxml.Xml;
using Unchained.Xlsx.Cell;
using Unchained.Xlsx.Core.Xml;

namespace Unchained.Xlsx.Worksheets;

public sealed partial class Worksheet
{
    /// <summary>The column definitions of this sheet.</summary>
    public ColumnCollection Columns
    {
        get
        {
            EnsureColumnsParsed();
            return ColumnsInternal;
        }
    }

    /// <summary>Returns the column definition covering <paramref name="columnNumber" />, or <see langword="null" />.</summary>
    public Column? GetColumn(int columnNumber) => Columns.GetColumn(columnNumber);

    /// <summary>Sets a column's width in character units, materialising the definition if necessary.</summary>
    public void SetColumnWidth(int columnNumber, double widthChars)
    {
        var column = Columns.GetOrCreateColumn(columnNumber);
        column.Width = widthChars;
        column.IsCustomWidth = true;
    }

    /// <summary>Hides the given column.</summary>
    public void HideColumn(int columnNumber) => Columns.GetOrCreateColumn(columnNumber).IsHidden = true;

    /// <summary>Shows the given column.</summary>
    public void ShowColumn(int columnNumber)
    {
        var column = Columns.GetColumn(columnNumber);
        column?.IsHidden = false;
    }

    private void EnsureColumnsParsed()
    {
        if (ColumnsMaterialised)
            return;

        ColumnsMaterialised = true;
        var colsElement = RawElement?.Child(SmlNames.Cols);
        if (colsElement == null)
            return;

        foreach (var column in from colElement in colsElement.Children(SmlNames.Col)
                               let min = colElement.GetAttrInt("min", 1)
                               let max = colElement.GetAttrInt("max", min)
                               select new Column(min, max)
                               {
                                   Width = colElement.GetAttrDouble("width"),
                                   IsCustomWidth = colElement.GetAttrBool("customWidth") == true,
                                   IsHidden = colElement.GetAttrBool("hidden") == true,
                                   IsCollapsed = colElement.GetAttrBool("collapsed") == true,
                                   OutlineLevel = colElement.GetAttrInt("outlineLevel", 0),
                                   StyleIndex = colElement.GetAttrInt("style")
                               })
            ColumnsInternal.AddExisting(column);
    }
}
