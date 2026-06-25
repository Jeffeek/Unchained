using System.Xml.Linq;
using Unchained.Ooxml.Opc;
using Unchained.Ooxml.Xml;
using Unchained.Xlsx.Core.Xml;
using Unchained.Xlsx.Engine;
using Unchained.Xlsx.Models.Cell;
using Unchained.Xlsx.Pivot;

namespace Unchained.Xlsx.Parsing;

/// <summary>
///     Parses a pivot table definition part (and its linked cache definition/records) enough to
///     expose identity, source range, and field list, and preserves the raw part bytes so loaded
///     pivot tables round-trip verbatim unless <see cref="PivotTable.Refresh" /> regenerates them.
/// </summary>
internal static class PivotParser
{
    public static PivotTable? Parse(SpreadsheetDocument document, OpcPart tablePart, string tableUri)
    {
        var root = OoXmlHelper.ParseXml(tablePart.Data).Root;
        if (root == null)
            return null;

        var name = root.GetAttr("name") ?? "PivotTable";
        var cacheId = root.GetAttrInt("cacheId", 1);

        var location = root.Child(SmlNames.X + "location")?.GetAttr("ref");
        var target = new CellReference(1, 1);
        if (location != null && CellReference.TryFromA1(location.Split(':')[0], out var loc))
            target = loc;

        // Resolve the cache definition via this part's relationship.
        var cacheDefRel = tablePart.Relationships
            .FirstOrDefault(static r => r.RelationshipType.Equals(SmlNames.RelTypePivotCacheDefinition, StringComparison.Ordinal));

        var sourceRange = new CellRange(new CellReference(1, 1), new CellReference(1, 1));
        var sourceSheet = string.Empty;
        OpcPart? cacheDefPart = null;

        if (cacheDefRel != null)
        {
            var cacheDefUri = tablePart.ResolveUri(cacheDefRel.TargetUri);
            cacheDefPart = document.Package?.TryGetPart(cacheDefUri);
            if (cacheDefPart != null)
                (sourceRange, sourceSheet) = ReadCacheSource(cacheDefPart);
        }

        var pivot = new PivotTable(name, cacheId, sourceRange, target, sourceSheet)
        {
            TablePartUri = tableUri,
            TablePartData = tablePart.Data,
            CacheDefinitionData = cacheDefPart?.Data,
            CacheDefinitionUri = cacheDefPart?.Uri ?? string.Empty
        };

        // Field names from the cache definition.
        // ReSharper disable once InvertIf
        if (cacheDefPart != null)
        {
            var cacheRoot = OoXmlHelper.ParseXml(cacheDefPart.Data).Root;
            ReadFields(pivot, cacheRoot);

            var recRel = cacheDefPart.Relationships.FirstOrDefault(static r => r.RelationshipType.EndsWith("pivotCacheRecords", StringComparison.Ordinal));
            if (recRel == null)
                return pivot;

            var recUri = cacheDefPart.ResolveUri(recRel.TargetUri);
            var cacheRecPart = document.Package?.TryGetPart(recUri);
            pivot.CacheRecordsUri = recUri;
            pivot.CacheRecordsData = cacheRecPart?.Data;
        }

        return pivot;
    }

    private static (CellRange range, string sheet) ReadCacheSource(OpcPart cacheDefPart)
    {
        var root = OoXmlHelper.ParseXml(cacheDefPart.Data).Root;
        var worksheetSource = root?.Child(SmlNames.X + "cacheSource")?.Child(SmlNames.X + "worksheetSource");
        var sheet = worksheetSource?.GetAttr("sheet") ?? string.Empty;
        var refAttr = worksheetSource?.GetAttr("ref");
        var range = refAttr != null
            ? CellRange.FromA1(refAttr)
            : new CellRange(new CellReference(1, 1), new CellReference(1, 1));
        return (range, sheet);
    }

    private static void ReadFields(PivotTable pivot, XElement? cacheRoot)
    {
        var fields = cacheRoot?.Child(SmlNames.X + "cacheFields");
        if (fields == null)
            return;

        var index = 0;

        foreach (var name in fields.Children(SmlNames.X + "cacheField").Select(field => field.GetAttr("name") ?? $"Field{index + 1}"))
            pivot.AddFieldRaw(new PivotField(name, index++));
    }
}
