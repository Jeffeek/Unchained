namespace Unchained.Pptx.Media;

/// <summary>
/// A font embedded in the presentation (an entry under <c>&lt;p:embeddedFontLst&gt;</c>,
/// stored as a <c>/ppt/fonts/*.fntdata</c> part). Carries the raw TrueType/OpenType bytes
/// so the renderer can rasterize text in the original typeface instead of a substitute.
/// </summary>
public sealed class EmbeddedFont
{
    /// <summary>The typeface name as referenced by runs (e.g. <c>"Georgia Pro Light"</c>).</summary>
    public string Typeface { get; init; } = string.Empty;

    /// <summary>The style variant this font data represents.</summary>
    public EmbeddedFontStyle Style { get; init; } = EmbeddedFontStyle.Regular;

    /// <summary>The raw font program bytes (TrueType/OpenType).</summary>
    public ReadOnlyMemory<byte> Data { get; init; }
}

/// <summary>The style variant of an <see cref="EmbeddedFont"/>.</summary>
public enum EmbeddedFontStyle
{
    /// <summary>Regular (upright, normal weight).</summary>
    Regular,

    /// <summary>Bold weight.</summary>
    Bold,

    /// <summary>Italic / oblique.</summary>
    Italic,

    /// <summary>Bold italic.</summary>
    BoldItalic
}
