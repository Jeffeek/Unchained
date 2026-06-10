namespace Unchained.Pptx.Shapes;

/// <summary>
/// A hyperlink action attached to a shape or text run.
/// Describes the navigation target (URL, slide, or built-in show action) that activates when the
/// user clicks the object during a slide show.
/// </summary>
public sealed class HyperlinkAction
{
    /// <summary>The URL target of the hyperlink. <see langword="null"/> if the action navigates to a slide.</summary>
    public string? Url { get; set; }

    /// <summary>
    /// The one-based slide number to navigate to when <see cref="Url"/> is <see langword="null"/>.
    /// </summary>
    public int? TargetSlideNumber { get; set; }

    /// <summary>
    /// <see langword="true"/> when the link should open in a new browser window or process.
    /// </summary>
    public bool OpenInNewWindow { get; set; }

    /// <summary>
    /// The optional tooltip shown when hovering over the link during a slide show.
    /// </summary>
    public string? Tooltip { get; set; }

    /// <summary>Creates a hyperlink action that opens the given URL.</summary>
    public static HyperlinkAction ToUrl(string url, bool openInNewWindow = false) =>
        new() { Url = url, OpenInNewWindow = openInNewWindow };

    /// <summary>Creates a hyperlink action that navigates to the given slide number.</summary>
    public static HyperlinkAction ToSlide(int slideNumber) =>
        new() { TargetSlideNumber = slideNumber };

    // ── Round-trip plumbing (internal) ────────────────────────────────────────

    /// <summary>The OPC relationship ID backing this hyperlink. Assigned on parse or on write.</summary>
    internal string RelationshipId { get; set; } = string.Empty;

    /// <summary>
    /// For internal slide jumps: the resolved target slide part URI (e.g.
    /// <c>/ppt/slides/slide3.xml</c>), captured at parse time and turned into
    /// <see cref="TargetSlideNumber"/> in a post-pass once all slides are known.
    /// </summary>
    internal string? TargetSlidePartUri { get; set; }
}
