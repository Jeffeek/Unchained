namespace Unchained.Pptx.Shapes;

/// <summary>
/// A shape that contains a SmartArt diagram. The diagram data is preserved verbatim;
/// rendering and editing SmartArt content is not supported in M1–M4.
/// </summary>
public sealed class SmartArtShape : Shape
{
    /// <summary>
    /// The raw SmartArt diagram XML element, preserved from the source file so that
    /// round-trips do not alter the diagram data.
    /// </summary>
    internal System.Xml.Linq.XElement? DiagramData { get; set; }
}
