namespace Unchained.Pptx.Export;

/// <summary>
///     Options that control HTML5 export of a presentation.
///     Each non-hidden slide is exported as a separate <c>.html</c> file.
/// </summary>
public sealed record HtmlSaveOptions
{
    /// <summary>A default instance with all settings at their defaults.</summary>
    public static readonly HtmlSaveOptions Default = new();

    /// <summary>
    ///     When <see langword="true" />, hidden slides are included in the output.
    ///     Default: <see langword="false" />.
    /// </summary>
    public bool IncludeHiddenSlides { get; init; }

    /// <summary>
    ///     When <see langword="true" />, images are embedded as Base64 data URIs.
    ///     When <see langword="false" />, images are written as separate files next
    ///     to the HTML output.
    ///     Default: <see langword="true" />.
    /// </summary>
    public bool EmbedImages { get; init; } = true;

    /// <summary>
    ///     Optional CSS to inject into the <c>&lt;style&gt;</c> block of every slide page.
    /// </summary>
    public string? AdditionalCss { get; init; }

    /// <summary>
    ///     An optional progress callback that receives values in the range [0.0, 1.0].
    /// </summary>
    public IProgress<double>? Progress { get; init; }
}
