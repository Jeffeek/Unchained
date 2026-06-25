using System.Globalization;
using System.Xml.Linq;
using Unchained.Ooxml.Opc;
using Unchained.Ooxml.Xml;
using Unchained.Xlsx.Models;

namespace Unchained.Xlsx.Writing;

/// <summary>
///     Writes the OPC core properties (<c>docProps/core.xml</c>) and extended app properties
///     (<c>docProps/app.xml</c>) from a <see cref="WorkbookProperties" />, creating the parts and
///     their package relationships when absent.
/// </summary>
internal static class WorkbookPropertiesWriter
{
    private const string CoreUri = "/docProps/core.xml";
    private const string AppUri = "/docProps/app.xml";

    private const string CoreContentType = "application/vnd.openxmlformats-package.core-properties+xml";
    private const string AppContentType = "application/vnd.openxmlformats-officedocument.extended-properties+xml";

    private const string CoreRelType = "http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties";
    private const string AppRelType = "http://schemas.openxmlformats.org/officeDocument/2006/relationships/extended-properties";

    private static readonly XNamespace Cp = "http://schemas.openxmlformats.org/package/2006/metadata/core-properties";
    private static readonly XNamespace Dc = "http://purl.org/dc/elements/1.1/";
    private static readonly XNamespace Dcterms = "http://purl.org/dc/terms/";
    private static readonly XNamespace Xsi = "http://www.w3.org/2001/XMLSchema-instance";
    private static readonly XNamespace Ep = "http://schemas.openxmlformats.org/officeDocument/2006/extended-properties";

    public static void Write(OpcPackage package, WorkbookProperties props)
    {
        WriteCore(package, props);
        WriteApp(package, props);
    }

    private static void WriteCore(OpcPackage package, WorkbookProperties props)
    {
        var root = new XElement(
            Cp + "coreProperties",
            new XAttribute(XNamespace.Xmlns + "cp", Cp.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "dc", Dc.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "dcterms", Dcterms.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "xsi", Xsi.NamespaceName));

        AddIfPresent(root, Dc + "title", props.Title);
        AddIfPresent(root, Dc + "subject", props.Subject);
        AddIfPresent(root, Dc + "creator", props.Author);
        AddIfPresent(root, Cp + "keywords", props.Keywords);
        AddIfPresent(root, Dc + "description", props.Description);
        AddIfPresent(root, Cp + "lastModifiedBy", props.LastModifiedBy);
        AddIfPresent(root, Cp + "category", props.Category);
        AddIfPresent(root, Cp + "contentStatus", props.ContentStatus);
        AddDate(root, Dcterms + "created", props.Created);
        AddDate(root, Dcterms + "modified", props.Modified);

        var bytes = new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), root).ToUtf8Bytes();
        package.AddOrReplacePart(CoreUri, CoreContentType, bytes);
        EnsurePackageRelationship(package, CoreRelType, "docProps/core.xml");
    }

    private static void WriteApp(OpcPackage package, WorkbookProperties props)
    {
        var root = new XElement(
            Ep + "Properties",
            new XAttribute("xmlns", Ep.NamespaceName));

        AddIfPresent(root, Ep + "Application", props.ApplicationName ?? "Unchained.Xlsx");
        AddIfPresent(root, Ep + "Company", props.Company);
        AddIfPresent(root, Ep + "Manager", props.Manager);

        var bytes = new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), root).ToUtf8Bytes();
        package.AddOrReplacePart(AppUri, AppContentType, bytes);
        EnsurePackageRelationship(package, AppRelType, "docProps/app.xml");
    }

    private static void AddIfPresent(XElement root, XName name, string? value)
    {
        if (!string.IsNullOrEmpty(value))
            root.Add(new XElement(name, value));
    }

    private static void AddDate(XElement root, XName name, DateTimeOffset? value)
    {
        if (value is null)
            return;

        root.Add(new XElement(name,
            new XAttribute(Xsi + "type", "dcterms:W3CDTF"),
            value.Value.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)));
    }

    private static void EnsurePackageRelationship(OpcPackage package, string relType, string target)
    {
        var exists = package.PackageRelationships
            .Any(r => r.RelationshipType.Equals(relType, StringComparison.Ordinal));
        if (exists)
            return;

        var used = new HashSet<string>(package.PackageRelationships.Select(r => r.Id), StringComparer.Ordinal);
        var n = 1;
        string relId;
        do
            relId = $"rId{n++}";
        while (!used.Add(relId));

        package.AddPackageRelationship(relId, relType, target);
    }
}
