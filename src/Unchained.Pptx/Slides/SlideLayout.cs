using System.Xml.Linq;
using Unchained.Pptx.Models.Themes;
using Unchained.Pptx.Shapes;

namespace Unchained.Pptx.Slides;

/// <summary>
///     A slide layout — a template slide that defines the arrangement of placeholders
///     and default formatting for slides that use it.
///     Each layout belongs to exactly one <see cref="MasterSlide" />.
/// </summary>
public sealed class SlideLayout
{
    /// <summary>The display name of the layout (e.g. "Title Slide", "Title and Content").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The layout type that identifies the layout's intended purpose.</summary>
    public LayoutType LayoutType { get; set; } = LayoutType.Custom;

    /// <summary>The master slide this layout belongs to.</summary>
    public MasterSlide Master { get; internal set; } = null!;

    /// <summary>Shapes defined on this layout (placeholders and decorative shapes).</summary>
    public ShapeCollection Shapes { get; } = new();

    /// <summary>The background applied to this layout.</summary>
    public SlideBackground Background { get; } = new();

    /// <summary>
    ///     The OPC part URI of this layout (e.g. <c>/ppt/slideLayouts/slideLayout1.xml</c>).
    ///     Used internally by the writer.
    /// </summary>
    internal string PartUri { get; set; } = string.Empty;

    /// <summary>
    ///     The relationship ID of this layout within its master's relationships.
    /// </summary>
    internal string RelationshipId { get; set; } = string.Empty;

    /// <summary>Raw XML preserved for elements not yet modelled (round-trip safety).</summary>
    internal XElement? RawElement { get; set; }
}
