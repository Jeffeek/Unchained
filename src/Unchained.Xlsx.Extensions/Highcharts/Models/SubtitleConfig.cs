namespace Unchained.Xlsx.Extensions.Highcharts.Models;

/// <summary>Chart subtitle configuration.</summary>
public class SubtitleConfig
{
    /// <summary>Subtitle text.</summary>
    public string? Text { get; set; }

    /// <summary>Horizontal alignment: "left", "center", "right".</summary>
    public string? Align { get; set; }

    /// <summary>X offset in pixels.</summary>
    public double? X { get; set; }

    /// <summary>Y offset in pixels.</summary>
    public double? Y { get; set; }

    /// <summary>CSS style object.</summary>
    public object? Style { get; set; }

    /// <summary>Whether to render as HTML.</summary>
    public bool? UseHtml { get; set; }

    /// <summary>Whether the subtitle floats.</summary>
    public bool? Floating { get; set; }

    /// <summary>Vertical alignment.</summary>
    public string? VerticalAlign { get; set; }
}
