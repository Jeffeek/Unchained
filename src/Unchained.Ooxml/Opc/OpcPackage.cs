using System.IO.Compression;
using System.Xml.Linq;

namespace Unchained.Ooxml.Opc;

/// <summary>
/// A thin Open Packaging Conventions (OPC) wrapper over <see cref="ZipArchive"/>.
/// Handles part storage, content-type registration, and relationship traversal
/// as defined in ECMA-376 Part 2 §10.
/// </summary>
/// <remarks>
/// Load an existing package with <see cref="Open(byte[])"/> or <see cref="Open(Stream)"/>.
/// Create a new empty package with <see cref="CreateEmpty"/>.
/// Serialize to bytes with <see cref="Save"/>.
/// </remarks>
internal sealed class OpcPackage : IDisposable
{
    private readonly Dictionary<string, OpcPart> _parts = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<OpcRelationship> _packageRelationships = [];
    private ContentTypeMap _contentTypes = new();
    private bool _disposed;

    private OpcPackage() { }

    // ── Factory methods ─────────────────────────────────────────────────────

    /// <summary>Creates a new empty OPC package with no parts.</summary>
    public static OpcPackage CreateEmpty() => new();

    /// <summary>Opens an OPC package from raw bytes.</summary>
    /// <exception cref="OoXmlException">Thrown when the bytes do not form a valid OPC package.</exception>
    public static OpcPackage Open(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        return Open(new MemoryStream(data, writable: false));
    }

    /// <summary>Opens an OPC package from a stream.</summary>
    /// <exception cref="OoXmlException">Thrown when the stream does not contain a valid OPC package.</exception>
    public static OpcPackage Open(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        try
        {
            var package = new OpcPackage();
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
            package.LoadFromArchive(archive);
            return package;
        }
        catch (InvalidDataException ex)
        {
            throw new OoXmlException("The stream does not contain a valid OOXML / OPC package.", ex);
        }
    }

    // ── Part access ─────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the part at the given URI, or <see langword="null"/> if no such part exists.
    /// Part URIs are case-insensitive and use forward slashes (e.g. <c>/ppt/presentation.xml</c>).
    /// </summary>
    public OpcPart? TryGetPart(string partUri)
    {
        ArgumentNullException.ThrowIfNull(partUri);
        var normalised = NormaliseUri(partUri);
        return _parts.TryGetValue(normalised, out var part) ? part : null;
    }

    /// <summary>Returns the part at the given URI.</summary>
    /// <exception cref="OoXmlException">Thrown when no part with that URI exists.</exception>
    public OpcPart GetPart(string partUri)
    {
        var part = TryGetPart(partUri)
            ?? throw new OoXmlException($"OPC part not found: '{partUri}'.");
        return part;
    }

    /// <summary>Returns all parts in the package.</summary>
    public IReadOnlyCollection<OpcPart> Parts => _parts.Values;

    /// <summary>
    /// Adds or replaces a part. The content type is registered automatically.
    /// </summary>
    /// <param name="partUri">Absolute part URI (e.g. <c>/ppt/slides/slide1.xml</c>).</param>
    /// <param name="contentType">MIME type (e.g. <c>application/vnd.openxmlformats-officedocument.presentationml.slide+xml</c>).</param>
    /// <param name="data">Raw bytes of the part.</param>
    public OpcPart AddOrReplacePart(string partUri, string contentType, byte[] data)
    {
        ArgumentNullException.ThrowIfNull(partUri);
        ArgumentNullException.ThrowIfNull(contentType);
        ArgumentNullException.ThrowIfNull(data);

        var normalised = NormaliseUri(partUri);
        var part = new OpcPart(normalised, contentType, data);
        _parts[normalised] = part;
        _contentTypes.Register(normalised, contentType);
        return part;
    }

    /// <summary>Removes the part at the given URI if it exists.</summary>
    public void RemovePart(string partUri)
    {
        var normalised = NormaliseUri(partUri);
        _parts.Remove(normalised);
    }

    // ── Relationship access ─────────────────────────────────────────────────

    /// <summary>Returns the package-level relationships (stored in <c>/_rels/.rels</c>).</summary>
    public IReadOnlyList<OpcRelationship> PackageRelationships => _packageRelationships;

    /// <summary>
    /// Returns the relationships for the part at <paramref name="partUri"/>
    /// (stored in the corresponding <c>_rels/</c> directory).
    /// </summary>
    public IReadOnlyList<OpcRelationship> GetRelationships(string partUri)
    {
        var normalised = NormaliseUri(partUri);
        return _parts.TryGetValue(normalised, out var part)
            ? part.Relationships
            : Array.Empty<OpcRelationship>();
    }

    /// <summary>
    /// Adds a relationship to the package-level relationships file (<c>/_rels/.rels</c>).
    /// </summary>
    public void AddPackageRelationship(
        string relationshipId,
        string relationshipType,
        string targetUri)
    {
        _packageRelationships.Add(new OpcRelationship(relationshipId, relationshipType, targetUri));
    }

