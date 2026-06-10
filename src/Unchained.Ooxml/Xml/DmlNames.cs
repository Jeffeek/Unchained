using System.Xml.Linq;

namespace Unchained.Ooxml.Xml;

/// <summary>
/// DrawingML XML namespace constants and commonly-used element/attribute names.
/// All values are taken directly from ECMA-376 5th Edition.
/// </summary>
internal static class DmlNames
{
    // ── Namespaces ───────────────────────────────────────────────────────────

    /// <summary>DrawingML main namespace: <c>http://schemas.openxmlformats.org/drawingml/2006/main</c></summary>
    public static readonly XNamespace Dml = "http://schemas.openxmlformats.org/drawingml/2006/main";

    /// <summary>DrawingML chart namespace.</summary>
    public static readonly XNamespace Chart = "http://schemas.openxmlformats.org/drawingml/2006/chart";

    /// <summary>DrawingML table namespace URI (used as <c>uri</c> attribute on <c>&lt;a:graphicData&gt;</c>).</summary>
    public const string GraphicDataTableUri = "http://schemas.openxmlformats.org/drawingml/2006/table";

    /// <summary>DrawingML chart namespace URI (used as <c>uri</c> attribute on <c>&lt;a:graphicData&gt;</c>).</summary>
    public const string GraphicDataChartUri = "http://schemas.openxmlformats.org/drawingml/2006/chart";

    /// <summary>DrawingML diagram (SmartArt) namespace URI (used as <c>uri</c> on <c>&lt;a:graphicData&gt;</c>).</summary>
    public const string GraphicDataDiagramUri = "http://schemas.openxmlformats.org/drawingml/2006/diagram";

    /// <summary>SmartArt diagram namespace (<c>dgm:</c>).</summary>
    public static readonly XNamespace Diagram = "http://schemas.openxmlformats.org/drawingml/2006/diagram";

    /// <summary><c>&lt;dgm:relIds&gt;</c> — references the four SmartArt parts from the graphic frame.</summary>
    public static readonly XName DiagramRelIds = Diagram + "relIds";

    /// <summary><c>&lt;dgm:dataModel&gt;</c> — root of the SmartArt data part (<c>data*.xml</c>).</summary>
    public static readonly XName DiagramDataModel = Diagram + "dataModel";

    /// <summary><c>&lt;dgm:ptLst&gt;</c> — the list of data points (nodes) in the diagram.</summary>
    public static readonly XName DiagramPointList = Diagram + "ptLst";

    /// <summary><c>&lt;dgm:pt&gt;</c> — a single data point (node) in the diagram.</summary>
    public static readonly XName DiagramPoint = Diagram + "pt";

    /// <summary><c>&lt;dgm:t&gt;</c> — the text body of a diagram node (contains <c>a:p</c>/<c>a:r</c>/<c>a:t</c>).</summary>
    public static readonly XName DiagramText = Diagram + "t";

    /// <summary><c>&lt;dgm:cxnLst&gt;</c> — the list of connections between data points.</summary>
    public static readonly XName DiagramConnectionList = Diagram + "cxnLst";

    /// <summary><c>&lt;dgm:cxn&gt;</c> — a single connection between data points.</summary>
    public static readonly XName DiagramConnection = Diagram + "cxn";

    // ── Transform & geometry ─────────────────────────────────────────────────

    /// <summary><c>&lt;a:xfrm&gt;</c> — 2-D transform (position + size + rotation).</summary>
    public static readonly XName Transform = Dml + "xfrm";

    /// <summary><c>&lt;a:off&gt;</c> — offset (x/y position).</summary>
    public static readonly XName Offset = Dml + "off";

    /// <summary><c>&lt;a:ext&gt;</c> — extents (cx/cy size).</summary>
    public static readonly XName Extents = Dml + "ext";

    /// <summary><c>&lt;a:chOff&gt;</c> — group child coordinate-space origin.</summary>
    public static readonly XName ChildOffset = Dml + "chOff";

    /// <summary><c>&lt;a:chExt&gt;</c> — group child coordinate-space extent.</summary>
    public static readonly XName ChildExtent = Dml + "chExt";

