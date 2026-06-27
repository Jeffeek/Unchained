using Unchained.Ooxml.Xml;
using Unchained.Xlsx.Core.Xml;
using Unchained.Xlsx.Models.Cell;
using Unchained.Xlsx.Tables;

namespace Unchained.Xlsx.Worksheets;

public sealed partial class Worksheet
{
    private ListObjectCollection? _tables;

    /// <summary>The structured tables (ListObjects) defined on this worksheet.</summary>
    public ListObjectCollection Tables
    {
        get
        {
            if (_tables != null)
                return _tables;

            _tables = new ListObjectCollection(this);
            ParseTables(_tables);
            return _tables;
        }
    }

    /// <summary>Adds a table over <paramref name="range" />.</summary>
    public ListObject AddTable(CellRange range, bool hasHeaders = true) => Tables.Add(range, name: null, hasHeaders);

    /// <summary>Removes a table from this worksheet.</summary>
    public void RemoveTable(ListObject table) => Tables.Remove(table);

    internal bool TablesMaterialised => _tables != null;

    internal ListObjectCollection? TablesOrNull => _tables;

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

            var table = Parsing.TableParser.Parse(OoXmlHelper.ParseXml(tableXml.Data).Root!);
            table.PartUri = tableUri;
            table.RelationshipId = relId;
            Document.ObserveTableId(table.Id);
            tables.AddExisting(table);
        }
    }
}
