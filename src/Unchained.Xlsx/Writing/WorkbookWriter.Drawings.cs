using Unchained.Ooxml.Media;
using Unchained.Ooxml.Opc;
using Unchained.Xlsx.Core.Xml;
using Unchained.Xlsx.Drawings;
using Unchained.Xlsx.Engine;

namespace Unchained.Xlsx.Writing;

internal static partial class WorkbookWriter
{
    // ── Drawing identity assignment ─────────────────────────────────────────────
    // Runs before worksheet XML is written so each sheet's <drawing r:id> can reference its part.

    private static void AssignDrawingIdentities(OpcPackage package, SpreadsheetDocument document)
    {
        var nextDrawing = 1;
        var nextChart = 1;
        var nextImage = 1;
        var usedUris = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var sheet in document.Sheets)
        {
            if (!sheet.DrawingsMaterialised || sheet.DrawingsOrNull!.Count == 0)
                continue;

            if (string.IsNullOrEmpty(sheet.DrawingPartUri))
            {
                string uri;
                do
                    uri = $"/xl/drawings/drawing{nextDrawing++}.xml";
                while (!usedUris.Add(uri) || package.TryGetPart(uri) != null);
                sheet.DrawingPartUri = uri;
            }

            if (string.IsNullOrEmpty(sheet.DrawingRelationshipId))
                sheet.DrawingRelationshipId = NextFreeRelIdFor(package, sheet.PartUri, "rIdDr");

            // Assign each drawing a shape id + a rel id within the drawing part, plus backing-part URIs.
            var shapeId = 1;
            var drawingRelN = 1;
            foreach (var drawing in sheet.DrawingsOrNull!.All)
            {
                drawing.ShapeId = shapeId++;
                if (string.IsNullOrEmpty(drawing.RelationshipId))
                    drawing.RelationshipId = $"rId{drawingRelN++}";
                else
                    drawingRelN++;

                switch (drawing)
                {
                    case PictureDrawing pic when string.IsNullOrEmpty(pic.MediaPartUri):
                    {
                        var ext = ImageExtensions.Extension(pic.Image.ContentType);
                        string uri;
                        do
                            uri = $"/xl/media/image{nextImage++}{ext}";
                        while (!usedUris.Add(uri) || package.TryGetPart(uri) != null);
                        pic.MediaPartUri = uri;
                        break;
                    }
                    case ChartDrawing chart when string.IsNullOrEmpty(chart.ChartPartUri):
                    {
                        string uri;
                        do
                            uri = $"/xl/charts/chart{nextChart++}.xml";
                        while (!usedUris.Add(uri) || package.TryGetPart(uri) != null);
                        chart.ChartPartUri = uri;
                        break;
                    }
                }
            }
        }
    }

    private static void WriteDrawingParts(OpcPackage package, SpreadsheetDocument document)
    {
        foreach (var sheet in document.Sheets)
        {
            if (!sheet.DrawingsMaterialised || sheet.DrawingsOrNull!.Count == 0)
                continue;

            var drawings = sheet.DrawingsOrNull!.All;

            // 1. The drawing part itself.
            package.AddOrReplacePart(sheet.DrawingPartUri, SmlNames.ContentTypeDrawing, DrawingWriter.Write(drawings));

            // 2. Sheet → drawing relationship (idempotent).
            EnsureRelationship(package, sheet.PartUri, sheet.DrawingRelationshipId, SmlNames.RelTypeDrawing,
                RelativeTo(package, sheet.PartUri, sheet.DrawingPartUri));

            // 3. Each drawing's backing part + drawing → part relationship.
            package.ClearRelationships(sheet.DrawingPartUri);
            foreach (var drawing in drawings)
            {
                switch (drawing)
                {
                    case PictureDrawing pic:
                        package.AddOrReplacePart(pic.MediaPartUri, pic.Image.ContentType, pic.Image.Data.ToArray());
                        package.AddRelationship(sheet.DrawingPartUri, drawing.RelationshipId, SmlNames.RelTypeImage,
                            RelativeTo(package, sheet.DrawingPartUri, pic.MediaPartUri));
                        break;
                    case ChartDrawing chart:
                        var bytes = chart.ChartPartData ?? Parsing.ChartXml.Write(chart.Chart);
                        package.AddOrReplacePart(chart.ChartPartUri, SmlNames.ContentTypeChart, bytes);
                        package.AddRelationship(sheet.DrawingPartUri, drawing.RelationshipId, SmlNames.RelTypeChart,
                            RelativeTo(package, sheet.DrawingPartUri, chart.ChartPartUri));
                        break;
                }
            }
        }
    }

    private static void EnsureRelationship(OpcPackage package, string sourceUri, string relId, string relType, string target)
    {
        var part = package.GetPart(sourceUri);
        if (part.Relationships.Any(r => r.Id == relId))
            return;

        package.AddRelationship(sourceUri, relId, relType, target);
    }

    private static string NextFreeRelIdFor(OpcPackage package, string partUri, string prefix)
    {
        var part = package.TryGetPart(partUri);
        var used = new HashSet<string>(part?.Relationships.Select(r => r.Id) ?? [], StringComparer.Ordinal);
        var n = 1;
        string relId;
        do
            relId = $"{prefix}{n++}";
        while (!used.Add(relId));
        return relId;
    }

    private static string RelativeTo(OpcPackage package, string fromPartUri, string targetUri) =>
        package.GetRelativeUri(fromPartUri, targetUri);
}
