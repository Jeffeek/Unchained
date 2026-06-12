namespace Unchained.Pptx.Engine;

/// <summary>
///     A raw OPC part captured verbatim at load time and re-emitted unchanged on save. Used for
///     content that Unchained does not model but must preserve for a faithful round-trip — the VBA
///     macro project (<c>vbaProject.bin</c>) and digital-signature parts (<c>/_xmlsignatures/*</c>).
/// </summary>
internal sealed class PreservedPart
{
    public required string Uri { get; init; }
    public required string ContentType { get; init; }
    public required byte[] Data { get; init; }

    /// <summary>Relationships originating from this part (kept verbatim with their original ids).</summary>
    public List<PreservedRelationship> Relationships { get; } = [];
}

/// <summary>A relationship captured for re-emission alongside a <see cref="PreservedPart" />.</summary>
internal sealed class PreservedRelationship
{
    /// <summary>The source part URI, or <see langword="null" /> for a package-level relationship.</summary>
    public string? SourceUri { get; init; }

    public required string Id { get; init; }
    public required string Type { get; init; }
    public required string Target { get; init; }
    public bool IsExternal { get; init; }
}

/// <summary>
///     The set of verbatim-preserved parts and relationships for a presentation, captured at parse
///     and replayed by the custom writer so unmodelled-but-important content survives a round-trip.
/// </summary>
internal sealed class PreservedContent
{
    /// <summary>Parts re-emitted verbatim (VBA project, signature parts, signature origin).</summary>
    public List<PreservedPart> Parts { get; } = [];

    /// <summary>
    ///     Relationships that connect a known source (presentation.xml or the package root) to a
    ///     preserved part. Re-emitted with fresh ids to avoid colliding with writer-generated ids.
    /// </summary>
    public List<PreservedRelationship> AnchorRelationships { get; } = [];

    /// <summary>
    ///     <see langword="true" /> when the presentation carries a VBA project, so the
    ///     <c>presentation.xml</c> part must be written with the macro-enabled content type.
    /// </summary>
    public bool HasMacros { get; set; }

    /// <summary>True when there is nothing to preserve (the common case).</summary>
    public bool IsEmpty => Parts.Count == 0 && AnchorRelationships.Count == 0;
}
