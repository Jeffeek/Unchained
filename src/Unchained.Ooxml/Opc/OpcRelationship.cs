namespace Unchained.Ooxml.Opc;

/// <summary>
///     Represents a relationship between two OPC parts, or between a part and an external resource.
///     Relationships are defined in <c>_rels/*.rels</c> XML files inside the package.
/// </summary>
internal sealed class OpcRelationship
{
    /// <summary>
    ///     Initialises a new relationship.
    /// </summary>
    /// <param name="id">The unique relationship identifier within its scope (e.g. <c>rId1</c>).</param>
    /// <param name="relationshipType">The full relationship type URI.</param>
    /// <param name="targetUri">The URI of the target part or resource.</param>
    /// <param name="isExternal">
    ///     <see langword="true" /> when the target is an external URI (TargetMode="External");
    ///     <see langword="false" /> for internal parts.
    /// </param>
    public OpcRelationship(
        string id,
        string relationshipType,
        string targetUri,
        bool isExternal = false
    )
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(relationshipType);
        ArgumentNullException.ThrowIfNull(targetUri);

        Id = id;
        RelationshipType = relationshipType;
        TargetUri = targetUri;
        IsExternal = isExternal;
    }

    /// <summary>The unique identifier of this relationship within its scope (e.g. <c>rId1</c>).</summary>
    public string Id { get; }

    /// <summary>
    ///     The full relationship type URI that describes the nature of the relationship
    ///     (e.g. <c>http://schemas.openxmlformats.org/officeDocument/2006/relationships/slide</c>).
    /// </summary>
    public string RelationshipType { get; }

    /// <summary>
    ///     The URI of the target. For internal relationships this is relative to the source part;
    ///     for external relationships it is an absolute URL.
    /// </summary>
    public string TargetUri { get; }

    /// <summary>
    ///     <see langword="true" /> when the target is outside the package (TargetMode="External").
    /// </summary>
    public bool IsExternal { get; }
}
