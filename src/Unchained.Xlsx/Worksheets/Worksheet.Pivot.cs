using Unchained.Xlsx.Core.Xml;
using Unchained.Xlsx.Models.Cell;
using Unchained.Xlsx.Pivot;

namespace Unchained.Xlsx.Worksheets;

public sealed partial class Worksheet
{
    /// <summary>The pivot tables on this worksheet.</summary>
    public PivotTableCollection PivotTables
    {
        get
        {
            if (PivotTablesOrNull != null)
                return PivotTablesOrNull;

            PivotTablesOrNull = new PivotTableCollection(this);
            ParsePivotTables(PivotTablesOrNull);
            return PivotTablesOrNull;
        }
    }

    /// <summary>
    ///     Creates a pivot table from <paramref name="sourceRange" /> (on this sheet) placed at
    ///     <paramref name="targetCell" />.
    /// </summary>
    public PivotTable AddPivotTable(CellRange sourceRange, CellReference targetCell, string name) =>
        PivotTables.Add(sourceRange, targetCell, name);

    internal bool PivotTablesMaterialised => PivotTablesOrNull != null;

    internal PivotTableCollection? PivotTablesOrNull { get; private set; }

    private void ParsePivotTables(PivotTableCollection pivots)
    {
        if (RawElement == null || Document.Package == null)
            return;

        var part = Document.Package.TryGetPart(PartUri);
        if (part == null)
            return;

        // Worksheet → pivotTable relationships (the worksheet has no <pivotTable> element; the link is
        // purely relationship-based for the table definition part).
        var pivotRels = part.Relationships
            .Where(static r => r.RelationshipType.Equals(SmlNames.RelTypePivotTable, StringComparison.Ordinal))
            .ToList();

        foreach (var rel in pivotRels)
        {
            var tableUri = part.ResolveUri(rel.TargetUri);
            var tablePart = Document.Package.TryGetPart(tableUri);
            if (tablePart == null)
                continue;

            var pivot = Parsing.PivotParser.Parse(Document, tablePart, tableUri);
            if (pivot == null)
                continue;

            pivot.TableRelationshipId = rel.Id;
            Document.ObservePivotCacheId(pivot.CacheId);
            pivots.AddExisting(pivot);
        }
    }
}
