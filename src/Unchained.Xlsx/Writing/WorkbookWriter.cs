using System.Globalization;
using System.Xml.Linq;
using Unchained.Ooxml.Opc;
using Unchained.Ooxml.Properties;
using Unchained.Ooxml.Xml;
using Unchained.Xlsx.Abstractions;
using Unchained.Xlsx.Core.Xml;
using Unchained.Xlsx.Engine;
using Unchained.Xlsx.Models;
using Unchained.Xlsx.Models.Sheets;
using Unchained.Xlsx.Styles;

namespace Unchained.Xlsx.Writing;

/// <summary>
///     Serializes a <see cref="SpreadsheetDocument" /> back to an OPC package (<c>.xlsx</c>) byte
///     array. When the document was loaded from a package, all parts Unchained does not model are
///     preserved verbatim; only <c>workbook.xml</c>, its relationships, the worksheet parts, and the
///     document properties are regenerated from the model. A freshly created document is written as a
///     minimal but valid package from scratch.
/// </summary>
internal static partial class WorkbookWriter
{
    private const string WorkbookUri = "/xl/workbook.xml";
    private const string StylesUri = "/xl/styles.xml";

    // ── sharedStrings.xml ──────────────────────────────────────────────────────

    private const string SharedStringsUri = "/xl/sharedStrings.xml";

    public static byte[] Write(SpreadsheetDocument document, XlsxSaveOptions options)
    {
        var package = document.Package ?? CreateMinimalPackage();

        AssignSheetIdentities(package, document);
        AssignTableIdentities(package, document);
        AssignDrawingIdentities(package, document);
        WriteWorksheetParts(package, document);
        WriteTableParts(package, document);
        WriteDrawingParts(package, document);
        WritePivotParts(package, document);
        RemoveOrphanedWorksheetParts(package, document);
        WriteSharedStrings(package, document);
        WriteWorkbookPart(package, document, options);
        WriteWorkbookRelationships(package, document);
        WriteProperties(package, document);
        WriteStylesPart(package, document);

        return package.Save();
    }

    // ── Sheet identity assignment ───────────────────────────────────────────────

    private static void AssignSheetIdentities(OpcPackage package, ISpreadsheetDocument document)
    {
        var usedUris = new HashSet<string>(
            document.Sheets.Where(static s => !string.IsNullOrEmpty(s.PartUri)).Select(static s => s.PartUri),
            StringComparer.OrdinalIgnoreCase
        );

        var nextSheet = 1;
        var nextRel = 1;
        var usedRelIds = new HashSet<string>(
            document.Sheets.Where(static s => !string.IsNullOrEmpty(s.RelationshipId)).Select(static s => s.RelationshipId),
            StringComparer.Ordinal
        );

        foreach (var sheet in document.Sheets)
        {
            if (string.IsNullOrEmpty(sheet.PartUri))
            {
                string uri;
                do
                    uri = $"/xl/worksheets/sheet{nextSheet++}.xml";
                while (!usedUris.Add(uri) || package.TryGetPart(uri) != null);
                sheet.PartUri = uri;
            }

            if (!string.IsNullOrEmpty(sheet.RelationshipId)) continue;

            string relId;
            do
                relId = $"rId{nextRel++}";
            while (!usedRelIds.Add(relId));
            sheet.RelationshipId = relId;
        }
    }

    private static void WriteWorksheetParts(OpcPackage package, ISpreadsheetDocument document)
    {
        foreach (var sheet in document.Sheets)
        {
            // Preserve the existing part's relationships (drawings, hyperlinks, comments, tables)
            // across the content replacement; AddOrReplacePart creates a fresh part with none.
            var existing = package.TryGetPart(sheet.PartUri);
            var preservedRels = existing?.Relationships.ToList() ?? [];

            var bytes = WorksheetWriter.Write(sheet);
            package.AddOrReplacePart(sheet.PartUri, SmlNames.ContentTypeWorksheet, bytes);

            foreach (var rel in preservedRels)
                package.AddRelationship(sheet.PartUri, rel.Id, rel.RelationshipType, rel.TargetUri, rel.IsExternal);
        }
    }

    // ── Tables (ListObjects) ────────────────────────────────────────────────────

