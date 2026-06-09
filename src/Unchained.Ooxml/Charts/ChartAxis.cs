using Unchained.Ooxml.Drawing;

namespace Unchained.Ooxml.Charts;

/// <summary>
/// A chart axis (category or value). Mirrors the OOXML <c>&lt;c:catAx&gt;</c>/<c>&lt;c:valAx&gt;</c>
/// at a practical level: title, visibility, scale bounds, gridlines, and number format.
/// </summary>
public sealed class ChartAxis
{
    /// <summary>Whether the axis is which kind — affects which OOXML element is emitted.</summary>
    public ChartAxisKind Kind { get; set; }

    /// <summary>Whether the axis is shown. <see langword="false"/> hides it (<c>delete=1</c>).</summary>
    public bool IsVisible { get; set; } = true;

    /// <summary>The axis title text. <see langword="null"/> or empty means no title.</summary>
    public string? Title { get; set; }

    /// <summary>Explicit minimum value (value axis only). <see langword="null"/> = auto.</summary>
    public double? Minimum { get; set; }

    /// <summary>Explicit maximum value (value axis only). <see langword="null"/> = auto.</summary>
    public double? Maximum { get; set; }

    /// <summary>Major unit between gridlines/ticks. <see langword="null"/> = auto.</summary>
    public double? MajorUnit { get; set; }

    /// <summary>Minor unit. <see langword="null"/> = auto.</summary>
    public double? MinorUnit { get; set; }

    /// <summary>Whether major gridlines are drawn.</summary>
    public bool HasMajorGridlines { get; set; }

    /// <summary>Whether minor gridlines are drawn.</summary>
    public bool HasMinorGridlines { get; set; }

    /// <summary>Number format code (e.g. <c>"0.00"</c>, <c>"0%"</c>). <see langword="null"/> = general.</summary>
    public string? NumberFormat { get; set; }

    /// <summary>Position of the axis (left/right/top/bottom). <see langword="null"/> = auto.</summary>
    public string? Position { get; set; }
}

/// <summary>Which kind of chart axis.</summary>
public enum ChartAxisKind
{
    /// <summary>Category (X) axis — labels.</summary>
    Category,

    /// <summary>Value (Y) axis — numeric.</summary>
    Value
}
