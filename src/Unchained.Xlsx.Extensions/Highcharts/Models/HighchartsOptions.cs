using System.Text.Json;

namespace Unchained.Xlsx.Extensions.Highcharts.Models;

/// <summary>
///     Root-level Highcharts configuration object. Serialises to camelCase JSON
///     via <see cref="JsonSerializerOptions" /> with <see cref="JsonNamingPolicy.CamelCase" />
///     and <see cref="JsonSerializerOptions.IgnoreNullValues" />.
/// </summary>
public class HighchartsOptions : IHasAdditionalProperties
{
    /// <summary>Chart type and basic render options.</summary>
    public ChartConfig Chart { get; set; } = new();

    /// <summary>Chart title.</summary>
    public TitleConfig Title { get; set; } = new();

    /// <summary>Chart subtitle.</summary>
    public SubtitleConfig? Subtitle { get; set; }

    /// <summary>X-axis configuration (categories for category-type axes).</summary>
    public AxisConfig? XAxis { get; set; }

    /// <summary>Value (Y) axes — single axis by default, multiple for dual-axis charts.</summary>
    public List<YAxisConfig> YAxis { get; set; } = new();

    /// <summary>Data series.</summary>
    public List<SeriesConfig> Series { get; set; } = new();

    /// <summary>Global plot options (e.g. stacking rules applied to all series).</summary>
    public PlotOptions? PlotOptions { get; set; }

    /// <summary>Legend configuration.</summary>
    public LegendConfig? Legend { get; set; }

    /// <summary>Tooltip configuration.</summary>
    public Tooltip? Tooltip { get; set; }

    /// <summary>Drilldown configuration.</summary>
    public Drilldown? Drilldown { get; set; }

    /// <summary>Chart annotations (shapes, labels, markers).</summary>
    public Annotations? Annotations { get; set; }

    /// <summary>Navigator configuration.</summary>
    public Navigator? Navigator { get; set; }

    /// <summary>Pane configuration for polar/angular charts.</summary>
    public Pane? Pane { get; set; }

    /// <summary>Chart colors palette.</summary>
    public List<string>? Colors { get; set; }

    /// <summary>Exporting configuration.</summary>
    public ExportingConfig? Exporting { get; set; }

    /// <summary>Credits configuration.</summary>
    public CreditsConfig? Credits { get; set; }

    /// <summary>Whether to use UTC for dates in the chart.</summary>
    public bool? TimeUseUtc { get; set; }

    /// <summary>Additional properties not covered by the typed API.</summary>
    public Dictionary<string, object>? AdditionalProperties { get; set; }

    Dictionary<string, object>? IHasAdditionalProperties.GetAdditionalProperties() => AdditionalProperties;
}
