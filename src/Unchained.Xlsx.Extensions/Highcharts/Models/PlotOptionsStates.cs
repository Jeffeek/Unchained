namespace Unchained.Xlsx.Extensions.Highcharts.Models;

/// <summary>Series states (hover, select, inactive).</summary>
public class PlotOptionsStates
{
    /// <summary>Hover state configuration.</summary>
    public PlotOptionsState Hover { get; set; } = new();

    /// <summary>Select state configuration.</summary>
    public PlotOptionsState Select { get; set; } = new();

    /// <summary>Inactive state configuration.</summary>
    public PlotOptionsState Inactive { get; set; } = new();
}