    /// <summary><c>&lt;a:prstGeom&gt;</c> — preset geometry.</summary>
    public static readonly XName PresetGeometry = Dml + "prstGeom";

    /// <summary><c>&lt;a:custGeom&gt;</c> — custom geometry.</summary>
    public static readonly XName CustomGeometry = Dml + "custGeom";

    /// <summary><c>&lt;a:avLst&gt;</c> — adjust value list.</summary>
    public static readonly XName AdjustValueList = Dml + "avLst";

    // ── Fill ─────────────────────────────────────────────────────────────────

    /// <summary><c>&lt;a:solidFill&gt;</c> — solid fill.</summary>
    public static readonly XName SolidFill = Dml + "solidFill";

    /// <summary><c>&lt;a:gradFill&gt;</c> — gradient fill.</summary>
    public static readonly XName GradientFill = Dml + "gradFill";

    /// <summary><c>&lt;a:pattFill&gt;</c> — pattern fill.</summary>
    public static readonly XName PatternFill = Dml + "pattFill";

    /// <summary><c>&lt;a:blipFill&gt;</c> — picture fill.</summary>
    public static readonly XName BlipFill = Dml + "blipFill";

    /// <summary><c>&lt;a:noFill&gt;</c> — no fill.</summary>
    public static readonly XName NoFill = Dml + "noFill";

    /// <summary><c>&lt;a:grpFill&gt;</c> — group fill (inherit from group).</summary>
    public static readonly XName GroupFill = Dml + "grpFill";

    /// <summary><c>&lt;a:gsLst&gt;</c> — gradient stop list.</summary>
    public static readonly XName GradientStopList = Dml + "gsLst";

    /// <summary><c>&lt;a:gs&gt;</c> — one gradient stop.</summary>
    public static readonly XName GradientStop = Dml + "gs";

    /// <summary><c>&lt;a:lin&gt;</c> — linear gradient direction.</summary>
    public static readonly XName LinearGradient = Dml + "lin";

    /// <summary><c>&lt;a:blip&gt;</c> — image reference inside a blip fill.</summary>
    public static readonly XName Blip = Dml + "blip";

    /// <summary><c>&lt;a:stretch&gt;</c> — stretch mode for a blip fill.</summary>
    public static readonly XName Stretch = Dml + "stretch";

    /// <summary><c>&lt;a:fillRect&gt;</c> — fill rectangle (full stretch).</summary>
    public static readonly XName FillRect = Dml + "fillRect";

    /// <summary><c>&lt;a:tile&gt;</c> — tile mode for a blip fill.</summary>
    public static readonly XName Tile = Dml + "tile";

    // ── Line ─────────────────────────────────────────────────────────────────

    /// <summary><c>&lt;a:ln&gt;</c> — line properties.</summary>
    public static readonly XName Line = Dml + "ln";

    /// <summary><c>&lt;a:prstDash&gt;</c> — preset dash style.</summary>
    public static readonly XName PresetDash = Dml + "prstDash";

    /// <summary><c>&lt;a:headEnd&gt;</c> — line head end properties.</summary>
    public static readonly XName HeadEnd = Dml + "headEnd";

    /// <summary><c>&lt;a:tailEnd&gt;</c> — line tail end properties.</summary>
    public static readonly XName TailEnd = Dml + "tailEnd";

    // ── Color ─────────────────────────────────────────────────────────────────

    /// <summary><c>&lt;a:srgbClr&gt;</c> — absolute sRGB colour.</summary>
    public static readonly XName SrgbColor = Dml + "srgbClr";

    /// <summary><c>&lt;a:schemeClr&gt;</c> — theme colour slot reference.</summary>
    public static readonly XName SchemeColor = Dml + "schemeClr";

    /// <summary><c>&lt;a:prstClr&gt;</c> — preset (named) colour.</summary>
    public static readonly XName PresetColor = Dml + "prstClr";

    /// <summary><c>&lt;a:sysClr&gt;</c> — system colour.</summary>
    public static readonly XName SystemColor = Dml + "sysClr";

    /// <summary><c>&lt;a:lumMod&gt;</c> — luminance modifier.</summary>
    public static readonly XName LuminanceModifier = Dml + "lumMod";

