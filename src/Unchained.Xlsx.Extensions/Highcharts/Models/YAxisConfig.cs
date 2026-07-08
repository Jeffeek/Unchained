namespace Unchained.Xlsx.Extensions.Highcharts.Models;

/// <summary>
///     Value (Y) axis configuration. Supports multiple axes for dual-axis charts. Inherits the
///     shared axis styling and scaling properties from <see cref="AxisConfigBase" />.
/// </summary>
public class YAxisConfig : AxisConfigBase
{
    /// <summary>Axis index (0 = primary, 1+ = secondary).</summary>
    public int Index { get; set; }

    /// <summary>Whether to show opposite (secondary) axis on the right.</summary>
    public bool Opposite { get; set; }

    /// <summary>Axis identifier for referencing in series.</summary>
    public string? Id { get; set; }
}
