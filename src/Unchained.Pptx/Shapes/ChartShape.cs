namespace Unchained.Pptx.Shapes;

/// <summary>
/// A shape that contains a chart (bar, column, pie, line, etc.).
/// Chart data editing is not supported in M1–M4; the chart XML is preserved verbatim
/// so that round-trips do not alter the chart.
/// </summary>
public sealed class ChartShape : Shape
{
    /// <summary>
    /// The raw chart XML part bytes, preserved from the source file.
    /// Written back unchanged during save.
    /// </summary>
    internal byte[]? ChartPartData { get; set; }

    /// <summary>
    /// The OPC relationship ID used to reference the chart part from the slide.
    /// </summary>
    internal string RelationshipId { get; set; } = string.Empty;

    /// <summary>
    /// The OPC part URI of the chart (e.g. <c>/ppt/charts/chart1.xml</c>).
    /// </summary>
    internal string PartUri { get; set; } = string.Empty;
}
