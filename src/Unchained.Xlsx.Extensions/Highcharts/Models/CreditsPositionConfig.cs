namespace Unchained.Xlsx.Extensions.Highcharts.Models;

/// <summary>Credits position configuration.</summary>
public class CreditsPositionConfig
{
    /// <summary>Horizontal alignment: "left", "center", "right".</summary>
    public string? Align { get; set; }

    /// <summary>X offset in pixels.</summary>
    public double? X { get; set; }

    /// <summary>Vertical alignment: "top", "middle", "bottom".</summary>
    public string? VerticalAlign { get; set; }

    /// <summary>Y offset in pixels.</summary>
    public double? Y { get; set; }
}
