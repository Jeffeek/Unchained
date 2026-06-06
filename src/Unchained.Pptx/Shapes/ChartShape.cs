using Unchained.Pptx.Charts;

namespace Unchained.Pptx.Shapes;

/// <summary>
/// A shape that contains an embedded chart (bar, column, pie, line, etc.).
/// </summary>
public sealed class ChartShape : Shape
{
    /// <summary>
    /// The chart model — type, title, data, and legend. Read and modify this to
    /// inspect or change the chart. When saving, the chart XML is regenerated from
    /// this model for programmatically-created charts; raw bytes are preserved for
    /// charts loaded from an existing PPTX file (see <see cref="ChartPartData"/>).
    /// </summary>
    public ChartModel Chart { get; internal set; } = new();

    /// <summary>
    /// The raw bytes of the <c>chart.xml</c> OPC part, preserved verbatim when the
    /// chart was loaded from a PPTX file. <see langword="null"/> for charts created
    /// programmatically via <c>ShapeCollection.AddChart</c>.
    /// </summary>
    internal byte[]? ChartPartData { get; set; }

    /// <summary>
    /// The OPC relationship ID that references this chart part from the slide.
    /// Assigned during parsing (from the file) or during the write step (for new charts).
    /// </summary>
    internal string RelationshipId { get; set; } = string.Empty;

    /// <summary>
    /// The absolute OPC part URI of this chart (e.g. <c>/ppt/charts/chart1.xml</c>).
    /// Assigned during parsing or during the write step.
    /// </summary>
    internal string PartUri { get; set; } = string.Empty;
}
