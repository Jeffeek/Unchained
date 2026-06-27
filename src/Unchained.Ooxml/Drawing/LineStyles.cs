namespace Unchained.Ooxml.Drawing;

/// <summary>
///     Converts between <see cref="LineDashStyle"/> values and their OOXML string representations.
/// </summary>
internal static class LineStyles
{
    public static LineDashStyle ParseDashStyle(string value) => value switch
    {
        "solid" => LineDashStyle.Solid,
        "dot" => LineDashStyle.Dot,
        "dash" => LineDashStyle.Dash,
        "dashDot" => LineDashStyle.DashDot,
        "lgDash" => LineDashStyle.LongDash,
        "lgDashDot" => LineDashStyle.LongDashDot,
        "lgDashDotDot" => LineDashStyle.LongDashDotDot,
        "sysDash" => LineDashStyle.SystemDash,
        "sysDot" => LineDashStyle.SystemDot,
        "sysDashDot" => LineDashStyle.SystemDashDot,
        "sysDashDotDot" => LineDashStyle.SystemDashDotDot,
        _ => LineDashStyle.Solid
    };

    public static string DashStyleToString(LineDashStyle style) => style switch
    {
        LineDashStyle.Dot => "dot",
        LineDashStyle.Dash => "dash",
        LineDashStyle.DashDot => "dashDot",
        LineDashStyle.LongDash => "lgDash",
        LineDashStyle.LongDashDot => "lgDashDot",
        LineDashStyle.LongDashDotDot => "lgDashDotDot",
        LineDashStyle.SystemDash => "sysDash",
        LineDashStyle.SystemDot => "sysDot",
        LineDashStyle.SystemDashDot => "sysDashDot",
        LineDashStyle.SystemDashDotDot => "sysDashDotDot",
        _ => "solid"
    };

    public static ArrowHeadType ParseArrowType(string value) => value switch
    {
        "triangle" => ArrowHeadType.Triangle,
        "stealth" => ArrowHeadType.Stealth,
        "diamond" => ArrowHeadType.Diamond,
        "oval" => ArrowHeadType.Oval,
        "arrow" or "open" => ArrowHeadType.Open,
        _ => ArrowHeadType.None
    };

    public static string ArrowTypeToString(ArrowHeadType type) => type switch
    {
        ArrowHeadType.Triangle => "triangle",
        ArrowHeadType.Stealth => "stealth",
        ArrowHeadType.Diamond => "diamond",
        ArrowHeadType.Oval => "oval",
        ArrowHeadType.Open => "arrow",
        _ => "none"
    };

    public static ArrowHeadSize ParseArrowSize(string value) => value switch
    {
        "sm" => ArrowHeadSize.Small,
        "lg" => ArrowHeadSize.Large,
        _ => ArrowHeadSize.Medium
    };

    public static string ArrowSizeToString(ArrowHeadSize size) => size switch
    {
        ArrowHeadSize.Small => "sm",
        ArrowHeadSize.Large => "lg",
        _ => "med"
    };

    public static PatternPreset ParsePatternPreset(string value) => value switch
    {
        "pct5" => PatternPreset.Percent5,
        "pct10" => PatternPreset.Percent10,
        "pct20" => PatternPreset.Percent20,
        "pct25" => PatternPreset.Percent25,
        "pct30" => PatternPreset.Percent30,
        "pct40" => PatternPreset.Percent40,
        "pct50" => PatternPreset.Percent50,
        "pct60" => PatternPreset.Percent60,
        "pct70" => PatternPreset.Percent70,
        "pct75" => PatternPreset.Percent75,
        "pct80" => PatternPreset.Percent80,
        "pct90" => PatternPreset.Percent90,
        "horz" => PatternPreset.HorizontalLines,
        "vert" => PatternPreset.VerticalLines,
        "ltHorz" => PatternPreset.LightHorizontal,
        "ltVert" => PatternPreset.LightVertical,
        "ltDnDiag" => PatternPreset.LightDownwardDiagonal,
        "ltUpDiag" => PatternPreset.LightUpwardDiagonal,
        "dkHorz" => PatternPreset.DarkHorizontal,
        "dkVert" => PatternPreset.DarkVertical,
        "dkDnDiag" => PatternPreset.DarkDownwardDiagonal,
        "dkUpDiag" => PatternPreset.DarkUpwardDiagonal,
        "wdDnDiag" => PatternPreset.WideDownwardDiagonal,
        "wdUpDiag" => PatternPreset.WideUpwardDiagonal,
        "dashHorz" => PatternPreset.DashedHorizontal,
        "dashVert" => PatternPreset.DashedVertical,
        "dashDnDiag" => PatternPreset.DashedDownwardDiagonal,
        "dashUpDiag" => PatternPreset.DashedUpwardDiagonal,
        "smConfetti" => PatternPreset.SmallConfetti,
        "lgConfetti" => PatternPreset.LargeConfetti,
        "zigZag" => PatternPreset.Zigzag,
        "wave" => PatternPreset.Wave,
        "diagBrick" => PatternPreset.DiagonalBrick,
        "horzBrick" => PatternPreset.HorizontalBrick,
        "weave" => PatternPreset.Weave,
        "plaid" => PatternPreset.Plaid,
        "divot" => PatternPreset.Divot,
        "dotGrid" => PatternPreset.DottedGrid,
        "dotDmnd" => PatternPreset.DottedDiamond,
        "shingle" => PatternPreset.Shingle,
        "trellis" => PatternPreset.Trellis,
        "sphere" => PatternPreset.Sphere,
        "smGrid" => PatternPreset.SmallGrid,
        "lgGrid" => PatternPreset.LargeGrid,
        "smCheck" => PatternPreset.SmallCheckerBoard,
        "lgCheck" => PatternPreset.LargeCheckerBoard,
        "openDmnd" => PatternPreset.OutlinedDiamond,
        "solidDmnd" => PatternPreset.SolidDiamond,
        _ => PatternPreset.Percent5
    };

    public static string PatternPresetToString(PatternPreset preset) => preset switch
    {
        PatternPreset.Percent5 => "pct5",
        PatternPreset.Percent10 => "pct10",
        PatternPreset.Percent20 => "pct20",
        PatternPreset.Percent25 => "pct25",
        PatternPreset.Percent30 => "pct30",
        PatternPreset.Percent40 => "pct40",
        PatternPreset.Percent50 => "pct50",
        PatternPreset.Percent60 => "pct60",
        PatternPreset.Percent70 => "pct70",
        PatternPreset.Percent75 => "pct75",
        PatternPreset.Percent80 => "pct80",
        PatternPreset.Percent90 => "pct90",
        PatternPreset.HorizontalLines => "horz",
        PatternPreset.VerticalLines => "vert",
        _ => "pct5"
    };
}
