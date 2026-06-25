using System.Xml.Linq;
using Unchained.Ooxml.Charts;

namespace Unchained.Pptx.Parsing;

/// <summary>
///     Parses a <c>&lt;c:chartSpace&gt;</c> XML root into a <see cref="ChartModel" />.
///     Delegates to the shared <see cref="ChartXmlReader" /> in <c>Unchained.Ooxml</c>, passing the
///     PPTX <see cref="FillParser" /> as the per-series fill hook.
/// </summary>
internal static class ChartParser
{
    /// <summary>
    ///     Populates <paramref name="model" /> from the <c>&lt;c:chartSpace&gt;</c> root element.
    ///     Fields not present in the XML are left at their default values.
    /// </summary>
    public static void Parse(XElement chartSpaceRoot, ChartModel model) =>
        ChartXmlReader.Parse(chartSpaceRoot, model, static (spPr, fill) => FillParser.Parse(spPr, fill));
}
