namespace Unchained.Xlsx.Extensions.Highcharts.Models;

/// <summary>
///     Pane configuration for polar and angular charts.
/// </summary>
public class Pane : IHasAdditionalProperties
{
    /// <summary>Start angle in degrees (0 = top).</summary>
    public double StartAngle { get; set; }

    /// <summary>End angle in degrees.</summary>
    public double EndAngle { get; set; }

    /// <summary>Inner radius as fraction of pane size (0–1).</summary>
    public double? InnerRadius { get; set; }

    /// <summary>Outer radius as fraction of pane size (0–1).</summary>
    public double? OuterRadius { get; set; }

    /// <summary>Pane background(s).</summary>
    public List<BackgroundConfig> Background { get; set; } = new();

    /// <summary>Additional properties not covered by the typed API.</summary>
    public Dictionary<string, object>? AdditionalProperties { get; set; }

    Dictionary<string, object>? IHasAdditionalProperties.GetAdditionalProperties() => AdditionalProperties;
}
