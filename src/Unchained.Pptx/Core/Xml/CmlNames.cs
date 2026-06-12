using System.Xml.Linq;

namespace Unchained.Pptx.Core.Xml;

/// <summary>
///     DrawingML Chart namespace constants and commonly-used element/attribute names.
///     All values are taken directly from ECMA-376 5th Edition §21.2.
/// </summary>
internal static class CmlNames
{
    // ── Attributes ────────────────────────────────────────────────────────────

    /// <summary>Generic value attribute: <c>val</c></summary>
    public const string AttributeValue = "val";

    /// <summary>Data point index attribute: <c>idx</c></summary>
    public const string AttributeIndex = "idx";
    // ── Namespace ────────────────────────────────────────────────────────────

    /// <summary>DrawingML Chart main namespace.</summary>
    public static readonly XNamespace Cml =
        "http://schemas.openxmlformats.org/drawingml/2006/chart";

    // ── Chart space / chart root ──────────────────────────────────────────────

    /// <summary><c>&lt;c:chartSpace&gt;</c> — root element of a chart part.</summary>
    public static readonly XName ChartSpace = Cml + "chartSpace";

    /// <summary><c>&lt;c:lang&gt;</c> — language of data in the chart.</summary>
    public static readonly XName Language = Cml + "lang";

    /// <summary><c>&lt;c:chart&gt;</c> — chart reference inside a graphic frame.</summary>
    public static readonly XName Chart = Cml + "chart";

    // ── Title ─────────────────────────────────────────────────────────────────

    /// <summary><c>&lt;c:title&gt;</c> — chart title.</summary>
    public static readonly XName Title = Cml + "title";

    /// <summary><c>&lt;c:tx&gt;</c> — text for a title or series name.</summary>
    public static readonly XName Text = Cml + "tx";

    /// <summary><c>&lt;c:rich&gt;</c> — rich-text body inside a chart title.</summary>
    public static readonly XName Rich = Cml + "rich";

    /// <summary><c>&lt;c:overlay&gt;</c> — whether a title overlays the plot area.</summary>
    public static readonly XName Overlay = Cml + "overlay";

    /// <summary><c>&lt;c:autoTitleDeleted&gt;</c> — auto-title suppressed flag.</summary>
    public static readonly XName AutoTitleDeleted = Cml + "autoTitleDeleted";

    // ── Plot area ─────────────────────────────────────────────────────────────

    /// <summary><c>&lt;c:plotArea&gt;</c> — container for chart types and axes.</summary>
    public static readonly XName PlotArea = Cml + "plotArea";

    /// <summary><c>&lt;c:layout&gt;</c> — manual or auto layout of the plot area.</summary>
    public static readonly XName Layout = Cml + "layout";

    // ── Chart type elements ───────────────────────────────────────────────────

    /// <summary><c>&lt;c:barChart&gt;</c> — bar or column chart.</summary>
    public static readonly XName BarChart = Cml + "barChart";

    /// <summary><c>&lt;c:lineChart&gt;</c> — line chart.</summary>
    public static readonly XName LineChart = Cml + "lineChart";

    /// <summary><c>&lt;c:pieChart&gt;</c> — pie chart.</summary>
    public static readonly XName PieChart = Cml + "pieChart";

    /// <summary><c>&lt;c:doughnutChart&gt;</c> — doughnut chart.</summary>
    public static readonly XName DoughnutChart = Cml + "doughnutChart";

    /// <summary><c>&lt;c:areaChart&gt;</c> — area chart.</summary>
    public static readonly XName AreaChart = Cml + "areaChart";

    /// <summary><c>&lt;c:scatterChart&gt;</c> — scatter chart.</summary>
    public static readonly XName ScatterChart = Cml + "scatterChart";

    /// <summary><c>&lt;c:bubbleChart&gt;</c> — bubble chart.</summary>
    public static readonly XName BubbleChart = Cml + "bubbleChart";