    /// <summary><c>&lt;a:lumOff&gt;</c> — luminance offset.</summary>
    public static readonly XName LuminanceOffset = Dml + "lumOff";

    /// <summary><c>&lt;a:alpha&gt;</c> — alpha (opacity) transform.</summary>
    public static readonly XName Alpha = Dml + "alpha";

    // ── Text body ─────────────────────────────────────────────────────────────

    /// <summary><c>&lt;a:txBody&gt;</c> — text body (in DrawingML context, e.g. table cells).</summary>
    public static readonly XName TextBody = Dml + "txBody";

    /// <summary><c>&lt;a:bodyPr&gt;</c> — text body properties.</summary>
    public static readonly XName BodyProperties = Dml + "bodyPr";

    /// <summary><c>&lt;a:lstStyle&gt;</c> — list style (paragraph defaults).</summary>
    public static readonly XName ListStyle = Dml + "lstStyle";

    /// <summary><c>&lt;a:p&gt;</c> — paragraph.</summary>
    public static readonly XName Paragraph = Dml + "p";

    /// <summary><c>&lt;a:pPr&gt;</c> — paragraph properties.</summary>
    public static readonly XName ParagraphProperties = Dml + "pPr";

    /// <summary><c>&lt;a:r&gt;</c> — text run.</summary>
    public static readonly XName Run = Dml + "r";

    /// <summary><c>&lt;a:rPr&gt;</c> — run (character) properties.</summary>
    public static readonly XName RunProperties = Dml + "rPr";

    /// <summary><c>&lt;a:t&gt;</c> — text content of a run.</summary>
    public static readonly XName Text = Dml + "t";

    /// <summary><c>&lt;a:endParaRPr&gt;</c> — end-of-paragraph run properties.</summary>
    public static readonly XName EndParagraphRunProperties = Dml + "endParaRPr";

    /// <summary><c>&lt;a:fld&gt;</c> — field (auto-updating text, e.g. slide number or date).</summary>
    public static readonly XName Field = Dml + "fld";

    /// <summary><c>&lt;a:br&gt;</c> — line break within a paragraph.</summary>
    public static readonly XName LineBreak = Dml + "br";

    // ── Bullets ───────────────────────────────────────────────────────────────

    /// <summary><c>&lt;a:buNone&gt;</c> — no bullet.</summary>
    public static readonly XName BulletNone = Dml + "buNone";

    /// <summary><c>&lt;a:buChar&gt;</c> — character bullet.</summary>
    public static readonly XName BulletChar = Dml + "buChar";

    /// <summary><c>&lt;a:buAutoNum&gt;</c> — auto-numbered bullet.</summary>
    public static readonly XName BulletAutoNumber = Dml + "buAutoNum";

    /// <summary><c>&lt;a:buBlip&gt;</c> — picture bullet.</summary>
    public static readonly XName BulletBlip = Dml + "buBlip";

    /// <summary><c>&lt;a:buFont&gt;</c> — bullet font.</summary>
    public static readonly XName BulletFont = Dml + "buFont";

    /// <summary><c>&lt;a:buClr&gt;</c> — bullet colour.</summary>
    public static readonly XName BulletColor = Dml + "buClr";

    /// <summary><c>&lt;a:buSzPct&gt;</c> — bullet size as percentage of text.</summary>
    public static readonly XName BulletSizePercent = Dml + "buSzPct";

    // ── Fonts ─────────────────────────────────────────────────────────────────

    /// <summary><c>&lt;a:latin&gt;</c> — Latin font reference.</summary>
    public static readonly XName LatinFont = Dml + "latin";

    /// <summary><c>&lt;a:ea&gt;</c> — East Asian font reference.</summary>
    public static readonly XName EastAsianFont = Dml + "ea";

    /// <summary><c>&lt;a:cs&gt;</c> — complex script font reference.</summary>
    public static readonly XName ComplexScriptFont = Dml + "cs";

    // ── Spacing ───────────────────────────────────────────────────────────────

    /// <summary><c>&lt;a:spcBef&gt;</c> — space before paragraph.</summary>
    public static readonly XName SpaceBefore = Dml + "spcBef";

    /// <summary><c>&lt;a:spcAft&gt;</c> — space after paragraph.</summary>
    public static readonly XName SpaceAfter = Dml + "spcAft";

