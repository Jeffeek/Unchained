namespace Unchained.Ooxml.Text;

/// <summary>
/// A hyperlink applied to a text run (the <c>&lt;a:hlinkClick&gt;</c> inside a run's properties).
/// This is a format-agnostic model carried by <see cref="RunFormat"/>; the host format
/// (PPTX, DOCX, …) is responsible for resolving the relationship id to a concrete target on load
/// and assigning one on save.
/// </summary>
public sealed class RunHyperlink
{
    /// <summary>The external URL target, or <see langword="null"/> for an internal jump.</summary>
    public string? Url { get; set; }

    /// <summary>
    /// For internal navigation (e.g. a jump to another slide): the 1-based slide number, or
    /// <see langword="null"/> when this is an external URL link.
    /// </summary>
    public int? TargetSlideNumber { get; set; }

    /// <summary>Tooltip shown when hovering the link.</summary>
    public string? Tooltip { get; set; }

    /// <summary>
    /// The OPC relationship id backing this link. Captured on load and assigned on save by the
    /// host format. Internal to the round-trip plumbing.
    /// </summary>
    public string RelationshipId { get; set; } = string.Empty;

    /// <summary>
    /// For internal jumps: the resolved target part URI captured at load time, mapped to
    /// <see cref="TargetSlideNumber"/> once all slides are known. Internal plumbing.
    /// </summary>
    public string? TargetPartUri { get; set; }

    /// <summary>Creates a run hyperlink that opens the given URL.</summary>
    public static RunHyperlink ToUrl(string url) => new() { Url = url };

    /// <summary>Creates a run hyperlink that navigates to the given 1-based slide number.</summary>
    public static RunHyperlink ToSlide(int slideNumber) => new() { TargetSlideNumber = slideNumber };
}
