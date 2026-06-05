namespace Unchained.Pptx.Models.Themes;

/// <summary>
/// Named layout types for slide layouts (ECMA-376 §19.7.15 <c>ST_SlideLayoutType</c>).
/// These identify the intended purpose of a layout within a presentation.
/// </summary>
public enum LayoutType
{
    /// <summary>A completely blank layout with no placeholders.</summary>
    Blank,
    /// <summary>A title-only layout (single title placeholder).</summary>
    Title,
    /// <summary>A layout with a title and a single content area.</summary>
    TitleAndContent,
    /// <summary>A layout with a title and two side-by-side content areas.</summary>
    TitleAndTwoContent,
    /// <summary>A layout with a title, one main content area, and two smaller areas.</summary>
    TitleAndTwoContentPlusContent,
    /// <summary>A layout with four equal content areas.</summary>
    FourContent,
    /// <summary>A centred title and subtitle (typical first slide).</summary>
    TitleSlide,
    /// <summary>A title-only layout (no content area).</summary>
    TitleOnly,
    /// <summary>A section divider layout.</summary>
    SectionHeader,
    /// <summary>A layout with two text columns.</summary>
    TwoTextColumns,
    /// <summary>A layout with a title above vertically-oriented text.</summary>
    TitleAndVerticalText,
    /// <summary>A layout for a picture with a caption below it.</summary>
    PictureWithCaption,
    /// <summary>A full-width, blank layout intended for custom use.</summary>
    Custom
}
