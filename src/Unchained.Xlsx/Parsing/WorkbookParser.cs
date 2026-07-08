using System.Globalization;
using System.Xml.Linq;
using Unchained.Ooxml.Opc;
using Unchained.Ooxml.Properties;
using Unchained.Ooxml.Xml;
using Unchained.Xlsx.Abstractions;
using Unchained.Xlsx.Core;
using Unchained.Xlsx.Core.Xml;
using Unchained.Xlsx.DefinedNames;
using Unchained.Xlsx.Engine;
using Unchained.Xlsx.Models.Sheets;
using Unchained.Xlsx.Worksheets;

namespace Unchained.Xlsx.Parsing;

/// <summary>
///     Parses an OPC package into a <see cref="SpreadsheetDocument" />: locates the workbook part,
///     reads the sheet list, date system, defined names, and document properties, and wires each
///     worksheet to its backing part. Cell content is parsed lazily by <see cref="WorksheetParser" />.
/// </summary>
internal static class WorkbookParser
{
    public static SpreadsheetDocument Parse(OpcPackage package)
    {
        var workbookRel = package.PackageRelationships
            .FirstOrDefault(static r => r.RelationshipType.Equals(SmlNames.RelTypeOfficeDocument, StringComparison.Ordinal));

        if (workbookRel == null)
            throw new SpreadsheetException("The package does not contain a workbook relationship.");

        var workbookUri = "/" + workbookRel.TargetUri.TrimStart('/');
        var workbookPart = package.TryGetPart(workbookUri)
                           ?? throw new SpreadsheetException($"Workbook part not found: '{workbookUri}'.");

        var doc = OoXmlHelper.ParseXml(workbookPart.Data);
        var root = doc.Root
                   ?? throw new SpreadsheetException("workbook.xml has no root element.");

        var document = new SpreadsheetDocument(package);

        // Date system
        var workbookPr = root.Child(SmlNames.WorkbookPr);
        document.Date1904 = workbookPr?.GetAttrBool("date1904") == true;

        ReadSheets(document, root, workbookPart);
        ReadDefinedNames(document, root);
        ReadWorkbookProtection(document, root);
        ReadProperties(document, package);

        return document;
    }

    private static void ReadWorkbookProtection(ISpreadsheetDocument document, XElement root)
    {
        var element = root.Child(SmlNames.WorkbookProtection);
        if (element == null)
            return;

        document.Protection.LockStructure = element.GetAttrBool("lockStructure") == true;
        document.Protection.LockWindows = element.GetAttrBool("lockWindows") == true;
        document.Protection.PasswordHash = element.GetAttr("workbookPassword");
    }

    private static void ReadDefinedNames(ISpreadsheetDocument document, XElement root)
    {
        var definedNames = root.Child(SmlNames.DefinedNames);
        if (definedNames == null)
            return;

        // ReSharper disable once LoopCanBePartlyConvertedToQuery
        foreach (var element in definedNames.Children(SmlNames.DefinedName))
        {
            var name = element.GetAttr("name");
            if (name == null)
                continue;

            var localSheetId = element.GetAttrInt("localSheetId");
            var defined = new DefinedName(name, element.Value, localSheetId)
            {
                Comment = element.GetAttr("comment"),
                IsHidden = element.GetAttrBool("hidden") == true
            };
            document.DefinedNames.AddExisting(defined);
        }
    }

    private static void ReadSheets(SpreadsheetDocument document, XElement root, OpcPart workbookPart)
    {
        var sheetsElement = root.Child(SmlNames.Sheets);
        if (sheetsElement == null)
            return;

        foreach (var sheetElement in sheetsElement.Children(SmlNames.Sheet))
        {
            var name = sheetElement.GetAttr("name") ?? "Sheet";
            var sheetId = sheetElement.GetAttrInt("sheetId", 0);
            var relId = (string?)sheetElement.Attribute(SmlNames.R + "id") ?? string.Empty;
            var state = ParseState(sheetElement.GetAttr("state"));

            var partUri = ResolveSheetUri(workbookPart, relId);

            // ReSharper disable once BadListLineBreaks
            var sheet = new Worksheet(
                document,
                name,
                sheetId,
                relId,
                partUri,
                state
            );

            if (!string.IsNullOrEmpty(partUri))
            {
                var part = document.Package?.TryGetPart(partUri);
                if (part != null)
                    sheet.RawElement = OoXmlHelper.ParseXml(part.Data).Root;
            }

            ReadTabColor(sheet);
            document.Sheets.AddExisting(sheet);
        }
    }

