namespace Unchained.Pptx.Shapes;

/// <summary>
///     A part referenced by a chart's own relationships — most commonly the embedded spreadsheet
///     backing the chart data, plus optional chart style/colour parts. Captured at load so the
///     custom writer can re-emit the chart part's <c>.rels</c> and the referenced parts, keeping the
///     chart's <c>r:id</c> references resolvable through load/save and cross-deck clone.
/// </summary>
internal sealed class ChartRelatedPart
{
    /// <summary>The relationship ID within the chart part's relationships (e.g. <c>rId1</c>).</summary>
    internal string RelationshipId { get; init; } = string.Empty;

    /// <summary>The OPC relationship type URI (e.g. package/relationships/oleObject).</summary>
    internal string RelationshipType { get; init; } = string.Empty;

    /// <summary><see langword="true" /> when the target is external (TargetMode="External").</summary>
    internal bool IsExternal { get; init; }

    /// <summary>
    ///     For internal parts, the absolute source part URI (used only to derive a name on write).
    ///     For external relationships, the verbatim external target.
    /// </summary>
    internal string Target { get; init; } = string.Empty;

    /// <summary>The MIME content type of the referenced part (empty for external targets).</summary>
    internal string ContentType { get; init; } = string.Empty;

    /// <summary>The raw bytes of the referenced part (<see langword="null" /> for external targets).</summary>
    internal byte[]? Data { get; init; }
}
