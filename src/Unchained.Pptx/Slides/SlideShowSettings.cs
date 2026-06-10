namespace Unchained.Pptx.Slides;

/// <summary>
/// The kind of slide show — how the presentation is delivered to an audience.
/// Maps to the <c>p:showPr</c> child element in <c>presProps.xml</c>.
/// </summary>
public enum SlideShowType
{
    /// <summary>Full-screen, presenter-driven show (<c>&lt;p:present/&gt;</c>). The default.</summary>
    Presenter,

    /// <summary>Windowed, individual-browsed show (<c>&lt;p:browse/&gt;</c>).</summary>
    Browsed,

    /// <summary>Full-screen kiosk show (<c>&lt;p:kiosk/&gt;</c>).</summary>
    Kiosk,
}

/// <summary>
/// Presentation-level slide-show settings, persisted in the <c>presProps.xml</c> part
/// (<c>p:presentationPr/p:showPr</c>). Controls how the deck plays back: loop, narration,
/// animation, show type, and an optional slide range.
/// </summary>
public sealed class SlideShowSettings
{
    /// <summary>The slide-show delivery type.</summary>
    public SlideShowType ShowType { get; set; } = SlideShowType.Presenter;

    /// <summary><see langword="true"/> to loop continuously until "Esc" (<c>loop="1"</c>).</summary>
    public bool Loop { get; set; }

    /// <summary><see langword="true"/> to play without narration (<c>showNarration="0"</c> inverse).</summary>
    public bool ShowWithoutNarration { get; set; }

    /// <summary><see langword="true"/> to play without animation (<c>showAnimation="0"</c> inverse).</summary>
    public bool ShowWithoutAnimation { get; set; }

    /// <summary>
    /// First slide (1-based) of the show range, or <see langword="null"/> to start at slide 1.
    /// When set together with <see cref="RangeEnd"/>, written as <c>&lt;p:sldRg&gt;</c>.
    /// </summary>
    public int? RangeStart { get; set; }

    /// <summary>Last slide (1-based) of the show range, or <see langword="null"/> for "to the end".</summary>
    public int? RangeEnd { get; set; }

    /// <summary>The pen colour used for on-slide annotations, as an <c>RRGGBB</c> hex string, or null.</summary>
    public string? PenColorHex { get; set; }

    /// <summary>
    /// Whether any non-default slide-show setting has been assigned. When <see langword="false"/>
    /// the writer omits the <c>p:showPr</c> element entirely.
    /// </summary>
    internal bool HasAnySetting =>
        ShowType != SlideShowType.Presenter
        || Loop
        || ShowWithoutNarration
        || ShowWithoutAnimation
        || RangeStart.HasValue
        || RangeEnd.HasValue
        || !string.IsNullOrEmpty(PenColorHex);
}
