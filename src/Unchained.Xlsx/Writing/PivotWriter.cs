using System.Globalization;
using System.Xml.Linq;
using Unchained.Ooxml.Xml;
using Unchained.Xlsx.Core.Xml;
using Unchained.Xlsx.Models.Cell;
using Unchained.Xlsx.Models.Pivot;
using Unchained.Xlsx.Pivot;

namespace Unchained.Xlsx.Writing;

/// <summary>
///     Serializes a <see cref="PivotTable" /> into its three OPC parts: the pivot table definition
///     (<c>pivotTable*.xml</c>), the cache definition (<c>pivotCacheDefinition*.xml</c>), and the
///     cache records (<c>pivotCacheRecords*.xml</c>). When the pivot was loaded and not refreshed,
///     the preserved raw bytes are written back verbatim.
/// </summary>
internal static class PivotWriter
{
    private static readonly XNamespace X = SmlNames.X;
    private static readonly XNamespace R = SmlNames.R;

    // ── Cache definition ────────────────────────────────────────────────────────

    public static byte[] WriteCacheDefinition(PivotTable pivot, string recordsRelId)
    {
        if (pivot.CacheDefinitionData != null)
            return pivot.CacheDefinitionData;

        var root = new XElement(
            X + "pivotCacheDefinition",
            new XAttribute(XNamespace.Xmlns + "r", R.NamespaceName),
            new XAttribute(R + "id", recordsRelId),
            new XAttribute("recordCount", pivot.CacheRecords.Count.ToString(CultureInfo.InvariantCulture)),
            new XElement(
                X + "cacheSource",
                new XAttribute("type", "worksheet"),
                new XElement(
                    X + "worksheetSource",
                    new XAttribute("ref", pivot.SourceRange.ToA1()),
                    new XAttribute(SmlNames.AttrSheet, pivot.SourceSheetName)
                )
            )
        );

        var cacheFields = new XElement(X + "cacheFields", new XAttribute("count", pivot.Fields.Count.ToString(CultureInfo.InvariantCulture)));
        foreach (var field in pivot.Fields)
        {
            var cacheField = new XElement(X + "cacheField", new XAttribute("name", field.Name), new XAttribute("numFmtId", "0"));

            var sharedItems = new XElement(X + "sharedItems", new XAttribute("count", field.Items.Count.ToString(CultureInfo.InvariantCulture)));
            foreach (var item in field.Items)
                sharedItems.Add(new XElement(X + "s", new XAttribute("v", item)));
            cacheField.Add(sharedItems);
            cacheFields.Add(cacheField);
        }

        root.Add(cacheFields);
        return new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), root).ToUtf8Bytes();
    }

    // ── Cache records ───────────────────────────────────────────────────────────

    public static byte[] WriteCacheRecords(PivotTable pivot)
    {
        if (pivot.CacheRecordsData != null)
            return pivot.CacheRecordsData;

        var root = new XElement(
            X + "pivotCacheRecords",
            new XAttribute(XNamespace.Xmlns + "r", R.NamespaceName),
            new XAttribute(
                "count",
                pivot.CacheRecords.Count.ToString(CultureInfo.InvariantCulture)
            )
        );

        foreach (var record in pivot.CacheRecords)
        {
            var r = new XElement(X + "r");
            foreach (var value in record)
                r.Add(WriteRecordValue(value));
            root.Add(r);
        }

        return new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), root).ToUtf8Bytes();
    }

    private static XElement WriteRecordValue(PivotCacheValue value) => value.Kind switch
    {
        PivotCacheValueKind.Number => new XElement(X + "n", new XAttribute("v", value.Number.ToString("G15", CultureInfo.InvariantCulture))),
        PivotCacheValueKind.Boolean => new XElement(X + "b", new XAttribute("v", value.Boolean ? "1" : "0")),
        PivotCacheValueKind.Error => new XElement(X + "e", new XAttribute("v", value.Error.ToLiteral())),
        PivotCacheValueKind.Text => new XElement(X + "s", new XAttribute("v", value.Text ?? string.Empty)),
        _ => new XElement(X + "m")
    };

    // ── Pivot table definition ──────────────────────────────────────────────────

    public static byte[] WriteTableDefinition(PivotTable pivot)
    {
        if (pivot.TablePartData != null)
            return pivot.TablePartData;

        var rowFields = pivot.RowFields.ToList();
        var colFields = pivot.ColumnFields.ToList();
        var pageFields = pivot.PageFields.ToList();

        // The displayed range spans target + a small header; a spreadsheet app recomputes the real extent.
        var location = new XElement(
            X + "location",
            new XAttribute("ref", pivot.TargetCell.ToA1()),
            new XAttribute("firstHeaderRow", "1"),
            new XAttribute("firstDataRow", "1"),
            new XAttribute("firstDataCol", "1")
        );

        var root = new XElement(
            X + "pivotTableDefinition",
            new XAttribute(XNamespace.Xmlns + "r", R.NamespaceName),
            new XAttribute("name", pivot.Name),
            new XAttribute("cacheId", pivot.CacheId.ToString(CultureInfo.InvariantCulture)),
            new XAttribute("applyNumberFormats", "0"),
            new XAttribute("applyBorderFormats", "0"),
            new XAttribute("applyFontFormats", "0"),
            new XAttribute("applyPatternFormats", "0"),
            new XAttribute("applyAlignmentFormats", "0"),
            new XAttribute("applyWidthHeightFormats", "1"),
            new XAttribute("dataCaption", "Values"),
            new XAttribute("updatedVersion", "6"),
            new XAttribute("minRefreshableVersion", "3"),
            new XAttribute("useAutoFormatting", "1"),
            new XAttribute("itemPrintTitles", "1"),
            new XAttribute("createdVersion", "6"),
            new XAttribute("indent", "0"),
            new XAttribute("outline", "1"),
            new XAttribute("outlineData", "1"),
            new XAttribute("multipleFieldFilters", "0"),
            location
        );

        // pivotFields — one per cache field, marked with its axis.
        var pivotFields = new XElement(X + "pivotFields", new XAttribute("count", pivot.Fields.Count.ToString(CultureInfo.InvariantCulture)));
        foreach (var field in pivot.Fields)
            pivotFields.Add(WritePivotField(field));
        root.Add(pivotFields);

        // rowFields / colFields reference cache-field indices.
        if (rowFields.Count > 0)
            root.Add(WriteFieldIndexList("rowFields", rowFields.Select(static f => f.SourceIndex)));
        if (colFields.Count > 0)
            root.Add(WriteFieldIndexList("colFields", colFields.Select(static f => f.SourceIndex)));
        if (pageFields.Count > 0)
            root.Add(WritePageFields(pageFields));

        // dataFields.
        if (pivot.DataFields.Count > 0)
            root.Add(WriteDataFields(pivot.DataFields));

        return new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), root).ToUtf8Bytes();
    }

    private static XElement WritePivotField(PivotField field)
    {
        var el = new XElement(X + "pivotField", new XAttribute("showAll", "0"));
        var axis = field.Axis switch
        {
            PivotAxis.Row => "axisRow",
            PivotAxis.Column => "axisCol",
            PivotAxis.Page => "axisPage",
            _ => null
        };
        if (axis != null)
            el.SetAttributeValue("axis", axis);
        switch (field.Axis)
        {
            case PivotAxis.Data:
                el.SetAttributeValue("dataField", "1");
            break;
            // Items: one <item> per shared item plus a default. Only meaningful for axis fields.
            case PivotAxis.Row or PivotAxis.Column or PivotAxis.Page when field.Items.Count > 0:
            {
                var items = new XElement(
                    X + "items",
                    new XAttribute("count", (field.Items.Count + 1).ToString(CultureInfo.InvariantCulture))
                );
                for (var i = 0; i < field.Items.Count; i++)
                    items.Add(new XElement(X + "item", new XAttribute("x", i.ToString(CultureInfo.InvariantCulture))));
                items.Add(new XElement(X + "item", new XAttribute("t", "default")));
                el.Add(items);
                break;
            }
        }

        return el;
    }

    private static XElement WriteFieldIndexList(string name, IEnumerable<int> indices)
    {
        var list = indices.ToList();
        var el = new XElement(X + name, new XAttribute("count", list.Count.ToString(CultureInfo.InvariantCulture)));
        foreach (var idx in list)
            el.Add(new XElement(X + "field", new XAttribute("x", idx.ToString(CultureInfo.InvariantCulture))));
        return el;
    }

    private static XElement WritePageFields(IReadOnlyCollection<PivotField> pageFields)
    {
        var el = new XElement(X + "pageFields", new XAttribute("count", pageFields.Count.ToString(CultureInfo.InvariantCulture)));
        foreach (var field in pageFields)
        {
            el.Add(
                new XElement(
                    X + "pageField",
                    new XAttribute("fld", field.SourceIndex.ToString(CultureInfo.InvariantCulture)),
                    new XAttribute("hier", "-1")
                )
            );
        }

        return el;
    }

    private static XElement WriteDataFields(IReadOnlyCollection<PivotDataField> dataFields)
    {
        var el = new XElement(X + "dataFields", new XAttribute("count", dataFields.Count.ToString(CultureInfo.InvariantCulture)));
        foreach (var data in dataFields)
        {
            var dataEl = new XElement(
                X + "dataField",
                new XAttribute("name", data.Name),
                new XAttribute("fld", data.SourceIndex.ToString(CultureInfo.InvariantCulture)),
                new XAttribute("baseField", "0"),
                new XAttribute("baseItem", "0")
            );
            var subtotal = SubtotalAttr(data.Function);
            if (subtotal != null)
                dataEl.SetAttributeValue("subtotal", subtotal);
            el.Add(dataEl);
        }

        return el;
    }

    private static string? SubtotalAttr(PivotDataFunction function) => function switch
    {
        PivotDataFunction.Sum => null, // sum is the default
        PivotDataFunction.Count => "count",
        PivotDataFunction.Average => "average",
        PivotDataFunction.Max => "max",
        PivotDataFunction.Min => "min",
        PivotDataFunction.Product => "product",
        PivotDataFunction.CountNumbers => "countNums",
        PivotDataFunction.StdDev => "stdDev",
        PivotDataFunction.StdDevP => "stdDevp",
        PivotDataFunction.Var => "var",
        PivotDataFunction.VarP => "varp",
        _ => null
    };
}
