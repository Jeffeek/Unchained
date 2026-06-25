using System.Xml.Linq;
using Unchained.Ooxml.Charts;

namespace Unchained.Xlsx.Parsing;

/// <summary>
///     Reads and writes the chart part (<c>xl/charts/chart*.xml</c>) for a worksheet drawing.
///     Delegates to the shared <see cref="ChartXmlReader" /> / <see cref="ChartXmlWriter" /> in
///     <c>Unchained.Ooxml</c> — the <c>c:chartSpace</c> XML is identical across formats. XLSX charts
///     do not carry per-series DrawingML fills, so no fill hook is supplied.
/// </summary>
internal static class ChartXml
{
    /// <summary>Parses a chart-space root into a <see cref="ChartModel" />; returns an empty model when null.</summary>
    public static ChartModel Parse(XElement? chartSpace) =>
        chartSpace is null ? new ChartModel() : ChartXmlReader.Parse(chartSpace);

    /// <summary>Serializes <paramref name="model" /> to chart XML bytes.</summary>
    public static byte[] Write(ChartModel model) => ChartXmlWriter.Write(model);
}
