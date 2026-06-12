using System.Xml.Linq;

namespace Unchained.Ooxml.Opc;

/// <summary>
///     Parses and serializes the <c>[Content_Types].xml</c> part of an OPC package,
///     which maps part URIs and file extensions to their MIME content types.
/// </summary>
internal sealed class ContentTypeMap
{
    private readonly Dictionary<string, string> _extensionDefaults =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, string> _partOverrides =
        new(StringComparer.OrdinalIgnoreCase);

    // ── Factory ─────────────────────────────────────────────────────────────

    /// <summary>
    ///     Creates a <see cref="ContentTypeMap" /> pre-populated with the mandatory OPC defaults.
    /// </summary>
    public static ContentTypeMap CreateWithDefaults()
    {
        var map = new ContentTypeMap();
        map._extensionDefaults["rels"] = "application/vnd.openxmlformats-package.relationships+xml";
        map._extensionDefaults["xml"] = "application/xml";
        return map;
    }

    /// <summary>Parses a <c>[Content_Types].xml</c> byte array into a <see cref="ContentTypeMap" />.</summary>
    public static ContentTypeMap Parse(byte[] bytes)
    {
        var map = new ContentTypeMap();
        var doc = XDocument.Load(new MemoryStream(bytes));
        var ns = XNamespace.Get("http://schemas.openxmlformats.org/package/2006/content-types");

        foreach (var el in doc.Root?.Elements() ?? [])
        {
            if (el.Name == ns + "Default")
            {
                var extension = (string?)el.Attribute("Extension");
                var contentType = (string?)el.Attribute("ContentType");
                if (extension != null && contentType != null)
                    map._extensionDefaults[extension] = contentType;
            }
            else if (el.Name == ns + "Override")
            {
                var partName = (string?)el.Attribute("PartName");
                var contentType = (string?)el.Attribute("ContentType");
                if (partName != null && contentType != null)
                    map._partOverrides[partName] = contentType;
            }
        }

        return map;
    }

    // ── Lookup ───────────────────────────────────────────────────────────────

    /// <summary>
    ///     Returns the content type for a given part URI, or <see langword="null" /> if
    ///     neither an explicit override nor an extension default is registered.
    /// </summary>
    public string? GetContentType(string partUri)
    {
        if (_partOverrides.TryGetValue(partUri, out var exact))
            return exact;

        var extension = Path.GetExtension(partUri)?.TrimStart('.');
        if (extension != null && _extensionDefaults.TryGetValue(extension, out var byExtension))
            return byExtension;

        return null;
    }

    /// <summary>Registers an explicit content type override for the given part URI.</summary>
    public void Register(string partUri, string contentType) =>
        _partOverrides[partUri] = contentType;

    /// <summary>Registers an extension default (e.g. <c>"rels"</c> → relationship MIME type).</summary>
    public void RegisterDefault(string extension, string contentType) =>
        _extensionDefaults[extension] = contentType;

    // ── Serialization ────────────────────────────────────────────────────────

    /// <summary>Serializes the map to a <c>[Content_Types].xml</c> byte array.</summary>
    public byte[] Serialize()
    {
        var ns = XNamespace.Get("http://schemas.openxmlformats.org/package/2006/content-types");
        var root = new XElement(ns + "Types");

        foreach (var (extension, contentType) in _extensionDefaults.OrderBy(static kv => kv.Key))
        {
            root.Add(new XElement(ns + "Default",
                new XAttribute("Extension", extension),
                new XAttribute("ContentType", contentType)));
        }

        foreach (var (partName, contentType) in _partOverrides.OrderBy(static kv => kv.Key))
        {
            root.Add(new XElement(ns + "Override",
                new XAttribute("PartName", partName),
                new XAttribute("ContentType", contentType)));
        }

        using var ms = new MemoryStream();
        new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), root).Save(ms);
        return ms.ToArray();
    }
}