    /// <summary>Adds a relationship to the specified source part.</summary>
    public void AddRelationship(
        string sourcePartUri,
        string relationshipId,
        string relationshipType,
        string targetUri)
    {
        GetPart(sourcePartUri).AddRelationship(
            new OpcRelationship(relationshipId, relationshipType, targetUri));
    }

    // ── Serialization ───────────────────────────────────────────────────────

    /// <summary>
    /// Serializes the package to a byte array in ZIP format.
    /// </summary>
    public byte[] Save()
    {
        using var ms = new MemoryStream();
        SaveTo(ms);
        return ms.ToArray();
    }

    /// <summary>
    /// Serializes the package into <paramref name="stream"/>.
    /// </summary>
    public void SaveTo(Stream stream)
    {
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true);

        var contentTypesXml = _contentTypes.Serialize();
        WriteEntry(archive, "[Content_Types].xml", contentTypesXml);

        if (_packageRelationships.Count > 0)
            WriteEntry(archive, "_rels/.rels", SerializeRelationships(_packageRelationships));

        foreach (var part in _parts.Values)
        {
            var entryName = part.Uri.TrimStart('/');
            WriteEntry(archive, entryName, part.Data);

            if (part.Relationships.Count > 0)
            {
                var relsPath = BuildRelationshipsPath(part.Uri);
                WriteEntry(archive, relsPath.TrimStart('/'), SerializeRelationships(part.Relationships));
            }
        }
    }

    // ── Loading ─────────────────────────────────────────────────────────────

    private void LoadFromArchive(ZipArchive archive)
    {
        var entries = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in archive.Entries)
        {
            using var entryStream = entry.Open();
            using var ms = new MemoryStream();
            entryStream.CopyTo(ms);
            entries[entry.FullName] = ms.ToArray();
        }

        if (entries.TryGetValue("[Content_Types].xml", out var ctBytes))
            _contentTypes = ContentTypeMap.Parse(ctBytes);

        if (entries.TryGetValue("_rels/.rels", out var pkgRelsBytes))
            _packageRelationships.AddRange(ParseRelationships(pkgRelsBytes));

        foreach (var (name, data) in entries)
        {
            if (IsMetaEntry(name)) continue;

            var uri = "/" + name.Replace('\\', '/');
            var contentType = _contentTypes.GetContentType(uri) ?? "application/octet-stream";
            var part = new OpcPart(uri, contentType, data);

            var relsEntryName = BuildRelationshipsPath(uri).TrimStart('/');
            if (entries.TryGetValue(relsEntryName, out var relsBytes))
            {
                foreach (var rel in ParseRelationships(relsBytes))
                    part.AddRelationship(rel);
            }

            _parts[uri] = part;
        }
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static string NormaliseUri(string uri) =>
        uri.StartsWith('/') ? uri : "/" + uri;

    private static string BuildRelationshipsPath(string partUri)
    {
        var dir = Path.GetDirectoryName(partUri)?.Replace('\\', '/') ?? string.Empty;
        var file = Path.GetFileName(partUri);
        return $"{dir}/_rels/{file}.rels";
    }

    private static bool IsMetaEntry(string name) =>
        name.Equals("[Content_Types].xml", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("_rels/", StringComparison.OrdinalIgnoreCase);

    private static void WriteEntry(ZipArchive archive, string name, byte[] data)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        using var stream = entry.Open();
        stream.Write(data, 0, data.Length);
    }

    private static IReadOnlyList<OpcRelationship> ParseRelationships(byte[] bytes)
    {
        var doc = XDocument.Load(new MemoryStream(bytes));
        var ns = XNamespace.Get("http://schemas.openxmlformats.org/package/2006/relationships");
        var list = new List<OpcRelationship>();

        foreach (var el in doc.Root?.Elements(ns + "Relationship") ?? [])
        {
            var id = (string?)el.Attribute("Id") ?? string.Empty;
            var type = (string?)el.Attribute("Type") ?? string.Empty;
            var target = (string?)el.Attribute("Target") ?? string.Empty;
            var mode = (string?)el.Attribute("TargetMode");
            list.Add(new OpcRelationship(id, type, target, mode == "External"));
        }

        return list;
    }

    private static byte[] SerializeRelationships(IReadOnlyList<OpcRelationship> relationships)
    {
        var ns = XNamespace.Get("http://schemas.openxmlformats.org/package/2006/relationships");
        var root = new XElement(ns + "Relationships");

        foreach (var rel in relationships)
        {
            var el = new XElement(ns + "Relationship",
                new XAttribute("Id", rel.Id),
                new XAttribute("Type", rel.RelationshipType),
                new XAttribute("Target", rel.TargetUri));

            if (rel.IsExternal)
                el.Add(new XAttribute("TargetMode", "External"));

            root.Add(el);
        }

        using var ms = new MemoryStream();
        new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), root).Save(ms);
        return ms.ToArray();
    }

    // ── IDisposable ─────────────────────────────────────────────────────────

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _parts.Clear();
    }
}