    private static void AssignTableIdentities(OpcPackage package, ISpreadsheetDocument document)
    {
        var nextTableNumber = 1;
        var usedUris = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var sheet in document.Sheets)
        {
            if (!sheet.TablesMaterialised)
                continue;

            var sheetRelIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var table in sheet.TablesOrNull!.All)
            {
                if (string.IsNullOrEmpty(table.PartUri))
                {
                    string uri;
                    do
                        uri = $"/xl/tables/table{nextTableNumber++}.xml";
                    while (!usedUris.Add(uri) || package.TryGetPart(uri) != null);
                    table.PartUri = uri;
                }
                else
                    usedUris.Add(table.PartUri);

                if (string.IsNullOrEmpty(table.RelationshipId))
                {
                    var n = 1;
                    string relId;
                    do
                        relId = $"rIdTbl{n++}";
                    while (!sheetRelIds.Add(relId));
                    table.RelationshipId = relId;
                }
                else
                    sheetRelIds.Add(table.RelationshipId);
            }
        }
    }

    private static void WriteTableParts(OpcPackage package, ISpreadsheetDocument document)
    {
        foreach (var sheet in document.Sheets)
        {
            if (!sheet.TablesMaterialised)
                continue;

            foreach (var table in sheet.TablesOrNull!.All)
            {
                package.AddOrReplacePart(table.PartUri, SmlNames.ContentTypeTable, TableWriter.Write(table));

                var hasRel = package.GetRelationships(sheet.PartUri)
                    .Any(r => r.Id == table.RelationshipId);
                if (hasRel) continue;

                var target = RelativeToSheet(table.PartUri);
                package.AddRelationship(sheet.PartUri, table.RelationshipId, SmlNames.RelTypeTable, target);
            }
        }
    }

    private static string RelativeToSheet(string tableUri) =>
        tableUri.StartsWith("/xl/", StringComparison.OrdinalIgnoreCase)
            ? "../" + tableUri["/xl/".Length..]
            : tableUri;

    private static void RemoveOrphanedWorksheetParts(OpcPackage package, ISpreadsheetDocument document)
    {
        var live = new HashSet<string>(document.Sheets.Select(static s => s.PartUri), StringComparer.OrdinalIgnoreCase);
        var orphans = package.Parts
            .Where(static p => p.ContentType.Equals(SmlNames.ContentTypeWorksheet, StringComparison.Ordinal))
            .Where(p => !live.Contains(p.Uri))
            .Select(static p => p.Uri)
            .ToList();

        foreach (var uri in orphans)
            package.RemovePart(uri);
    }

    private static void WriteSharedStrings(OpcPackage package, SpreadsheetDocument document)
    {
        var table = document.SharedStrings;

        // Only (re)write when entries changed or the part already exists; an untouched empty table
        // needs no part.
        if (!table.IsDirty && package.TryGetPart(SharedStringsUri) == null)
            return;

        package.AddOrReplacePart(SharedStringsUri, SmlNames.ContentTypeSharedStrings, table.Serialize());
    }

    // ── workbook.xml ─────────────────────────────────────────────────────────

    private static void WriteWorkbookPart(OpcPackage package, ISpreadsheetDocument document, XlsxSaveOptions options)
    {
        var existing = package.TryGetPart(WorkbookUri);
        var root = existing != null
            ? new XElement(OoXmlHelper.ParseXml(existing.Data).Root!)
            : CreateWorkbookRoot();

        WriteDateSystem(root, document);
        WriteSheetList(root, document);
        WriteDefinedNames(root, document);
        WriteWorkbookProtection(root, document);
        WriteCalcPr(root, options);

        // <pivotCaches> registers each pivot cache + adds workbook → cacheDefinition relationships
        // (preserved by WriteWorkbookRelationships, which runs next). Per CT_Workbook, pivotCaches
        // comes AFTER calcPr (…definedNames → calcPr → oleSize → customWorkbookViews → pivotCaches…).
        root.Child(SmlNames.X + "pivotCaches")?.Remove();
        var pivotCaches = PivotCaches(package, document);
        if (pivotCaches != null)
        {
            var afterAnchor = root.Child(SmlNames.CalcPr)
                              ?? root.Child(SmlNames.DefinedNames)
                              ?? root.Child(SmlNames.Sheets);
            if (afterAnchor != null)
                afterAnchor.AddAfterSelf(pivotCaches);
            else
                root.Add(pivotCaches);
        }

        var bytes = new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), root).ToUtf8Bytes();
        package.AddOrReplacePart(WorkbookUri, SmlNames.ContentTypeWorkbook, bytes);
    }

    private static XElement CreateWorkbookRoot() =>
        new(
            SmlNames.Workbook,
            new XAttribute("xmlns", SmlNames.X.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "r", SmlNames.R.NamespaceName)
        );

    private static void WriteDateSystem(XElement root, ISpreadsheetDocument document)
    {
        var workbookPr = root.Child(SmlNames.WorkbookPr);
        if (!document.Date1904)
        {
            // 1900 is the default; drop a redundant date1904="0" only, keep other attributes.
            workbookPr?.Attribute("date1904")?.Remove();
            return;
        }

        if (workbookPr == null)
        {
            workbookPr = new XElement(SmlNames.WorkbookPr);
            root.AddFirst(workbookPr);
        }

        workbookPr.SetAttributeValue("date1904", "1");
    }

    private static void WriteSheetList(XElement root, ISpreadsheetDocument document)
    {
        root.Child(SmlNames.Sheets)?.Remove();

        var sheets = new XElement(SmlNames.Sheets);
        foreach (var sheet in document.Sheets)
        {
            var element = new XElement(
                SmlNames.Sheet,
                new XAttribute("name", sheet.Name),
                new XAttribute("sheetId", sheet.SheetId.ToString(CultureInfo.InvariantCulture)),
                new XAttribute(SmlNames.R + "id", sheet.RelationshipId)
            );

            if (sheet.State != SheetState.Visible)
                element.SetAttributeValue("state", sheet.State == SheetState.Hidden ? "hidden" : "veryHidden");

            sheets.Add(element);
        }

        // <sheets> follows workbookPr/fileVersion/bookViews; inserting after the last of those
        // (or first) keeps a schema-valid ordering for the common case.
        var anchor = root.Child(SmlNames.BookViews)
                     ?? root.Child(SmlNames.WorkbookPr)
                     ?? root.Child(SmlNames.FileVersion);
        if (anchor != null)
            anchor.AddAfterSelf(sheets);
        else
            root.AddFirst(sheets);
    }

    private static void WriteDefinedNames(XElement root, ISpreadsheetDocument document)
    {
        root.Child(SmlNames.DefinedNames)?.Remove();

        var names = document.DefinedNames.All;
        if (names.Count == 0)
            return;

        var definedNames = new XElement(SmlNames.DefinedNames);
        foreach (var name in names)
        {
            var element = new XElement(
                SmlNames.DefinedName,
                new XAttribute("name", name.Name),
                name.Formula
            );

            if (name.LocalSheetId is { } sheetId)
                element.SetAttributeValue("localSheetId", sheetId.ToString(CultureInfo.InvariantCulture));
            if (!string.IsNullOrEmpty(name.Comment))
                element.SetAttributeValue("comment", name.Comment);
            if (name.IsHidden)
                element.SetAttributeValue("hidden", "1");

            definedNames.Add(element);
        }

        // <definedNames> follows <sheets>.
        var sheets = root.Child(SmlNames.Sheets);
        if (sheets != null)
            sheets.AddAfterSelf(definedNames);
        else
            root.Add(definedNames);
    }

    private static void WriteWorkbookProtection(XElement root, ISpreadsheetDocument document)
    {
        root.Child(SmlNames.WorkbookProtection)?.Remove();

        var protection = document.Protection;
        if (!protection.IsProtected)
            return;

        var element = new XElement(SmlNames.WorkbookProtection);
        if (protection.LockStructure) element.SetAttributeValue("lockStructure", "1");
        if (protection.LockWindows) element.SetAttributeValue("lockWindows", "1");
        if (protection.PasswordHash != null) element.SetAttributeValue("workbookPassword", protection.PasswordHash);

        // <workbookProtection> follows <fileVersion>/<workbookPr> and precedes <bookViews>/<sheets>.
        var anchor = root.Child(SmlNames.Sheets);
        if (anchor != null)
            anchor.AddBeforeSelf(element);
        else
            root.AddFirst(element);
    }

    private static void WriteCalcPr(XElement root, XlsxSaveOptions options)
    {
        if (!options.RecalcAll)
            return;

        var calcPr = root.Child(SmlNames.CalcPr);
        if (calcPr == null)
        {
            calcPr = new XElement(SmlNames.CalcPr);
            root.Add(calcPr);
        }

        calcPr.SetAttributeValue("fullCalcOnLoad", "1");
    }

    // ── workbook.xml.rels ──────────────────────────────────────────────────────

    private static void WriteWorkbookRelationships(OpcPackage package, ISpreadsheetDocument document)
    {
        var workbookPart = package.GetPart(WorkbookUri);

        // Preserve every relationship that is not a worksheet relationship (styles, sharedStrings,
        // theme, pivotCache, …); worksheet rels are rebuilt from the live sheet list.
        var preserved = workbookPart.Relationships
            .Where(static r => !r.RelationshipType.Equals(SmlNames.RelTypeWorksheet, StringComparison.Ordinal))
            .ToList();

        package.ClearRelationships(WorkbookUri);

        foreach (var sheet in document.Sheets)
        {
            var target = RelativeToWorkbook(sheet.PartUri);
            package.AddRelationship(WorkbookUri, sheet.RelationshipId, SmlNames.RelTypeWorksheet, target);
        }

        foreach (var rel in preserved)
            package.AddRelationship(WorkbookUri, rel.Id, rel.RelationshipType, rel.TargetUri, rel.IsExternal);

        EnsureStylesRelationship(package, workbookPart);
        EnsureSharedStringsRelationship(package, workbookPart);
    }

    private static void EnsureSharedStringsRelationship(OpcPackage package, OpcPart workbookPart)
    {
        if (package.TryGetPart(SharedStringsUri) == null)
            return;

        var hasRel = workbookPart.Relationships
            .Any(static r => r.RelationshipType.Equals(SmlNames.RelTypeSharedStrings, StringComparison.Ordinal));
        if (hasRel)
            return;

        var relId = NextFreeRelId(package.GetPart(WorkbookUri));
        package.AddRelationship(WorkbookUri, relId, SmlNames.RelTypeSharedStrings, "sharedStrings.xml");
    }

    private static void EnsureStylesRelationship(OpcPackage package, OpcPart workbookPart)
    {
        var hasStyles = workbookPart.Relationships
            .Any(static r => r.RelationshipType.Equals(SmlNames.RelTypeStyles, StringComparison.Ordinal));
        if (hasStyles)
            return;

        var relId = NextFreeRelId(package.GetPart(WorkbookUri));
        package.AddRelationship(WorkbookUri, relId, SmlNames.RelTypeStyles, "styles.xml");
    }

    private static string NextFreeRelId(OpcPart part)
    {
        var used = new HashSet<string>(part.Relationships.Select(static r => r.Id), StringComparer.Ordinal);
        var n = 1;
        string relId;
        do
            relId = $"rId{n++}";
        while (!used.Add(relId));
        return relId;
    }

    private static string RelativeToWorkbook(string sheetUri) => OpcPackage.GetRelativeUri(WorkbookUri, sheetUri);

    // ── styles.xml ─────────────────────────────────────────────────────────────

    private static void WriteStylesPart(OpcPackage package, SpreadsheetDocument document)
    {
        // When the style book was materialised (loaded or mutated), serialize it. Otherwise keep the
        // existing raw part, or emit a minimal valid one for a fresh document.
        var styles = document.MaterialisedStyles;
        if (styles != null)
        {
            package.AddOrReplacePart(StylesUri, SmlNames.ContentTypeStyles, StylesWriter.Write(styles));
            return;
        }

        if (package.TryGetPart(StylesUri) == null)
            package.AddOrReplacePart(StylesUri, SmlNames.ContentTypeStyles, StylesWriter.Write(StyleBook.CreateDefault()));
    }

    // ── Minimal package for CreateBlank ─────────────────────────────────────────

    private static OpcPackage CreateMinimalPackage()
    {
        var package = OpcPackage.CreateEmpty();
        package.AddOrReplacePart(WorkbookUri, SmlNames.ContentTypeWorkbook, EmptyWorkbookBytes());
        package.AddPackageRelationship("rId1", SmlNames.RelTypeOfficeDocument, "xl/workbook.xml");
        return package;
    }

    private static byte[] EmptyWorkbookBytes() =>
        new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), CreateWorkbookRoot()).ToUtf8Bytes();

    // ── Document properties ──────────────────────────────────────────────────

    private static void WriteProperties(OpcPackage package, ISpreadsheetDocument document)
    {
        var props = document.Properties;

        PropertiesWriter.Write(package, props, static (pkg, innerProps) => PropertiesWriter.WriteApp(pkg, innerProps, "Unchained.Xlsx"));
    }
}
