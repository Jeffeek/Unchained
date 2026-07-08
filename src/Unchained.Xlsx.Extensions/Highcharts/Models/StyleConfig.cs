namespace Unchained.Xlsx.Extensions.Highcharts.Models;

/// <summary>
///     Style configuration for text elements (titles, labels, tooltips, etc.).
/// </summary>
public class StyleConfig
{
    /// <summary>Text color as <c>#RRGGBB</c>.</summary>
    public string? Color { get; set; }

    /// <summary>Font family (e.g. "sans-serif", "Arial").</summary>
    public string? FontFamily { get; set; }

    /// <summary>Font size (e.g. "12px").</summary>
    public string? FontSize { get; set; }

    /// <summary>Font weight (e.g. "400", "bold", "Normal").</summary>
    public string? FontWeight { get; set; }

    /// <summary>Line height (e.g. "14px").</summary>
    public string? LineHeight { get; set; }

    /// <summary>Text decoration (e.g. "underline").</summary>
    public string? TextDecoration { get; set; }

    /// <summary>Z-index for stacking context.</summary>
    public double? ZIndex { get; set; }
}
