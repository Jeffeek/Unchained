namespace Unchained.Pdf.Models;

/// <summary>
/// Controls which document-level metadata is copied into the merged output
/// when combining documents via <see cref="IDocumentMerger"/>.
/// </summary>
public sealed record MergeOptions(
    /// <summary>
    /// When <see langword="true"/>, bookmarks (outline entries, <c>/Outlines</c>)
    /// from each source document are copied and renumbered in the merged output.
    /// </summary>
    bool CopyOutlines = true,

    /// <summary>
    /// When <see langword="true"/>, named destinations (<c>/Dests</c>) from each source
    /// document are merged into the output name tree. Duplicate names from later
    /// documents overwrite earlier ones.
    /// </summary>
    bool CopyNamedDestinations = true,

    /// <summary>
    /// When <see langword="true"/>, shared resources (fonts, images) that appear in
    /// multiple source documents are de-duplicated in the merged output to reduce file size.
    /// This increases merge time proportionally to the number of resources.
    /// </summary>
    bool OptimizeResources = false
)
{
    /// <summary>Default options: copy outlines and named destinations, no resource optimization.</summary>
    public static readonly MergeOptions Default = new();

    /// <summary>
    /// Minimal merge: skip outline and named-destination copying for maximum throughput.
    /// Use when the merged document will be processed further or when metadata is not needed.
    /// </summary>
    public static readonly MergeOptions Fast = new(CopyOutlines: false, CopyNamedDestinations: false);
}
