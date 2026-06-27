using System.Globalization;
using System.Xml.Linq;
using Unchained.Ooxml.Opc;
using Unchained.Xlsx.Core.Xml;
using Unchained.Xlsx.Engine;
using Unchained.Xlsx.Pivot;

namespace Unchained.Xlsx.Writing;

internal static partial class WorkbookWriter
{
    // Pivot tables span three parts each (table definition, cache definition, cache records) plus a
    // workbook-level <pivotCaches> registration. Identities are assigned, then parts + relationships
    // are written; the workbook part registration is handled in WriteWorkbookPart via PivotCaches().

    private static void WritePivotParts(OpcPackage package, SpreadsheetDocument document)
    {
        var pivots = CollectPivots(document);
        if (pivots.Count == 0)
            return;

        var nextTable = 1;
        var nextCache = 1;

        foreach (var (sheet, pivot) in pivots)
        {
            if (string.IsNullOrEmpty(pivot.TablePartUri))
                pivot.TablePartUri = $"/xl/pivotTables/pivotTable{nextTable++}.xml";
            if (string.IsNullOrEmpty(pivot.CacheDefinitionUri))
                pivot.CacheDefinitionUri = $"/xl/pivotCache/pivotCacheDefinition{nextCache}.xml";
            if (string.IsNullOrEmpty(pivot.CacheRecordsUri))
                pivot.CacheRecordsUri = $"/xl/pivotCache/pivotCacheRecords{nextCache}.xml";
            nextCache++;

            // 1. Cache records part.
            package.AddOrReplacePart(pivot.CacheRecordsUri, SmlNames.ContentTypePivotCacheRecords, PivotWriter.WriteCacheRecords(pivot));

            // 2. Cache definition part + def → records relationship.
            var recordsRelId = "rId1";
            package.AddOrReplacePart(pivot.CacheDefinitionUri, SmlNames.ContentTypePivotCacheDefinition,
                PivotWriter.WriteCacheDefinition(pivot, recordsRelId));
            package.ClearRelationships(pivot.CacheDefinitionUri);
            package.AddRelationship(pivot.CacheDefinitionUri, recordsRelId, SmlNames.RelTypePivotCacheRecords,
                package.GetRelativeUri(pivot.CacheDefinitionUri, pivot.CacheRecordsUri));

            // 3. Table definition part + table → cacheDefinition relationship.
            package.AddOrReplacePart(pivot.TablePartUri, SmlNames.ContentTypePivotTable, PivotWriter.WriteTableDefinition(pivot));
            package.ClearRelationships(pivot.TablePartUri);
            pivot.CacheDefinitionRelId = "rId1";
            package.AddRelationship(pivot.TablePartUri, pivot.CacheDefinitionRelId, SmlNames.RelTypePivotCacheDefinition,
                package.GetRelativeUri(pivot.TablePartUri, pivot.CacheDefinitionUri));

            // 4. Worksheet → pivot table relationship.
            if (string.IsNullOrEmpty(pivot.TableRelationshipId))
                pivot.TableRelationshipId = package.NextFreeRelId(sheet.PartUri, "rIdPv");
            EnsureRelationship(package, sheet.PartUri, pivot.TableRelationshipId, SmlNames.RelTypePivotTable,
                package.GetRelativeUri(sheet.PartUri, pivot.TablePartUri));
        }
    }

    /// <summary>Builds the workbook-level <c>&lt;pivotCaches&gt;</c> element + the workbook → cacheDefinition relationships.</summary>
    private static XElement? PivotCaches(OpcPackage package, SpreadsheetDocument document)
    {
        var pivots = CollectPivots(document);
        if (pivots.Count == 0)
            return null;

        var caches = new XElement(SmlNames.X + "pivotCaches");
        foreach (var (_, pivot) in pivots)
        {
            var relId = NextFreeRelId(package.GetPart(WorkbookUri));
            package.AddRelationship(WorkbookUri, relId, SmlNames.RelTypePivotCacheDefinition,
                RelativeToWorkbook(package, pivot.CacheDefinitionUri));
            caches.Add(new XElement(SmlNames.X + "pivotCache",
                new XAttribute("cacheId", pivot.CacheId.ToString(CultureInfo.InvariantCulture)),
                new XAttribute(SmlNames.R + "id", relId)));
        }

        return caches;
    }

    private static List<(Worksheets.Worksheet Sheet, PivotTable Pivot)> CollectPivots(SpreadsheetDocument document)
    {
        var result = new List<(Worksheets.Worksheet, PivotTable)>();
        foreach (var sheet in document.Sheets)
        {
            if (!sheet.PivotTablesMaterialised)
                continue;

            foreach (var pivot in sheet.PivotTablesOrNull!.All)
                result.Add((sheet, pivot));
        }

        return result;
    }
}
