namespace Unchained.Pptx.Shapes;

/// <summary>
/// A hyperlink action attached to a shape or text run.
/// Describes the navigation target (URL, slide, or other) that activates when the
/// user clicks or hovers over the object during a slide show.
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

    /// <summary>Creates a hyperlink action that opens the given URL.</summary>
    public static HyperlinkAction ToUrl(string url, bool openInNewWindow = false) =>
        new() { Url = url, OpenInNewWindow = openInNewWindow };

    /// <summary>Creates a hyperlink action that navigates to the given slide number.</summary>
    public static HyperlinkAction ToSlide(int slideNumber) =>
        new() { TargetSlideNumber = slideNumber };
}
