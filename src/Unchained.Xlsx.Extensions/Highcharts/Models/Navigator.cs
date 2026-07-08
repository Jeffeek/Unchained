namespace Unchained.Xlsx.Extensions.Highcharts.Models;

/// <summary>
///     Navigator configuration. Shows a mini overview of the data.
/// </summary>
public class Navigator : IHasAdditionalProperties
{
    /// <summary>Navigator series configuration.</summary>
    public SeriesConfig? Series { get; set; }

    /// <summary>Whether to adapt the navigator to updated data.</summary>
    public bool AdaptToUpdatedData { get; set; } = true;

    /// <summary>Height of the navigator in pixels.</summary>
    public double? Height { get; set; }

    /// <summary>Margin between the navigator and the main chart.</summary>
    public double? Margin { get; set; }

    /// <summary>Navigator background color.</summary>
    public string? BackgroundColor { get; set; }

    /// <summary>Navigator border color.</summary>
    public string? BorderColor { get; set; }

    /// <summary>Scrollbar configuration.</summary>
    public ScrollbarConfig? Scrollbar { get; set; }

    /// <summary>Additional properties not covered by the typed API.</summary>
    public Dictionary<string, object>? AdditionalProperties { get; set; }

    Dictionary<string, object>? IHasAdditionalProperties.GetAdditionalProperties() => AdditionalProperties;
}
