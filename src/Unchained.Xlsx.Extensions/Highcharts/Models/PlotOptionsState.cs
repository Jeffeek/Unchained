namespace Unchained.Xlsx.Extensions.Highcharts.Models;

/// <summary>A single series state.</summary>
public class PlotOptionsState
{
    /// <summary>Whether the state is enabled.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Hover halo configuration.</summary>
    public PlotOptionsHalo? Halo { get; set; }
}
