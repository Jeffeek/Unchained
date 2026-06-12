namespace Unchained.Pptx.Export;

/// <summary>
///     Options that control SVG export of presentation slides.
///     Each slide is exported as a self-contained SVG document.
/// </summary>
public sealed record SvgSaveOptions
{
    /// <summary>A default instance with all settings at their defaults.</summary>
    public static readonly SvgSaveOptions Default = new();

    /// <summary>
    ///     When <see langword="true" />, hidden slides are included in the output.
    ///     Default: <see langword="false" />.
    /// </summary>
    public bool IncludeHiddenSlides { get; init; }

    /// <summary>
    ///     When <see langword="true" />, the SVG uses a percentage-based
    ///     <c>viewBox</c> so it scales to fill its container.
    ///     Default: <see langword="false" />.
    /// </summary>
    public bool Responsive { get; init; }

    /// <summary>
    ///     When <see langword="true" />, images are embedded as Base64 data URIs.
    ///     When <see langword="false" />, images are referenced as external files.
    ///     Default: <see langword="true" />.
    /// </summary>
    public bool EmbedImages { get; init; } = true;
}
