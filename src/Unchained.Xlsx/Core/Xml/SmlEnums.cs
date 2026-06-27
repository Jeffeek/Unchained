using Unchained.Xlsx.Models;

namespace Unchained.Xlsx.Core.Xml;

/// <summary>Maps style enums to and from their SpreadsheetML attribute literals.</summary>
internal static class SmlEnums
{
    // ── Horizontal alignment ───────────────────────────────────────────────────

    public static string? ToLiteral(HorizontalAlignment value) => value switch
    {
        HorizontalAlignment.General => null,
        HorizontalAlignment.Left => "left",
        HorizontalAlignment.Center => "center",
        HorizontalAlignment.Right => "right",
        HorizontalAlignment.Fill => "fill",
        HorizontalAlignment.Justify => "justify",
        HorizontalAlignment.CenterAcrossSelection => "centerContinuous",
        HorizontalAlignment.Distributed => "distributed",
        _ => null
    };

    public static HorizontalAlignment ParseHorizontal(string? literal) => literal switch
    {
        "left" => HorizontalAlignment.Left,
        "center" => HorizontalAlignment.Center,
        "right" => HorizontalAlignment.Right,
        "fill" => HorizontalAlignment.Fill,
        "justify" => HorizontalAlignment.Justify,
        "centerContinuous" => HorizontalAlignment.CenterAcrossSelection,
        "distributed" => HorizontalAlignment.Distributed,
        _ => HorizontalAlignment.General
    };

    // ── Vertical alignment ─────────────────────────────────────────────────────

    public static string? ToLiteral(VerticalAlignment value) => value switch
    {
        VerticalAlignment.Top => "top",
        VerticalAlignment.Center => "center",
        VerticalAlignment.Bottom => null, // bottom is the default
        VerticalAlignment.Justify => "justify",
        VerticalAlignment.Distributed => "distributed",
        _ => null
    };

    public static VerticalAlignment ParseVertical(string? literal) => literal switch
    {
        "top" => VerticalAlignment.Top,
        "center" => VerticalAlignment.Center,
        "justify" => VerticalAlignment.Justify,
        "distributed" => VerticalAlignment.Distributed,
        _ => VerticalAlignment.Bottom
    };

    // ── Reading order ──────────────────────────────────────────────────────────

    public static int ToLiteral(ReadingOrder value) => (int)value;

    public static ReadingOrder ParseReadingOrder(int? value) => value switch
    {
        1 => ReadingOrder.LeftToRight,
        2 => ReadingOrder.RightToLeft,
        _ => ReadingOrder.ContextDependent
    };

    // ── Font underline ─────────────────────────────────────────────────────────

    public static string? ToLiteral(FontUnderline value) => value switch
    {
        FontUnderline.None => null,
        FontUnderline.Single => "single",
        FontUnderline.Double => "double",
        FontUnderline.SingleAccounting => "singleAccounting",
        FontUnderline.DoubleAccounting => "doubleAccounting",
        _ => null
    };

    public static FontUnderline ParseUnderline(string? literal) => literal switch
    {
        "single" or null => FontUnderline.Single, // <u/> with no val means single
        "double" => FontUnderline.Double,
        "singleAccounting" => FontUnderline.SingleAccounting,
        "doubleAccounting" => FontUnderline.DoubleAccounting,
        "none" => FontUnderline.None,
        _ => FontUnderline.Single
    };

    // ── Font vertical alignment ──────────────────────────────────────────────

    public static string? ToLiteral(FontVerticalAlignment value) => value switch
    {
        FontVerticalAlignment.Superscript => "superscript",
        FontVerticalAlignment.Subscript => "subscript",
        _ => null
    };

    public static FontVerticalAlignment ParseFontVerticalAlignment(string? literal) => literal switch
    {
        "superscript" => FontVerticalAlignment.Superscript,
        "subscript" => FontVerticalAlignment.Subscript,
        _ => FontVerticalAlignment.None
    };

    // ── Fill pattern ───────────────────────────────────────────────────────────

    public static string ToLiteral(FillPattern value) => value switch
    {
        FillPattern.None => "none",
        FillPattern.Solid => "solid",
        FillPattern.DarkGray => "darkGray",
        FillPattern.MediumGray => "mediumGray",
        FillPattern.LightGray => "lightGray",
        FillPattern.Gray125 => "gray125",
        FillPattern.Gray0625 => "gray0625",
        FillPattern.DarkHorizontal => "darkHorizontal",
        FillPattern.DarkVertical => "darkVertical",
        FillPattern.DarkDown => "darkDown",
        FillPattern.DarkUp => "darkUp",
        FillPattern.DarkGrid => "darkGrid",
        FillPattern.DarkTrellis => "darkTrellis",
        FillPattern.LightHorizontal => "lightHorizontal",
        FillPattern.LightVertical => "lightVertical",
        FillPattern.LightDown => "lightDown",
        FillPattern.LightUp => "lightUp",
        FillPattern.LightGrid => "lightGrid",
        FillPattern.LightTrellis => "lightTrellis",
        _ => "none"
    };

