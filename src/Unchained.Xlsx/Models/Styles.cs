namespace Unchained.Xlsx.Models.Styles;

/// <summary>The line style of a cell border edge.</summary>
public enum BorderStyle
{
    /// <summary>No border.</summary>
    None,

    /// <summary>Thin line.</summary>
    Thin,

    /// <summary>Medium line.</summary>
    Medium,

    /// <summary>Thick line.</summary>
    Thick,

    /// <summary>Dashed line.</summary>
    Dashed,

    /// <summary>Dotted line.</summary>
    Dotted,

    /// <summary>Double line.</summary>
    Double,

    /// <summary>Hairline (thinnest).</summary>
    Hair,

    /// <summary>Medium dashed line.</summary>
    MediumDashed,

    /// <summary>Dash-dot line.</summary>
    DashDot,

    /// <summary>Medium dash-dot line.</summary>
    MediumDashDot,

    /// <summary>Dash-dot-dot line.</summary>
    DashDotDot,

    /// <summary>Medium dash-dot-dot line.</summary>
    MediumDashDotDot,

    /// <summary>Slanted dash-dot line.</summary>
    SlantDashDot
}

/// <summary>The fill pattern of a cell background.</summary>
public enum FillPattern
{
    /// <summary>No fill.</summary>
    None,

    /// <summary>A solid foreground fill.</summary>
    Solid,

    /// <summary>75% gray.</summary>
    DarkGray,

    /// <summary>50% gray.</summary>
    MediumGray,

    /// <summary>25% gray.</summary>
    LightGray,

    /// <summary>12.5% gray.</summary>
    Gray125,

    /// <summary>6.25% gray.</summary>
    Gray0625,

    /// <summary>Dark horizontal stripes.</summary>
    DarkHorizontal,

    /// <summary>Dark vertical stripes.</summary>
    DarkVertical,

    /// <summary>Dark diagonal-down stripes.</summary>
    DarkDown,

    /// <summary>Dark diagonal-up stripes.</summary>
    DarkUp,

    /// <summary>Dark grid.</summary>
    DarkGrid,

    /// <summary>Dark trellis.</summary>
    DarkTrellis,

    /// <summary>Light horizontal stripes.</summary>
    LightHorizontal,

    /// <summary>Light vertical stripes.</summary>
    LightVertical,

    /// <summary>Light diagonal-down stripes.</summary>
    LightDown,

    /// <summary>Light diagonal-up stripes.</summary>
    LightUp,

    /// <summary>Light grid.</summary>
    LightGrid,

    /// <summary>Light trellis.</summary>
    LightTrellis
}

/// <summary>The underline style applied to cell font.</summary>
public enum FontUnderline
{
    /// <summary>No underline.</summary>
    None,

    /// <summary>A single underline.</summary>
    Single,

    /// <summary>A double underline.</summary>
    Double,

    /// <summary>A single accounting-style underline (spans the cell width).</summary>
    SingleAccounting,

    /// <summary>A double accounting-style underline.</summary>
    DoubleAccounting
}

/// <summary>The vertical alignment (superscript / subscript) of cell font.</summary>
public enum FontVerticalAlignment
{
    /// <summary>Normal baseline.</summary>
    None,

    /// <summary>Raised above the baseline.</summary>
    Superscript,

    /// <summary>Lowered below the baseline.</summary>
    Subscript
}

/// <summary>Horizontal text alignment within a cell.</summary>
public enum HorizontalAlignment
{
    /// <summary>Default alignment (text left, numbers right).</summary>
    General,

    /// <summary>Left-aligned.</summary>
    Left,

    /// <summary>Centered.</summary>
    Center,

    /// <summary>Right-aligned.</summary>
    Right,

    /// <summary>Repeat the content to fill the cell width.</summary>
    Fill,

    /// <summary>Justify text to both edges.</summary>
    Justify,

    /// <summary>Center across the selection of merged cells.</summary>
    CenterAcrossSelection,

    /// <summary>Distribute text evenly across the cell width.</summary>
    Distributed
}

/// <summary>The reading order (text direction) of a cell's content.</summary>
public enum ReadingOrder
{
    /// <summary>Determined by the first strong-directional character (default).</summary>
    ContextDependent,

    /// <summary>Left-to-right.</summary>
    LeftToRight,

    /// <summary>Right-to-left.</summary>
    RightToLeft
}

/// <summary>Vertical text alignment within a cell.</summary>
public enum VerticalAlignment
{
    /// <summary>Aligned to the top of the cell.</summary>
    Top,

    /// <summary>Vertically centered.</summary>
    Center,

    /// <summary>Aligned to the bottom of the cell (Excel default).</summary>
    Bottom,

    /// <summary>Justified vertically.</summary>
    Justify,

    /// <summary>Distributed vertically.</summary>
    Distributed
}
