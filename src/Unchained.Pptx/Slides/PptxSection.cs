namespace Unchained.Pptx.Slides;

/// <summary>
///     A named group of consecutive slides within a presentation.
///     Sections are PowerPoint 2010+ metadata; they do not affect slide order or layout.
/// </summary>
public sealed class PptxSection
{
    internal PptxSection(string name) => Name = name;

    /// <summary>The display name of the section.</summary>
    public string Name { get; set; }

    /// <summary>
    ///     The slide IDs (<see cref="Slide.SlideId" />) assigned to this section,
    ///     in their presentation order.
    /// </summary>
    public List<uint> SlideIds { get; } = [];
}
