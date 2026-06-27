using Unchained.Ooxml.Charts;

namespace Unchained.Pptx.Writing;

/// <summary>
///     Serializes a <see cref="ChartModel" /> to the bytes of a <c>chart.xml</c> OPC part.
///     Delegates to the shared <see cref="ChartXmlWriter" /> in <c>Unchained.Ooxml</c> (the chart XML
///     is identical across formats), passing the PPTX <see cref="FillWriter" /> as the per-series fill
///     hook so series-level <c>c:spPr</c> fills are written.
/// </summary>
internal static class ChartWriter
{
    /// <summary>Writes <paramref name="model" /> and returns the UTF-8 encoded chart XML bytes.</summary>
    public static byte[] Write(ChartModel model) =>
        ChartXmlWriter.Write(model, static (spPr, fill) => FillWriter.Write(spPr, fill));
}
