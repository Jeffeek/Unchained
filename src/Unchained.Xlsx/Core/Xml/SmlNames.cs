using System.Xml.Linq;
using Unchained.Ooxml.Xml;

namespace Unchained.Xlsx.Core.Xml;

/// <summary>
///     SpreadsheetML <see cref="XName" /> constants for the <c>x:</c> namespace and the
///     spreadsheet-specific relationship namespaces. Analogous to <c>WmlNames</c> (Docx)
///     and <c>PmlNames</c> (Pptx). <c>DmlNames</c> and <c>OoXmlHelper</c> are shared from
///     <c>Unchained.Ooxml</c> and must not be duplicated here.
/// </summary>
internal static class SmlNames
{
    private const string Sml = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private const string Xdr = "http://schemas.openxmlformats.org/drawingml/2006/spreadsheetDrawing";
    private const string Chart = OoxmlNamespaces.Chart;
    private const string Rel = OoxmlNamespaces.OfficeDocument;

    public static readonly XNamespace X = Sml;
    public static readonly XNamespace XDR = Xdr;
    public static readonly XNamespace CH = Chart;
    public static readonly XNamespace R = Rel;

    // ── Relationship type URIs ─────────────────────────────────────────────────
    private const string RelBase = OoxmlNamespaces.OfficeDocument + "/";
    public const string RelTypeOfficeDocument = RelBase + "officeDocument";
    public const string RelTypeWorksheet = RelBase + "worksheet";
    public const string RelTypeSharedStrings = RelBase + "sharedStrings";
    public const string RelTypeStyles = RelBase + "styles";
    public const string RelTypeTheme = RelBase + OoxmlNamespaces.RelTheme;
    public const string RelTypeDrawing = RelBase + "drawing";
    public const string RelTypeChart = RelBase + OoxmlNamespaces.RelChart;
    public const string RelTypeTable = RelBase + "table";
    public const string RelTypePivotTable = RelBase + "pivotTable";
    public const string RelTypePivotCacheDefinition = RelBase + "pivotCacheDefinition";
    public const string RelTypePivotCacheRecords = RelBase + "pivotCacheRecords";
    public const string RelTypeImage = RelBase + OoxmlNamespaces.RelImage;
    public const string RelTypeComments = RelBase + OoxmlNamespaces.RelComments;
    public const string RelTypeHyperlink = RelBase + OoxmlNamespaces.RelHyperlink;
    public const string RelTypeVmlDrawing = RelBase + "vmlDrawing";

    // ── Content types ──────────────────────────────────────────────────────────
    private const string CtBase = "application/vnd.openxmlformats-officedocument.spreadsheetml.";
    public const string ContentTypeWorkbook = CtBase + "sheet.main+xml";
    public const string ContentTypeWorksheet = CtBase + "worksheet+xml";
    public const string ContentTypeSharedStrings = CtBase + "sharedStrings+xml";
    public const string ContentTypeStyles = CtBase + "styles+xml";
    public const string ContentTypeTable = CtBase + "table+xml";
    public const string ContentTypeComments = CtBase + "comments+xml";
    public const string ContentTypePivotTable = CtBase + "pivotTable+xml";
    public const string ContentTypePivotCacheDefinition = CtBase + "pivotCacheDefinition+xml";
    public const string ContentTypePivotCacheRecords = CtBase + "pivotCacheRecords+xml";
    public const string ContentTypeTheme = OoxmlContentTypes.Theme;
    public const string ContentTypeDrawing = "application/vnd.openxmlformats-officedocument.drawing+xml";
    public const string ContentTypeChart = OoxmlContentTypes.Chart;

    // ── Workbook ───────────────────────────────────────────────────────────────
    public static readonly XName Workbook = X + "workbook";
    public static readonly XName FileVersion = X + "fileVersion";
    public static readonly XName WorkbookPr = X + "workbookPr";
    public static readonly XName WorkbookProtection = X + "workbookProtection";
    public static readonly XName BookViews = X + "bookViews";
    public static readonly XName WorkbookView = X + "workbookView";
    public static readonly XName Sheets = X + "sheets";
    public static readonly XName Sheet = X + "sheet";
    public static readonly XName DefinedNames = X + "definedNames";
    public static readonly XName DefinedName = X + "definedName";
    public static readonly XName CalcPr = X + "calcPr";

