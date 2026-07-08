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

    /// <summary>
    ///     When <see langword="true" />, each top-level shape's group element is annotated with a
    ///     <c>data-shape-index</c> attribute (its 0-based index within the slide) so interactive
    ///     consumers (e.g. an editor) can hit-test shapes by their painted geometry.
    ///     Default: <see langword="false" />.
    /// </summary>
    public bool AnnotateShapes { get; init; }

    /// <summary>
    ///     When <see langword="true" />, embedded fonts from the presentation are inlined as
    ///     <c>@font-face</c> rules in the SVG <c>&lt;defs&gt;</c> so the original typeface
    ///     renders regardless of what's installed on the client machine.
    ///     Default: <see langword="true" />.
    /// </summary>
    public bool EmbedFonts { get; init; } = true;
}
