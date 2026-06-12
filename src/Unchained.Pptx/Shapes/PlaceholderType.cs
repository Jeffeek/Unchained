namespace Unchained.Pptx.Shapes;

/// <summary>
///     The placeholder type of a shape (<c>p:ph/@type</c>). Placeholders inherit position, size, and
///     default formatting from the matching placeholder on the slide layout (and in turn the master).
/// </summary>
public enum PlaceholderType
{
    /// <summary>The shape is not a placeholder.</summary>
    None,

    /// <summary>Title placeholder (<c>title</c>).</summary>
    Title,

    /// <summary>Centred title placeholder, used on title slides (<c>ctrTitle</c>).</summary>
    CenteredTitle,

    /// <summary>Subtitle placeholder (<c>subTitle</c>).</summary>
    Subtitle,

    /// <summary>Body placeholder (<c>body</c>).</summary>
    Body,

    /// <summary>Generic content placeholder (no explicit <c>type</c>; the default).</summary>
    Content,

    /// <summary>Centered text / object placeholder.</summary>
    Object,

    /// <summary>Date placeholder (<c>dt</c>).</summary>
    Date,

    /// <summary>Footer placeholder (<c>ftr</c>).</summary>
    Footer,

    /// <summary>Slide-number placeholder (<c>sldNum</c>).</summary>
    SlideNumber,

    /// <summary>Header placeholder (<c>hdr</c>, notes/handout).</summary>
    Header,

    /// <summary>Chart, table, picture, media, or other typed placeholder.</summary>
    Media
}