    // ── Worksheet ──────────────────────────────────────────────────────────────
    public static readonly XName Worksheet = X + "worksheet";
    public static readonly XName SheetPr = X + "sheetPr";
    public static readonly XName TabColor = X + "tabColor";
    public static readonly XName Dimension = X + "dimension";
    public static readonly XName SheetViews = X + "sheetViews";
    public static readonly XName SheetView = X + "sheetView";
    public static readonly XName Pane = X + "pane";
    public static readonly XName Selection = X + "selection";
    public static readonly XName SheetFormatPr = X + "sheetFormatPr";
    public static readonly XName Cols = X + "cols";
    public static readonly XName Col = X + "col";
    public static readonly XName SheetData = X + "sheetData";
    public static readonly XName Row = X + "row";
    public static readonly XName Cell = X + "c";
    public static readonly XName CellValue = X + "v";
    public static readonly XName Formula = X + "f";
    public static readonly XName InlineString = X + "is";
    public static readonly XName Text = X + "t";
    public static readonly XName MergeCells = X + "mergeCells";
    public static readonly XName MergeCell = X + "mergeCell";
    public static readonly XName SheetProtection = X + "sheetProtection";
    public static readonly XName ConditionalFormatting = X + "conditionalFormatting";
    public static readonly XName CfRule = X + "cfRule";
    public static readonly XName DataValidations = X + "dataValidations";
    public static readonly XName DataValidation = X + "dataValidation";
    public static readonly XName Formula1 = X + "formula1";
    public static readonly XName Formula2 = X + "formula2";
    public static readonly XName AutoFilter = X + "autoFilter";
    public static readonly XName PageSetup = X + "pageSetup";
    public static readonly XName PageMargins = X + "pageMargins";
    public static readonly XName HeaderFooter = X + "headerFooter";
    public static readonly XName OddHeader = X + "oddHeader";
    public static readonly XName OddFooter = X + "oddFooter";
    public static readonly XName EvenHeader = X + "evenHeader";
    public static readonly XName EvenFooter = X + "evenFooter";
    public static readonly XName FirstHeader = X + "firstHeader";
    public static readonly XName FirstFooter = X + "firstFooter";
    public static readonly XName RowBreaks = X + "rowBreaks";
    public static readonly XName ColBreaks = X + "colBreaks";
    public static readonly XName Brk = X + "brk";
    public static readonly XName Drawing = X + "drawing";
    public static readonly XName LegacyDrawing = X + "legacyDrawing";
    public static readonly XName TableParts = X + "tableParts";
    public static readonly XName TablePart = X + "tablePart";

    // ── Shared strings ───────────────────────────────────────────────────────
    public static readonly XName Sst = X + "sst";
    public static readonly XName Si = X + "si";
    public static readonly XName RichRun = X + "r";
    public static readonly XName RunProperties = X + "rPr";

    // ── Styles ─────────────────────────────────────────────────────────────────
    public static readonly XName StyleSheet = X + "styleSheet";
    public static readonly XName NumFmts = X + "numFmts";
    public static readonly XName NumFmt = X + "numFmt";
    public static readonly XName Fonts = X + "fonts";
    public static readonly XName Font = X + "font";
    public static readonly XName Fills = X + "fills";
    public static readonly XName Fill = X + "fill";
    public static readonly XName PatternFill = X + "patternFill";
    public static readonly XName GradientFill = X + "gradientFill";
    public static readonly XName FgColor = X + "fgColor";
    public static readonly XName BgColor = X + "bgColor";
    public static readonly XName Borders = X + "borders";
    public static readonly XName Border = X + "border";
    public static readonly XName Left = X + "left";
    public static readonly XName Right = X + "right";
    public static readonly XName Top = X + "top";
    public static readonly XName Bottom = X + "bottom";
    public static readonly XName Diagonal = X + "diagonal";
    public static readonly XName Color = X + "color";
    public static readonly XName CellStyleXfs = X + "cellStyleXfs";
    public static readonly XName CellXfs = X + "cellXfs";
    public static readonly XName Xf = X + "xf";
    public static readonly XName Alignment = X + "alignment";
    public static readonly XName CellStyles = X + "cellStyles";
    public static readonly XName CellStyle = X + "cellStyle";
    public static readonly XName Dxfs = X + "dxfs";
    public static readonly XName Dxf = X + "dxf";
    public static readonly XName TableStyles = X + "tableStyles";

    // ── Font sub-elements ──────────────────────────────────────────────────────
    public static readonly XName FontBold = X + "b";
    public static readonly XName FontItalic = X + "i";
    public static readonly XName FontUnderline = X + "u";
    public static readonly XName FontStrike = X + "strike";
    public static readonly XName FontSize = X + "sz";
    public static readonly XName FontName = X + "name";
    public static readonly XName FontFamily = X + "family";
    public static readonly XName FontScheme = X + "scheme";
    public static readonly XName FontVertAlign = X + "vertAlign";
    public static readonly XName FontOutline = X + "outline";
    public static readonly XName FontShadow = X + "shadow";
    public static readonly XName FontCondense = X + "condense";
    public static readonly XName FontExtend = X + "extend";

    // ── Attribute names ──────────────────────────────────────────────────────
    public const string AttributeName = "name";
    public const string AttributeDisplayName = "displayName";
    public const string AttributeRef = "ref";
    public const string AttributeErrorTitle = "errorTitle";
    public const string AttributeErrorMessage = "error";
    public const string AttributePromptTitle = "promptTitle";
    public const string AttributePrompt = "prompt";
}
