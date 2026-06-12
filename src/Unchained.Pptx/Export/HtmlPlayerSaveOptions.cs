namespace Unchained.Pptx.Export;

/// <summary>
///     Options that control export of a presentation as a single-file HTML5 player — one self-contained
///     <c>.html</c> document containing every slide with keyboard/click navigation.
/// </summary>
public sealed record HtmlPlayerSaveOptions
{
    /// <summary>A default instance with all settings at their defaults.</summary>
    public static readonly HtmlPlayerSaveOptions Default = new();

    /// <summary>
    ///     When <see langword="true" />, hidden slides are included in the player.
    ///     Default: <see langword="false" />.
    /// </summary>
    public bool IncludeHiddenSlides { get; init; }

    /// <summary>
    ///     When <see langword="true" />, images are embedded as Base64 data URIs (always the case for a
    ///     single-file player). Default: <see langword="true" />.
    /// </summary>
    public bool EmbedImages { get; init; } = true;

    /// <summary>The document <c>&lt;title&gt;</c>. Defaults to "Presentation" when null.</summary>
    public string? Title { get; init; }

    /// <summary>
    ///     When <see langword="true" />, a slide counter ("3 / 12") is shown in the player chrome.
    ///     Default: <see langword="true" />.
    /// </summary>
    public bool ShowSlideCounter { get; init; } = true;

    /// <summary>Optional CSS injected into the player's <c>&lt;style&gt;</c> block.</summary>
    public string? AdditionalCss { get; init; }

    /// <summary>An optional progress callback that receives values in the range [0.0, 1.0].</summary>
    public IProgress<double>? Progress { get; init; }
}
