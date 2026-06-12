using Unchained.Pptx.Themes;
using Unchained.Pptx.Shapes;

namespace Unchained.Pptx.Slides;

/// <summary>
/// A slide master — the top-level template that defines the default theme, background,
/// and layout set for all slides that use it.
/// </summary>
public sealed class MasterSlide
{
    /// <summary>The display name of the master (e.g. "Office Theme").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The layouts defined for this master.</summary>
    public SlideLayoutCollection Layouts { get; }

    /// <summary>Initialises a new master with an empty, owner-linked layout collection.</summary>
    public MasterSlide()
    {
        Layouts = new SlideLayoutCollection { Owner = this };
    }

    /// <summary>Shapes placed on this master (logos, backgrounds, decorative elements).</summary>
    public ShapeCollection Shapes { get; } = new();

    /// <summary>The background applied to this master.</summary>
    public SlideBackground Background { get; } = new();

    /// <summary>The theme (colours, fonts, effects) applied to this master.</summary>
    public PptxTheme Theme { get; set; } = new();

    /// <summary>
    /// The OPC part URI of this master (e.g. <c>/ppt/slideMasters/slideMaster1.xml</c>).
    /// Used internally by the writer.
    /// </summary>
    internal string PartUri { get; set; } = string.Empty;

    /// <summary>The relationship ID of this master within the presentation relationships.</summary>
    internal string RelationshipId { get; set; } = string.Empty;

    /// <summary>Raw XML preserved for elements not yet modelled (round-trip safety).</summary>
    internal System.Xml.Linq.XElement? RawElement { get; set; }
}