    public static FillPattern ParseFillPattern(string? literal) => literal switch
    {
        "solid" => FillPattern.Solid,
        "darkGray" => FillPattern.DarkGray,
        "mediumGray" => FillPattern.MediumGray,
        "lightGray" => FillPattern.LightGray,
        "gray125" => FillPattern.Gray125,
        "gray0625" => FillPattern.Gray0625,
        "darkHorizontal" => FillPattern.DarkHorizontal,
        "darkVertical" => FillPattern.DarkVertical,
        "darkDown" => FillPattern.DarkDown,
        "darkUp" => FillPattern.DarkUp,
        "darkGrid" => FillPattern.DarkGrid,
        "darkTrellis" => FillPattern.DarkTrellis,
        "lightHorizontal" => FillPattern.LightHorizontal,
        "lightVertical" => FillPattern.LightVertical,
        "lightDown" => FillPattern.LightDown,
        "lightUp" => FillPattern.LightUp,
        "lightGrid" => FillPattern.LightGrid,
        "lightTrellis" => FillPattern.LightTrellis,
        _ => FillPattern.None
    };

    // ── Border style ───────────────────────────────────────────────────────────

    public static string? ToLiteral(BorderStyle value) => value switch
    {
        BorderStyle.None => null,
        BorderStyle.Thin => "thin",
        BorderStyle.Medium => "medium",
        BorderStyle.Thick => "thick",
        BorderStyle.Dashed => "dashed",
        BorderStyle.Dotted => "dotted",
        BorderStyle.Double => "double",
        BorderStyle.Hair => "hair",
        BorderStyle.MediumDashed => "mediumDashed",
        BorderStyle.DashDot => "dashDot",
        BorderStyle.MediumDashDot => "mediumDashDot",
        BorderStyle.DashDotDot => "dashDotDot",
        BorderStyle.MediumDashDotDot => "mediumDashDotDot",
        BorderStyle.SlantDashDot => "slantDashDot",
        _ => null
    };

    public static BorderStyle ParseBorderStyle(string? literal) => literal switch
    {
        "thin" => BorderStyle.Thin,
        "medium" => BorderStyle.Medium,
        "thick" => BorderStyle.Thick,
        "dashed" => BorderStyle.Dashed,
        "dotted" => BorderStyle.Dotted,
        "double" => BorderStyle.Double,
        "hair" => BorderStyle.Hair,
        "mediumDashed" => BorderStyle.MediumDashed,
        "dashDot" => BorderStyle.DashDot,
        "mediumDashDot" => BorderStyle.MediumDashDot,
        "dashDotDot" => BorderStyle.DashDotDot,
        "mediumDashDotDot" => BorderStyle.MediumDashDotDot,
        "slantDashDot" => BorderStyle.SlantDashDot,
        _ => BorderStyle.None
    };

    // ── Data validation ──────────────────────────────────────────────────────

    public static string? ToLiteral(DataValidationType value) => value switch
    {
        DataValidationType.None => null,
        DataValidationType.Whole => "whole",
        DataValidationType.Decimal => "decimal",
        DataValidationType.List => "list",
        DataValidationType.Date => "date",
        DataValidationType.Time => "time",
        DataValidationType.TextLength => "textLength",
        DataValidationType.Custom => "custom",
        _ => null
    };

    public static DataValidationType ParseValidationType(string? literal) => literal switch
    {
        "whole" => DataValidationType.Whole,
        "decimal" => DataValidationType.Decimal,
        "list" => DataValidationType.List,
        "date" => DataValidationType.Date,
        "time" => DataValidationType.Time,
        "textLength" => DataValidationType.TextLength,
        "custom" => DataValidationType.Custom,
        _ => DataValidationType.None
    };

    public static string? ToLiteral(DataValidationOperator value) => value switch
    {
        DataValidationOperator.Between => null, // between is the default
        DataValidationOperator.NotBetween => "notBetween",
        DataValidationOperator.Equal => "equal",
        DataValidationOperator.NotEqual => "notEqual",
        DataValidationOperator.GreaterThan => "greaterThan",
        DataValidationOperator.LessThan => "lessThan",
        DataValidationOperator.GreaterThanOrEqual => "greaterThanOrEqual",
        DataValidationOperator.LessThanOrEqual => "lessThanOrEqual",
        _ => null
    };

    public static DataValidationOperator ParseValidationOperator(string? literal) => literal switch
    {
        "notBetween" => DataValidationOperator.NotBetween,
        "equal" => DataValidationOperator.Equal,
        "notEqual" => DataValidationOperator.NotEqual,
        "greaterThan" => DataValidationOperator.GreaterThan,
        "lessThan" => DataValidationOperator.LessThan,
        "greaterThanOrEqual" => DataValidationOperator.GreaterThanOrEqual,
        "lessThanOrEqual" => DataValidationOperator.LessThanOrEqual,
        _ => DataValidationOperator.Between
    };

    public static string? ToLiteral(DataValidationErrorStyle value) => value switch
    {
        DataValidationErrorStyle.Stop => null, // stop is the default
        DataValidationErrorStyle.Warning => "warning",
        DataValidationErrorStyle.Information => "information",
        _ => null
    };

    public static DataValidationErrorStyle ParseErrorStyle(string? literal) => literal switch
    {
        "warning" => DataValidationErrorStyle.Warning,
        "information" => DataValidationErrorStyle.Information,
        _ => DataValidationErrorStyle.Stop
    };
}
