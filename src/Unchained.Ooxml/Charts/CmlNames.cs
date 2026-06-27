using System.Xml.Linq;
using Unchained.Ooxml.Xml;

namespace Unchained.Ooxml.Charts;

/// <summary>
///     DrawingML Chart (<c>c:</c>) namespace constants and the element / attribute names used when
///     reading and writing <c>c:chartSpace</c> XML. Shared by every format that embeds charts
///     (PPTX, XLSX), since the chart part XML is identical across them (ECMA-376 §21.2).
/// </summary>
public static class CmlNames
{
    /// <summary>Data point index attribute: <c>idx</c>.</summary>
    public const string AttributeIndex = "idx";

    /// <summary>The DrawingML Chart main namespace.</summary>
    public static readonly XNamespace Cml = OoxmlNamespaces.Chart;

    // ── Structure ──────────────────────────────────────────────────────────────
    public static readonly XName ChartSpace = Cml + "chartSpace";
    public static readonly XName Language = Cml + "lang";
    public static readonly XName Chart = Cml + "chart";
    public static readonly XName Title = Cml + "title";
    public static readonly XName Text = Cml + "tx";
    public static readonly XName Rich = Cml + "rich";
    public static readonly XName Overlay = Cml + "overlay";
    public static readonly XName AutoTitleDeleted = Cml + "autoTitleDeleted";
    public static readonly XName PlotArea = Cml + "plotArea";
    public static readonly XName Layout = Cml + "layout";
    public static readonly XName PlotVisibleOnly = Cml + "plotVisOnly";

    // ── Chart-type elements ──────────────────────────────────────────────────
    public static readonly XName BarChart = Cml + "barChart";
    public static readonly XName LineChart = Cml + "lineChart";
    public static readonly XName PieChart = Cml + "pieChart";
    public static readonly XName DoughnutChart = Cml + "doughnutChart";
    public static readonly XName AreaChart = Cml + "areaChart";
    public static readonly XName ScatterChart = Cml + "scatterChart";
    public static readonly XName BubbleChart = Cml + "bubbleChart";
    public static readonly XName RadarChart = Cml + "radarChart";
    public static readonly XName StockChart = Cml + "stockChart";
    public static readonly XName SurfaceChart = Cml + "surfaceChart";

    public static readonly XName BarDirection = Cml + "barDir";
    public static readonly XName Grouping = Cml + "grouping";
    public static readonly XName RadarStyle = Cml + "radarStyle";
    public static readonly XName ScatterStyle = Cml + "scatterStyle";
    public static readonly XName VaryColors = Cml + "varyColors";
    public static readonly XName HoleSize = Cml + "holeSize";

    // ── Series & data ──────────────────────────────────────────────────────────
    public static readonly XName Series = Cml + "ser";
    public static readonly XName Index = Cml + "idx";
    public static readonly XName Order = Cml + "order";
    public static readonly XName Marker = Cml + "marker";
    public static readonly XName MarkerSymbol = Cml + "symbol";
    public static readonly XName Category = Cml + "cat";
    public static readonly XName Values = Cml + DmlNames.AttributeValue;
    public static readonly XName XValues = Cml + "xVal";
    public static readonly XName YValues = Cml + "yVal";
    public static readonly XName StringLiteral = Cml + "strLit";
    public static readonly XName NumberLiteral = Cml + "numLit";
    public static readonly XName PointCount = Cml + "ptCount";
    public static readonly XName Point = Cml + "pt";
    public static readonly XName PointValue = Cml + "v";
    public static readonly XName FormatCode = Cml + "formatCode";
    public static readonly XName StringReference = Cml + "strRef";
    public static readonly XName NumberReference = Cml + "numRef";
    public static readonly XName StringCache = Cml + "strCache";
    public static readonly XName NumberCache = Cml + "numCache";

    // ── Axes ───────────────────────────────────────────────────────────────────
    public static readonly XName CategoryAxis = Cml + "catAx";
    public static readonly XName ValueAxis = Cml + "valAx";
    public static readonly XName DateAxis = Cml + "dateAx";
    public static readonly XName SeriesAxis = Cml + "serAx";
    public static readonly XName AxisId = Cml + "axId";
    public static readonly XName Scaling = Cml + "scaling";
    public static readonly XName Orientation = Cml + "orientation";
    public static readonly XName Delete = Cml + "delete";
    public static readonly XName AxisPosition = Cml + "axPos";
    public static readonly XName CrossAxis = Cml + "crossAx";

    // ── Legend ─────────────────────────────────────────────────────────────────
    public static readonly XName Legend = Cml + "legend";
    public static readonly XName LegendPosition = Cml + "legendPos";

    /// <summary>Print settings element (<c>c:printSettings</c>).</summary>
    public static readonly XName PrintSettings = Cml + "printSettings";
}
