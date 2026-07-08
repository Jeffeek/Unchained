namespace Unchained.Xlsx.Extensions.Highcharts.Models;

/// <summary>Style for an active drilldown series.</summary>
public class DrilldownSeriesStyle
{
    /// <summary>Series color when active.</summary>
    public string? Color { get; set; }

    /// <summary>Border color when active.</summary>
    public string? BorderColor { get; set; }

    /// <summary>Opacity when active.</summary>
    public double? Opacity { get; set; }
}