    /// <summary><c>&lt;a:lnSpc&gt;</c> — line spacing.</summary>
    public static readonly XName LineSpacing = Dml + "lnSpc";

    /// <summary><c>&lt;a:spcPts&gt;</c> — spacing in hundredths of a point.</summary>
    public static readonly XName SpacingPoints = Dml + "spcPts";

    /// <summary><c>&lt;a:spcPct&gt;</c> — spacing as thousandths of a percent.</summary>
    public static readonly XName SpacingPercent = Dml + "spcPct";

    // ── Theme ─────────────────────────────────────────────────────────────────

    /// <summary><c>&lt;a:theme&gt;</c> — root element of a theme part.</summary>
    public static readonly XName Theme = Dml + "theme";

    /// <summary><c>&lt;a:themeElements&gt;</c> — container for colour, font, and format schemes.</summary>
    public static readonly XName ThemeElements = Dml + "themeElements";

    /// <summary><c>&lt;a:clrScheme&gt;</c> — colour scheme.</summary>
    public static readonly XName ColorScheme = Dml + "clrScheme";

    /// <summary><c>&lt;a:fontScheme&gt;</c> — font scheme.</summary>
    public static readonly XName FontScheme = Dml + "fontScheme";

    /// <summary><c>&lt;a:fmtScheme&gt;</c> — format scheme (fills, lines, effects).</summary>
    public static readonly XName FormatScheme = Dml + "fmtScheme";

    /// <summary><c>&lt;a:majorFont&gt;</c> — major (heading) font set.</summary>
    public static readonly XName MajorFont = Dml + "majorFont";

    /// <summary><c>&lt;a:minorFont&gt;</c> — minor (body) font set.</summary>
    public static readonly XName MinorFont = Dml + "minorFont";

    /// <summary><c>&lt;a:dk1&gt;</c> — Dark 1 colour slot.</summary>
    public static readonly XName Dark1 = Dml + "dk1";
    /// <summary><c>&lt;a:lt1&gt;</c> — Light 1 colour slot.</summary>
    public static readonly XName Light1 = Dml + "lt1";
    /// <summary><c>&lt;a:dk2&gt;</c> — Dark 2 colour slot.</summary>
    public static readonly XName Dark2 = Dml + "dk2";
    /// <summary><c>&lt;a:lt2&gt;</c> — Light 2 colour slot.</summary>
    public static readonly XName Light2 = Dml + "lt2";
    /// <summary><c>&lt;a:accent1&gt;</c></summary>
    public static readonly XName Accent1 = Dml + "accent1";
    /// <summary><c>&lt;a:accent2&gt;</c></summary>
    public static readonly XName Accent2 = Dml + "accent2";
    /// <summary><c>&lt;a:accent3&gt;</c></summary>
    public static readonly XName Accent3 = Dml + "accent3";
    /// <summary><c>&lt;a:accent4&gt;</c></summary>
    public static readonly XName Accent4 = Dml + "accent4";
    /// <summary><c>&lt;a:accent5&gt;</c></summary>
    public static readonly XName Accent5 = Dml + "accent5";
    /// <summary><c>&lt;a:accent6&gt;</c></summary>
    public static readonly XName Accent6 = Dml + "accent6";
    /// <summary><c>&lt;a:hlink&gt;</c> — Hyperlink colour slot.</summary>
    public static readonly XName Hyperlink = Dml + "hlink";
    /// <summary><c>&lt;a:folHlink&gt;</c> — Followed hyperlink colour slot.</summary>
    public static readonly XName FollowedHyperlink = Dml + "folHlink";

    /// <summary><c>&lt;a:hlinkClick&gt;</c> — click hyperlink on a shape (in cNvPr) or run (in rPr).</summary>
    public static readonly XName HyperlinkClick = Dml + "hlinkClick";
    /// <summary><c>&lt;a:hlinkHover&gt;</c> — mouse-over hyperlink.</summary>
    public static readonly XName HyperlinkHover = Dml + "hlinkHover";

    // ── Table ─────────────────────────────────────────────────────────────────

    /// <summary><c>&lt;a:tbl&gt;</c> — table root element.</summary>
    public static readonly XName Table = Dml + "tbl";

