using System.Globalization;
using System.Xml.Linq;
using Unchained.Ooxml.Xml;
using Unchained.Xlsx.Core.Xml;
using Unchained.Xlsx.Models.Cell;
using Unchained.Xlsx.Models.Tables;
using Unchained.Xlsx.Tables;

namespace Unchained.Xlsx.Writing;

/// <summary>Serializes a <see cref="ListObject" /> to a <c>xl/tables/table*.xml</c> part.</summary>
internal static class TableWriter
{
    public static byte[] Write(ListObject table)
    {
        var root = new XElement(
            SmlNames.X + "table",
            new XAttribute("xmlns", SmlNames.X.NamespaceName),
            new XAttribute("id", table.Id.ToString(CultureInfo.InvariantCulture)),
            new XAttribute("name", table.Name),
            new XAttribute("displayName", table.DisplayName),
            new XAttribute("ref", table.Range.ToA1())
        );

        if (!table.ShowHeaderRow)
            root.SetAttributeValue("headerRowCount", "0");
        if (table.ShowTotalsRow)
            root.SetAttributeValue("totalsRowCount", "1");

        // <autoFilter> covering the header row is conventional for worksheet tables.
        if (table.ShowHeaderRow)
        {
            var filterRange = CellRange.FromCorners(
                table.Range.TopLeft,
                new CellReference(table.Range.TopLeft.Row, table.Range.BottomRight.Column)
            );
            root.Add(new XElement(SmlNames.AutoFilter, new XAttribute("ref", filterRange.ToA1())));
        }

        var columns = new XElement(
            SmlNames.X + "tableColumns",
            new XAttribute("count", table.Columns.Count.ToString(CultureInfo.InvariantCulture))
        );
        foreach (var column in table.Columns)
            columns.Add(WriteColumn(column));
        root.Add(columns);

        root.Add(
            new XElement(
                SmlNames.X + "tableStyleInfo",
                new XAttribute("name", table.StyleName),
                new XAttribute("showFirstColumn", table.ShowFirstColumn ? "1" : "0"),
                new XAttribute("showLastColumn", table.ShowLastColumn ? "1" : "0"),
                new XAttribute("showRowStripes", table.ShowBandedRows ? "1" : "0"),
                new XAttribute("showColumnStripes", table.ShowBandedColumns ? "1" : "0")
            )
        );

        return new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), root).ToUtf8Bytes();
    }

    private static XElement WriteColumn(ListColumn column)
    {
        var element = new XElement(
            SmlNames.X + "tableColumn",
            new XAttribute("id", column.Id.ToString(CultureInfo.InvariantCulture)),
            new XAttribute("name", column.Name)
        );

        if (column.TotalsFunction != TotalsRowFunction.None)
            element.SetAttributeValue("totalsRowFunction", ToLiteral(column.TotalsFunction));
        if (!string.IsNullOrEmpty(column.TotalsLabel))
            element.SetAttributeValue("totalsRowLabel", column.TotalsLabel);
        if (!string.IsNullOrEmpty(column.TotalsFormula))
            element.Add(new XElement(SmlNames.X + "totalsRowFormula", column.TotalsFormula));
        if (!string.IsNullOrEmpty(column.ColumnFormula))
            element.Add(new XElement(SmlNames.X + "calculatedColumnFormula", column.ColumnFormula));

        return element;
    }

    private static string ToLiteral(TotalsRowFunction function) => function switch
    {
        TotalsRowFunction.Sum => "sum",
        TotalsRowFunction.Count => "count",
        TotalsRowFunction.Average => "average",
        TotalsRowFunction.Max => "max",
        TotalsRowFunction.Min => "min",
        TotalsRowFunction.StdDev => "stdDev",
        TotalsRowFunction.Var => "var",
        TotalsRowFunction.CountNumbers => "countNums",
        TotalsRowFunction.Custom => "custom",
        _ => "none"
    };
}
