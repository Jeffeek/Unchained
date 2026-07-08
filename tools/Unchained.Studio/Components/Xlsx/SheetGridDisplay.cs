using System.Globalization;
using System.Text;
using Unchained.Studio.Studio.Xlsx;
using Unchained.Xlsx.Drawings;
using Unchained.Xlsx.Models;
using Unchained.Xlsx.Models.Cell;
using Unchained.Xlsx.Models.Drawings;
using Unchained.Xlsx.Styles;
using Unchained.Xlsx.Worksheets;

namespace Unchained.Studio.Components.Xlsx;

/// <summary>
///     Renders display values and CSS for the cell grid.
///     Pure logic — no internal state mutations.
/// </summary>
internal static class SheetGridDisplay
{
    internal const double DefaultColWidthChars = 8.43;
    internal const double DefaultRowHeightPoints = 15.0;
    private const double PixelsPerChar = 7.0;
    private const double ColWidthPadding = 5.0;
    private const double PixelsPerPoint = 96.0 / 72.0;
    private const double RowHeaderWidthPx = 44.0;
    private const double EmusPerPixel = 9525.0; // 914400 EMU per inch / 96 dpi
    private const double ColHeaderHeightPx = 24.0;

    public static string DisplayValue(Worksheet sheet, int row, int col)
    {
        var cell = sheet.GetCell(row, col);
        return cell is null ? string.Empty : cell.GetFormattedString();
    }

    public static string CellClass(Worksheet sheet, int row, int col, bool selected, bool active)
    {
        var cell = sheet.GetCell(row, col);
        var kind = cell?.CellType switch
        {
            CellType.Number => "sg-num",
            CellType.Boolean => "sg-bool",
            CellType.Error => "sg-err",
            CellType.Formula => "sg-formula",
            _ => string.Empty
        };

        var merged = cell?.IsMerged == true;
        return $"sg-cell {kind} {(selected ? "sg-selected" : string.Empty)} {(active ? "sg-active" : string.Empty)} {(merged ? "sg-merged" : string.Empty)}"
            .Replace("  ", " ")
            .Trim();
    }

    /// <summary>Builds the inline CSS for a cell from its effective style (font/fill/alignment/border).</summary>
    public static string CellStyle(Worksheet sheet, int row, int col)
    {
        var cell = sheet.GetCell(row, col);

        if (cell is not { StyleIndex: > 0 })
            return string.Empty;

        var sb = new StringBuilder();
        var styles = sheet.Document.Styles;
        var index = cell.StyleIndex;
        var font = styles.GetFont(index);
        var fill = styles.GetFill(index);
        var border = styles.GetBorder(index);
        var align = cell.GetEffectiveStyle().Alignment;

        if (font.Bold) sb.Append("font-weight:bold;");
        if (font.Italic) sb.Append("font-style:italic;");
        if (font.Underline != FontUnderline.None) sb.Append("text-decoration:underline;");
        if (font.Strikethrough) sb.Append("text-decoration:line-through;");

        if (font.Color is { } fc) sb.Append("color:").Append(XlsxColor.ToHex(fc)).Append(';');
        // SizePoints differs from the 11pt default → scale the cell font.
        if (Math.Abs(font.SizePoints - 11.0) > 0.01)
            sb.Append("font-size:").Append((font.SizePoints / 11.0 * 0.8).ToString("0.##", CultureInfo.InvariantCulture)).Append("rem;");

        if (fill is { PatternType: not FillPattern.None, ForegroundColor: { } bg })
            sb.Append("background-color:").Append(XlsxColor.ToHex(bg)).Append(';');

        AppendAlignment(sb, align);
        AppendBorders(sb, border);

        // Respect column width (px) when set.
        return sb.ToString();
    }

    private static void AppendAlignment(StringBuilder sb, CellAlignment align)
    {
        var horizontal = align.Horizontal switch
        {
            HorizontalAlignment.Left => "left",
            HorizontalAlignment.Center or HorizontalAlignment.CenterAcrossSelection => "center",
            HorizontalAlignment.Right => "right",
            HorizontalAlignment.Justify or HorizontalAlignment.Distributed => "justify",
            _ => null
        };
        if (horizontal != null) sb.Append("text-align:").Append(horizontal).Append(';');

        var vertical = align.Vertical switch
        {
            VerticalAlignment.Top => "top",
            VerticalAlignment.Center => "middle",
            VerticalAlignment.Bottom => "bottom",
            _ => null
        };
        if (vertical != null) sb.Append("vertical-align:").Append(vertical).Append(';');

        if (align.WrapText) sb.Append("white-space:normal;");
    }

    private static void AppendBorders(StringBuilder sb, CellBorder border)
    {
        AppendEdge(sb, "border-left", border.Left);
        AppendEdge(sb, "border-right", border.Right);
        AppendEdge(sb, "border-top", border.Top);
        AppendEdge(sb, "border-bottom", border.Bottom);
    }

