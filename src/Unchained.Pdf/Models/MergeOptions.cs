namespace Unchained.Pdf.Models;

/// <summary>Controls which document-level metadata is copied into the merged output.</summary>
/// <param name="CopyOutlines">
///     When <see langword="true" />, bookmarks (<c>/Outlines</c>) from each source document
///     are copied and renumbered in the merged output.
/// </param>
/// <param name="CopyNamedDestinations">
///     When <see langword="true" />, named destinations (<c>/Dests</c>) from each source
///     document are merged into the output name tree. Duplicate names from later documents
///     overwrite earlier ones.
/// </param>
/// <param name="OptimizeResources">
///     When <see langword="true" />, shared resources (fonts, images) that appear in multiple
///     source documents are de-duplicated in the merged output to reduce file size.
/// </param>
public sealed record MergeOptions(
    bool CopyOutlines = true,
    bool CopyNamedDestinations = true,
    bool OptimizeResources = false
)
{
    /// <summary>Default options: copy outlines and named destinations, no resource optimization.</summary>
    public static readonly MergeOptions Default = new();

    /// <summary>
    ///     Minimal merge: skip outline and named-destination copying for maximum throughput.
    ///     Use when the merged document will be processed further or when metadata is not needed.
    /// </summary>
    public static readonly MergeOptions Fast = new(false, false);
}
