using Unchained.Ooxml.Xml;
using Unchained.Xlsx.Core.Xml;
using Unchained.Xlsx.Models.Cell;
using Unchained.Xlsx.Parsing;
using Unchained.Xlsx.Tables;

namespace Unchained.Xlsx.Worksheets;

public sealed partial class Worksheet
{
    /// <summary>The structured tables (ListObjects) defined on this worksheet.</summary>
    public ListObjectCollection Tables
    {
        get
        {
            if (TablesOrNull != null)
                return TablesOrNull;

            TablesOrNull = new ListObjectCollection(this);
            ParseTables(TablesOrNull);
            return TablesOrNull;
        }
    }

    internal bool TablesMaterialised => TablesOrNull != null;

    internal ListObjectCollection? TablesOrNull { get; private set; }

    /// <summary>Adds a table over <paramref name="range" />.</summary>
    public ListObject AddTable(CellRange range, bool hasHeaders = true) => Tables.Add(range, null, hasHeaders);

    /// <summary>Removes a table from this worksheet.</summary>
    public void RemoveTable(ListObject table) => Tables.Remove(table);

    private void ParseTables(ListObjectCollection tables)
    {
        if (RawElement == null || Document.Package == null)
            return;

        var part = Document.Package.TryGetPart(PartUri);
        if (part == null)
            return;

        foreach (var tablePart in RawElement.Child(SmlNames.TableParts)?.Children(SmlNames.TablePart) ?? [])
        {
            var relId = (string?)tablePart.Attribute(SmlNames.R + "id");
            if (relId == null)
                continue;

            var rel = part.Relationships.FirstOrDefault(r => r.Id == relId);
            if (rel == null)
                continue;

            var tableUri = part.ResolveUri(rel.TargetUri);
            var tableXml = Document.Package.TryGetPart(tableUri);
            if (tableXml == null)
                continue;

            var table = TableParser.Parse(OoXmlHelper.ParseXml(tableXml.Data).Root!);
            table.PartUri = tableUri;
            table.RelationshipId = relId;
            Document.ObserveTableId(table.Id);
            tables.AddExisting(table);
        }
    }
}
