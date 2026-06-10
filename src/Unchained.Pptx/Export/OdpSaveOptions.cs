namespace Unchained.Pptx.Export;

/// <summary>
/// Options that control export of a presentation to OpenDocument Presentation (<c>.odp</c>) format.
/// </summary>
public sealed record OdpSaveOptions
{
    /// <summary>
    /// When <see langword="true"/>, hidden slides are included in the output. Default: <see langword="true"/>
    /// (ODF marks them with a draw:page presentation property rather than omitting them).
    /// </summary>
    public bool IncludeHiddenSlides { get; init; } = true;

    /// <summary>
    /// When <see langword="true"/>, embedded images are written into the package <c>Pictures/</c> folder.
    /// Default: <see langword="true"/>.
    /// </summary>
    public bool EmbedImages { get; init; } = true;

    /// <summary>An optional progress callback that receives values in the range [0.0, 1.0].</summary>
    public IProgress<double>? Progress { get; init; }

    /// <summary>A default instance with all settings at their defaults.</summary>
    public static readonly OdpSaveOptions Default = new();
}