    /// <summary><c>&lt;c:radarChart&gt;</c> — radar / spider chart.</summary>
    public static readonly XName RadarChart = Cml + "radarChart";

    /// <summary><c>&lt;c:stockChart&gt;</c> — stock (OHLC) chart.</summary>
    public static readonly XName StockChart = Cml + "stockChart";

    /// <summary><c>&lt;c:surfaceChart&gt;</c> — surface chart.</summary>
    public static readonly XName SurfaceChart = Cml + "surfaceChart";

    // ── Bar/column specifics ──────────────────────────────────────────────────

    /// <summary><c>&lt;c:barDir&gt;</c> — bar direction: <c>bar</c> (horizontal) or <c>col</c> (vertical).</summary>
    public static readonly XName BarDirection = Cml + "barDir";

    /// <summary><c>&lt;c:grouping&gt;</c> — grouping style: clustered / stacked / percentStacked / standard.</summary>
    public static readonly XName Grouping = Cml + "grouping";

    // ── Radar specifics ───────────────────────────────────────────────────────

    /// <summary><c>&lt;c:radarStyle&gt;</c> — radar style: standard, marker, filled.</summary>
    public static readonly XName RadarStyle = Cml + "radarStyle";

    // ── Scatter specifics ─────────────────────────────────────────────────────

    /// <summary><c>&lt;c:scatterStyle&gt;</c> — scatter style: line, lineMarker, marker, etc.</summary>
    public static readonly XName ScatterStyle = Cml + "scatterStyle";

    // ── Common chart elements ─────────────────────────────────────────────────

    /// <summary><c>&lt;c:varyColors&gt;</c> — vary series colours by point.</summary>
    public static readonly XName VaryColors = Cml + "varyColors";

    /// <summary><c>&lt;c:holeSize&gt;</c> — doughnut hole size as percentage.</summary>
    public static readonly XName HoleSize = Cml + "holeSize";

    // ── Series ────────────────────────────────────────────────────────────────

    /// <summary><c>&lt;c:ser&gt;</c> — one data series.</summary>
    public static readonly XName Series = Cml + "ser";

    /// <summary><c>&lt;c:idx&gt;</c> — zero-based series index.</summary>
    public static readonly XName Index = Cml + "idx";

    /// <summary><c>&lt;c:order&gt;</c> — display order of the series.</summary>
    public static readonly XName Order = Cml + "order";

    /// <summary><c>&lt;c:marker&gt;</c> — data point marker settings.</summary>
    public static readonly XName Marker = Cml + "marker";

    /// <summary><c>&lt;c:symbol&gt;</c> — marker symbol type.</summary>
    public static readonly XName MarkerSymbol = Cml + "symbol";

    // ── Series data arrays ────────────────────────────────────────────────────

    /// <summary><c>&lt;c:cat&gt;</c> — category (label) data for bar/line/area charts.</summary>
    public static readonly XName Category = Cml + "cat";

    /// <summary><c>&lt;c:val&gt;</c> — numeric value data.</summary>
    public static readonly XName Values = Cml + "val";

    /// <summary><c>&lt;c:xVal&gt;</c> — X values for scatter/bubble charts.</summary>
    public static readonly XName XValues = Cml + "xVal";

    /// <summary><c>&lt;c:yVal&gt;</c> — Y values for scatter/bubble charts.</summary>
    public static readonly XName YValues = Cml + "yVal";

    // ── Literal data (no workbook link) ───────────────────────────────────────

    /// <summary><c>&lt;c:strLit&gt;</c> — inline string literal array.</summary>
    public static readonly XName StringLiteral = Cml + "strLit";

    /// <summary><c>&lt;c:numLit&gt;</c> — inline numeric literal array.</summary>
    public static readonly XName NumberLiteral = Cml + "numLit";

    /// <summary><c>&lt;c:ptCount&gt;</c> — number of data points in a literal array.</summary>
    public static readonly XName PointCount = Cml + "ptCount";

