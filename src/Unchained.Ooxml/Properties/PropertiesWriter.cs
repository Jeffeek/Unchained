using System.Globalization;
using System.Xml.Linq;
using Unchained.Ooxml.Opc;
using Unchained.Ooxml.Xml;

namespace Unchained.Ooxml.Properties;

/// <summary>
///     Writes the OPC core properties (<c>docProps/core.xml</c>) and extended app properties
///     (<c>docProps/app.xml</c>) from an <see cref="OoXmlCoreProperties" /> instance,
///     creating the parts and their package relationships when absent.
/// </summary>
internal static class PropertiesWriter
{
    private const string CoreUri = "/docProps/core.xml";
    private const string AppUri = "/docProps/app.xml";

    private const string CoreContentType = OoxmlContentTypes.CoreProperties;
    private const string AppContentType = OoxmlContentTypes.ExtendedProperties;

    private const string CoreRelType = OoxmlNamespaces.CorePropertiesFull;
    private const string AppRelType = OoxmlNamespaces.ExtendedPropertiesFull;

    private static readonly XNamespace Cp = "http://schemas.openxmlformats.org/package/2006/metadata/core-properties";
    private static readonly XNamespace Dc = "http://purl.org/dc/elements/1.1/";
    private static readonly XNamespace Dcterms = "http://purl.org/dc/terms/";
    private static readonly XNamespace Xsi = "http://www.w3.org/2001/XMLSchema-instance";
    private static readonly XNamespace Ep = "http://schemas.openxmlformats.org/officeDocument/2006/extended-properties";

    /// <summary>
    ///     Writes core properties to <c>docProps/core.xml</c> and, when <paramref name="writeApp" /> is
    ///     provided, writes extended app properties to <c>docProps/app.xml</c>.
    /// </summary>
    public static void Write(OpcPackage package, OoXmlCoreProperties props, Action<OpcPackage, OoXmlCoreProperties>? writeApp = null)
    {
        WriteCore(package, props);
        writeApp?.Invoke(package, props);
    }

    private static void WriteCore(OpcPackage package, OoXmlCoreProperties props)
    {
        var root = new XElement(
            Cp + "coreProperties",
            new XAttribute(XNamespace.Xmlns + "cp", Cp.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "dc", Dc.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "dcterms", Dcterms.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "xsi", Xsi.NamespaceName)
        );

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
        AddDate(root, Cp + "lastPrinted", props.LastPrinted);

        var bytes = new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), root).ToUtf8Bytes();
        package.AddOrReplacePart(CoreUri, CoreContentType, bytes);
        package.EnsurePackageRelationship(CoreRelType, "docProps/core.xml");
    }

    private static void AddIfPresent(XContainer root, XName name, string? value)
    {
        if (!string.IsNullOrEmpty(value))
            root.Add(new XElement(name, value));
    }

    private static void AddDate(XContainer root, XName name, DateTimeOffset? value)
    {
        if (value is null)
            return;

        root.Add(
            new XElement(
                name,
                new XAttribute(Xsi + "type", "dcterms:W3CDTF"),
                value.Value.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)
            )
        );
    }

    /// <summary>
    ///     Writes the extended app properties (<c>docProps/app.xml</c>) from an
    ///     <see cref="OoXmlCoreProperties" /> instance, creating the part and its package
    ///     relationship when absent.
    /// </summary>
    public static void WriteApp(OpcPackage package, OoXmlCoreProperties props, string defaultAppName)
    {
        var root = new XElement(
            Ep + "Properties",
            new XAttribute("xmlns", Ep.NamespaceName)
        );

        AddIfPresent(root, Ep + "Application", props.ApplicationName ?? defaultAppName);
        AddIfPresent(root, Ep + "Company", props.Company);
        AddIfPresent(root, Ep + "Manager", props.Manager);

        var bytes = new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), root).ToUtf8Bytes();
        package.AddOrReplacePart(AppUri, AppContentType, bytes);
        package.EnsurePackageRelationship(AppRelType, "docProps/app.xml");
    }
}