    private static void ReadTabColor(Worksheet sheet)
    {
        var rgb = sheet.RawElement?.Child(SmlNames.SheetPr)?.Child(SmlNames.TabColor)?.GetAttr(SmlNames.AttrRgb);
        sheet.TabColor = SmlColor.FromHexArgb(rgb);
    }

    private static string ResolveSheetUri(OpcPart workbookPart, string relId)
    {
        if (string.IsNullOrEmpty(relId))
            return string.Empty;

        var rel = workbookPart.Relationships.FirstOrDefault(r => r.Id == relId);
        return rel == null ? string.Empty : workbookPart.ResolveUri(rel.TargetUri);
    }

    private static SheetState ParseState(string? state) => state switch
    {
        "hidden" => SheetState.Hidden,
        "veryHidden" => SheetState.VeryHidden,
        _ => SheetState.Visible
    };

    // ── Document properties ──────────────────────────────────────────────────

    private static void ReadProperties(ISpreadsheetDocument document, OpcPackage package)
    {
        var props = document.Properties;

        var corePart = FindPartByRelType(
            package,
            OoxmlNamespaces.PackageRelationships + "/" + OoxmlNamespaces.RelCoreProperties
        );
        if (corePart != null)
            ReadCoreProperties(props, OoXmlHelper.ParseXml(corePart.Data).Root);

        var appPart = FindPartByRelType(
            package,
            OoxmlNamespaces.OfficeDocument + "/" + OoxmlNamespaces.RelExtendedProperties
        );
        if (appPart != null)
            ReadAppProperties(props, OoXmlHelper.ParseXml(appPart.Data).Root);
    }

    private static OpcPart? FindPartByRelType(OpcPackage package, string relType)
    {
        var rel = package.PackageRelationships
            .FirstOrDefault(r => r.RelationshipType.Equals(relType, StringComparison.Ordinal));
        if (rel == null)
            return null;

        var uri = "/" + rel.TargetUri.TrimStart('/');
        return package.TryGetPart(uri);
    }

    private static void ReadCoreProperties(OoXmlCoreProperties props, XContainer? root)
    {
        if (root == null) return;

        XNamespace cp = "http://schemas.openxmlformats.org/package/2006/metadata/core-properties";
        XNamespace dc = "http://purl.org/dc/elements/1.1/";
        XNamespace dcterms = "http://purl.org/dc/terms/";

        props.Title = (string?)root.Element(dc + "title");
        props.Subject = (string?)root.Element(dc + "subject");
        props.Author = (string?)root.Element(dc + "creator");
        props.Description = (string?)root.Element(dc + "description");
        props.Keywords = (string?)root.Element(cp + "keywords");
        props.Category = (string?)root.Element(cp + "category");
        props.ContentStatus = (string?)root.Element(cp + "contentStatus");
        props.LastModifiedBy = (string?)root.Element(cp + "lastModifiedBy");
        props.Created = ParseDate((string?)root.Element(dcterms + "created"));
        props.Modified = ParseDate((string?)root.Element(dcterms + "modified"));
    }

    private static void ReadAppProperties(OoXmlCoreProperties props, XContainer? root)
    {
        if (root == null) return;

        XNamespace ep = "http://schemas.openxmlformats.org/officeDocument/2006/extended-properties";
        props.Company = (string?)root.Element(ep + "Company");
        props.Manager = (string?)root.Element(ep + "Manager");
        props.ApplicationName = (string?)root.Element(ep + "Application");
    }

    private static DateTimeOffset? ParseDate(string? raw) =>
        DateTimeOffset.TryParse(
            raw,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var value
        )
            ? value
            : null;
}