    /// <summary><c>&lt;c:pt&gt;</c> — one data point.</summary>
    public static readonly XName Point = Cml + "pt";

    /// <summary><c>&lt;c:v&gt;</c> — value inside a data point.</summary>
    public static readonly XName PointValue = Cml + "v";

    /// <summary><c>&lt;c:formatCode&gt;</c> — number format code (e.g. "General").</summary>
    public static readonly XName FormatCode = Cml + "formatCode";

    // ── Workbook-linked data ──────────────────────────────────────────────────

    /// <summary><c>&lt;c:strRef&gt;</c> — string reference to a worksheet range.</summary>
    public static readonly XName StringReference = Cml + "strRef";

    /// <summary><c>&lt;c:numRef&gt;</c> — numeric reference to a worksheet range.</summary>
    public static readonly XName NumberReference = Cml + "numRef";

    /// <summary><c>&lt;c:f&gt;</c> — worksheet formula (e.g. "Sheet1!$A$2:$A$5").</summary>
    public static readonly XName Formula = Cml + "f";

    /// <summary><c>&lt;c:strCache&gt;</c> — cached string values from a worksheet.</summary>
    public static readonly XName StringCache = Cml + "strCache";

    /// <summary><c>&lt;c:numCache&gt;</c> — cached numeric values from a worksheet.</summary>
    public static readonly XName NumberCache = Cml + "numCache";

    // ── Axes ─────────────────────────────────────────────────────────────────

    /// <summary><c>&lt;c:catAx&gt;</c> — category axis.</summary>
    public static readonly XName CategoryAxis = Cml + "catAx";

    /// <summary><c>&lt;c:valAx&gt;</c> — value axis.</summary>
    public static readonly XName ValueAxis = Cml + "valAx";

    /// <summary><c>&lt;c:dateAx&gt;</c> — date axis.</summary>
    public static readonly XName DateAxis = Cml + "dateAx";

    /// <summary><c>&lt;c:serAx&gt;</c> — series axis (for 3-D charts).</summary>
    public static readonly XName SeriesAxis = Cml + "serAx";

    /// <summary><c>&lt;c:axId&gt;</c> — axis identifier reference from chart to axis.</summary>
    public static readonly XName AxisId = Cml + "axId";

    /// <summary><c>&lt;c:scaling&gt;</c> — axis scaling settings.</summary>
    public static readonly XName Scaling = Cml + "scaling";

    /// <summary><c>&lt;c:orientation&gt;</c> — axis orientation (minMax / maxMin).</summary>
    public static readonly XName Orientation = Cml + "orientation";

    /// <summary><c>&lt;c:delete&gt;</c> — whether the axis is hidden.</summary>
    public static readonly XName Delete = Cml + "delete";

    /// <summary><c>&lt;c:axPos&gt;</c> — axis position: b / l / r / t.</summary>
    public static readonly XName AxisPosition = Cml + "axPos";

    /// <summary><c>&lt;c:crossAx&gt;</c> — ID of the perpendicular axis this one crosses.</summary>
    public static readonly XName CrossAxis = Cml + "crossAx";

    // ── Legend ────────────────────────────────────────────────────────────────

    /// <summary><c>&lt;c:legend&gt;</c> — chart legend.</summary>
    public static readonly XName Legend = Cml + "legend";

    /// <summary><c>&lt;c:legendPos&gt;</c> — legend position: b / t / l / r / tr.</summary>
    public static readonly XName LegendPosition = Cml + "legendPos";

    // ── Miscellaneous ─────────────────────────────────────────────────────────

    /// <summary><c>&lt;c:plotVisOnly&gt;</c> — plot only visible cells.</summary>
    public static readonly XName PlotVisibleOnly = Cml + "plotVisOnly";

    /// <summary><c>&lt;c:printSettings&gt;</c> — print settings block.</summary>
    public static readonly XName PrintSettings = Cml + "printSettings";
}
