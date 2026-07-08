namespace Unchained.Xlsx.Extensions.Highcharts.Models;

/// <summary>Menu button configuration.</summary>
public class ButtonConfig
{
    /// <summary>Symbol theme (SVG theme object).</summary>
    public object? Symbol { get; set; }

    public string? SymbolFill { get; set; }
    public string? SymbolStroke { get; set; }
    public double? SymbolStrokeWidth { get; set; }
    public object? Theme { get; set; }
}