    private static void AppendEdge(StringBuilder sb, string prop, BorderLine line)
    {
        if (line.Style == BorderStyle.None)
            return;

        var width = line.Style switch
        {
            BorderStyle.Thick or BorderStyle.Double => "3px",
            BorderStyle.Medium or BorderStyle.MediumDashed or BorderStyle.MediumDashDot => "2px",
            _ => "1px"
        };
        var css = line.Style switch
        {
            BorderStyle.Dashed or BorderStyle.MediumDashed or BorderStyle.DashDot or BorderStyle.DashDotDot => "dashed",
            BorderStyle.Dotted or BorderStyle.Hair => "dotted",
            BorderStyle.Double => "double",
            _ => "solid"
        };
        var color = line.Color is { } c ? XlsxColor.ToHex(c) : "#000";
        sb.Append(prop).Append(':').Append(width).Append(' ').Append(css).Append(' ').Append(color).Append(';');
    }

    public static string ColumnHeadStyle(Worksheet sheet, int col)
    {
        var width = sheet.GetColumn(col)?.Width;
        var px = ((width ?? DefaultColWidthChars) * PixelsPerChar) + ColWidthPadding;
        return $"width:{px.ToString("0", CultureInfo.InvariantCulture)}px;min-width:{px.ToString("0", CultureInfo.InvariantCulture)}px;";
    }

    public static string RowStyle(Worksheet sheet, int row)
    {
        var height = sheet.GetRow(row)?.Height;
        var px = (height ?? DefaultRowHeightPoints) * PixelsPerPoint;
        return $"height:{px.ToString("0", CultureInfo.InvariantCulture)}px;";
    }

    public static double ColumnWidthPx(Worksheet sheet, int col) =>
        ((sheet.GetColumn(col)?.Width ?? DefaultColWidthChars) * PixelsPerChar) + ColWidthPadding;

    public static double RowHeightPx(Worksheet sheet, int row) =>
        (sheet.GetRow(row)?.Height ?? DefaultRowHeightPoints) * PixelsPerPoint;

    /// <summary>Left/top pixel offset of a cell's top-left corner within the scroll area.</summary>
    public static (double Left, double Top) CellOrigin(Worksheet sheet, int row, int col)
    {
        var left = RowHeaderWidthPx;
        for (var c = 1; c < col; c++) left += ColumnWidthPx(sheet, c);
        var top = ColHeaderHeightPx;
        for (var r = 1; r < row; r++) top += RowHeightPx(sheet, r);
        return (left, top);
    }

    /// <summary>Computes the absolute CSS box for a drawing from its anchor.</summary>
    public static string DrawingBox(Worksheet sheet, WorksheetDrawing drawing)
    {
        var anchor = drawing.Anchor;
        var (left, top) = CellOrigin(sheet, anchor.From.Row, anchor.From.Column);
        left += anchor.FromOffsetX.Value / EmusPerPixel;
        top += anchor.FromOffsetY.Value / EmusPerPixel;

        double width, height;
        if (anchor.AnchorType == DrawingAnchorType.TwoCell)
        {
            var (toLeft, toTop) = CellOrigin(sheet, anchor.To.Row, anchor.To.Column);
            toLeft += anchor.ToOffsetX.Value / EmusPerPixel;
            toTop += anchor.ToOffsetY.Value / EmusPerPixel;
            width = Math.Max(20, toLeft - left);
            height = Math.Max(20, toTop - top);
        }
        else
        {
            width = anchor.Width.Value / EmusPerPixel;
            height = anchor.Height.Value / EmusPerPixel;
        }

        return string.Create(
            CultureInfo.InvariantCulture,
            $"left:{left:0}px;top:{top:0}px;width:{width:0}px;height:{height:0}px;"
        );
    }

    public static string ImageDataUri(PictureDrawing pic) =>
        $"data:{pic.Image.ContentType};base64,{Convert.ToBase64String(pic.Image.Data.Span)}";

    public static (double Width, double Height) DrawingSize(Worksheet sheet, WorksheetDrawing drawing)
    {
        var anchor = drawing.Anchor;
        if (anchor.AnchorType != DrawingAnchorType.TwoCell)
            return (anchor.Width.Value / EmusPerPixel, anchor.Height.Value / EmusPerPixel);

        var (left, top) = CellOrigin(sheet, anchor.From.Row, anchor.From.Column);
        left += anchor.FromOffsetX.Value / EmusPerPixel;
        top += anchor.FromOffsetY.Value / EmusPerPixel;
        var (toLeft, toTop) = CellOrigin(sheet, anchor.To.Row, anchor.To.Column);
        toLeft += anchor.ToOffsetX.Value / EmusPerPixel;
        toTop += anchor.ToOffsetY.Value / EmusPerPixel;
        return (Math.Max(20, toLeft - left), Math.Max(20, toTop - top));
    }

    public static string DrawingClass(ChartDrawing chart, bool isSelected) =>
        $"sg-drawing sg-drawing--chart{(isSelected ? " sg-drawing--selected" : string.Empty)}";
}
