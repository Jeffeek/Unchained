using System.Xml.Linq;
using Unchained.Ooxml.Xml;
using Unchained.Xlsx.Core.Xml;
using Unchained.Xlsx.Models.Cell;
using Unchained.Xlsx.Models.Tables;
using Unchained.Xlsx.Tables;

namespace Unchained.Xlsx.Parsing;

/// <summary>Parses a <c>xl/tables/table*.xml</c> part into a <see cref="ListObject" />.</summary>
internal static class TableParser
{
    private static readonly XName Table = SmlNames.X + "table";
    private static readonly XName TableColumns = SmlNames.X + "tableColumns";
    private static readonly XName TableColumn = SmlNames.X + "tableColumn";
    private static readonly XName TableStyleInfo = SmlNames.X + "tableStyleInfo";

    public static ListObject Parse(XElement root)
    {
        var id = root.GetAttrInt("id", 0);
        var name = root.GetAttr("name") ?? root.GetAttr("displayName") ?? $"Table{id}";
        var range = CellRange.FromA1(root.GetAttr("ref") ?? "A1");

        var table = new ListObject(id, name, range)
        {
            DisplayName = root.GetAttr("displayName") ?? name,
            ShowHeaderRow = root.GetAttrInt("headerRowCount", 1) > 0,
            ShowTotalsRow = root.GetAttrInt("totalsRowCount", 0) > 0
        };

        foreach (var columnElement in root.Child(TableColumns)?.Children(TableColumn) ?? [])
        {
            var column = new ListColumn(
                columnElement.GetAttrInt("id", 0),
                columnElement.GetAttr("name") ?? string.Empty)
            {
                TotalsLabel = columnElement.GetAttr("totalsRowLabel"),
                TotalsFunction = ParseTotalsFunction(columnElement.GetAttr("totalsRowFunction")),
                TotalsFormula = columnElement.Child(SmlNames.X + "totalsRowFormula")?.Value,
                ColumnFormula = columnElement.Child(SmlNames.X + "calculatedColumnFormula")?.Value
            };
            table.AddColumnRaw(column);
        }

        var styleInfo = root.Child(TableStyleInfo);
        if (styleInfo != null)
        {
            table.StyleName = styleInfo.GetAttr("name") ?? table.StyleName;
            table.ShowFirstColumn = styleInfo.GetAttrBool("showFirstColumn") == true;
            table.ShowLastColumn = styleInfo.GetAttrBool("showLastColumn") == true;
            table.ShowBandedRows = styleInfo.GetAttrBool("showRowStripes") != false;
            table.ShowBandedColumns = styleInfo.GetAttrBool("showColumnStripes") == true;
        }

        return table;
    }

    private static TotalsRowFunction ParseTotalsFunction(string? literal) => literal switch
    {
        "sum" => TotalsRowFunction.Sum,
        "count" => TotalsRowFunction.Count,
        "average" => TotalsRowFunction.Average,
        "max" => TotalsRowFunction.Max,
        "min" => TotalsRowFunction.Min,
        "stdDev" => TotalsRowFunction.StdDev,
        "var" => TotalsRowFunction.Var,
        "countNums" => TotalsRowFunction.CountNumbers,
        "custom" => TotalsRowFunction.Custom,
        _ => TotalsRowFunction.None
    };
}