    /// <summary><c>&lt;a:tblPr&gt;</c> — table properties.</summary>
    public static readonly XName TableProperties = Dml + "tblPr";

    /// <summary><c>&lt;a:tblGrid&gt;</c> — table column grid.</summary>
    public static readonly XName TableGrid = Dml + "tblGrid";

    /// <summary><c>&lt;a:gridCol&gt;</c> — one column in the table grid.</summary>
    public static readonly XName GridColumn = Dml + "gridCol";

    /// <summary><c>&lt;a:tr&gt;</c> — table row.</summary>
    public static readonly XName TableRow = Dml + "tr";

    /// <summary><c>&lt;a:tc&gt;</c> — table cell.</summary>
    public static readonly XName TableCell = Dml + "tc";

    /// <summary><c>&lt;a:tcPr&gt;</c> — table cell properties.</summary>
    public static readonly XName TableCellProperties = Dml + "tcPr";

    // ── Graphic frame ─────────────────────────────────────────────────────────

    /// <summary><c>&lt;a:graphic&gt;</c> — graphic container inside a graphic frame.</summary>
    public static readonly XName Graphic = Dml + "graphic";

    /// <summary><c>&lt;a:graphicData&gt;</c> — typed graphic data.</summary>
    public static readonly XName GraphicData = Dml + "graphicData";

    // ── Commonly used attribute names ─────────────────────────────────────────

    /// <summary>Preset shape type attribute: <c>prst</c></summary>
    public const string AttributePreset = "prst";

    /// <summary>Width attribute: <c>cx</c></summary>
    public const string AttributeWidth = "cx";

    /// <summary>Height attribute: <c>cy</c></summary>
    public const string AttributeHeight = "cy";

    /// <summary>X position attribute: <c>x</c></summary>
    public const string AttributeX = "x";

    /// <summary>Y position attribute: <c>y</c></summary>
    public const string AttributeY = "y";

    /// <summary>Rotation attribute: <c>rot</c> (in 1/60000 degrees, clockwise).</summary>
    public const string AttributeRotation = "rot";

    /// <summary>Flip horizontal attribute: <c>flipH</c></summary>
    public const string AttributeFlipHorizontal = "flipH";

    /// <summary>Flip vertical attribute: <c>flipV</c></summary>
    public const string AttributeFlipVertical = "flipV";

    /// <summary>Line width attribute: <c>w</c> (in EMU).</summary>
    public const string AttributeLineWidth = "w";

    /// <summary>Colour value attribute: <c>val</c></summary>
    public const string AttributeValue = "val";

    /// <summary>Language attribute: <c>lang</c></summary>
    public const string AttributeLanguage = "lang";

    /// <summary>Bold attribute: <c>b</c></summary>
    public const string AttributeBold = "b";

    /// <summary>Italic attribute: <c>i</c></summary>
    public const string AttributeItalic = "i";

    /// <summary>Underline attribute: <c>u</c></summary>
    public const string AttributeUnderline = "u";

    /// <summary>Strike attribute: <c>strike</c></summary>
    public const string AttributeStrike = "strike";

    /// <summary>Font size attribute: <c>sz</c> (in hundredths of a point).</summary>
    public const string AttributeFontSize = "sz";

    /// <summary>Alignment attribute: <c>algn</c></summary>
    public const string AttributeAlignment = "algn";

    /// <summary>Gradient stop position: <c>pos</c> (0–100000).</summary>
    public const string AttributePosition = "pos";

    /// <summary>Table row height: <c>h</c> (in EMU).</summary>
    public const string AttributeRowHeight = "h";

    /// <summary>Font typeface attribute: <c>typeface</c></summary>
    public const string AttributeTypeface = "typeface";

    /// <summary>Dirty flag attribute: <c>dirty</c></summary>
    public const string AttributeDirty = "dirty";

    /// <summary>Hidden attribute: <c>hidden</c></summary>
    public const string AttributeHidden = "hidden";

    /// <summary>Description / alt-text attribute: <c>descr</c></summary>
    public const string AttributeDescription = "descr";

    /// <summary>URI attribute on <c>&lt;a:graphicData&gt;</c>: <c>uri</c></summary>
    public const string AttributeUri = "uri";
}
