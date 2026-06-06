namespace Unchained.Ooxml.Opc;

/// <summary>
/// Represents a single part in an OPC package — a named byte stream with a content type
/// and an optional set of relationships to other parts.
/// </summary>
internal sealed class OpcPart
{
    private readonly List<OpcRelationship> _relationships = [];

    /// <summary>
    /// Initialises a new part with the given URI, content type, and raw byte data.
    /// </summary>
    /// <param name="uri">Absolute part URI (e.g. <c>/ppt/slides/slide1.xml</c>).</param>
    /// <param name="contentType">MIME content type for this part.</param>
    /// <param name="data">Raw bytes of the part.</param>
    public OpcPart(string uri, string contentType, byte[] data)
    {
        ArgumentNullException.ThrowIfNull(uri);
        ArgumentNullException.ThrowIfNull(contentType);
        ArgumentNullException.ThrowIfNull(data);

        Uri = uri;
        ContentType = contentType;
        Data = data;
    }

    /// <summary>The absolute URI of this part within the package (e.g. <c>/ppt/presentation.xml</c>).</summary>
    public string Uri { get; }

    /// <summary>The MIME content type of this part.</summary>
    public string ContentType { get; }

    /// <summary>The raw bytes of this part.</summary>
    public byte[] Data { get; set; }

    /// <summary>The relationships defined for this part.</summary>
    public IReadOnlyList<OpcRelationship> Relationships => _relationships;

    /// <summary>Adds a relationship to this part's relationship list.</summary>
    internal void AddRelationship(OpcRelationship relationship) =>
        _relationships.Add(relationship);

    /// <summary>
    /// Finds the first relationship with the given type, or <see langword="null"/> if none exists.
    /// </summary>
    /// <param name="relationshipType">
    /// The full relationship type URI
    /// (e.g. <c>http://schemas.openxmlformats.org/officeDocument/2006/relationships/slide</c>).
    /// </param>
    public OpcRelationship? FindRelationship(string relationshipType) =>
        _relationships.FirstOrDefault(r => r.RelationshipType.Equals(relationshipType, StringComparison.Ordinal));

    /// <summary>Returns all relationships with the given type.</summary>
    public IReadOnlyList<OpcRelationship> FindRelationships(string relationshipType) =>
        _relationships.Where(r => r.RelationshipType.Equals(relationshipType, StringComparison.Ordinal)).ToList();

    /// <summary>Resolves a target URI relative to this part's own URI.</summary>
    /// <param name="targetUri">
    /// The target URI from a relationship — may be relative (e.g. <c>slides/slide1.xml</c>)
    /// or absolute (e.g. <c>/ppt/slides/slide1.xml</c>).
    /// </param>
    public string ResolveUri(string targetUri)
    {
        if (targetUri.StartsWith('/'))
            return targetUri;

        var baseDir = System.IO.Path.GetDirectoryName(Uri)?.Replace('\\', '/') ?? "/";
        if (!baseDir.StartsWith('/'))
            baseDir = "/" + baseDir;

        var combined = baseDir.TrimEnd('/') + "/" + targetUri;
        return NormalisePath(combined);
    }

    private static string NormalisePath(string path)
    {
        var segments = path.Split('/');
        var stack = new Stack<string>();

        foreach (var segment in segments)
        {
            switch (segment)
            {
                case "" or ".":
                    break;
                case "..":
                {
                    if (stack.Count > 0)
                        stack.Pop();
                    break;
                }
                default:
                    stack.Push(segment);
                    break;
            }
        }

        return "/" + string.Join("/", stack.Reverse());
    }
}
