namespace Unchained.Xlsx.Extensions.Highcharts.Models;

/// <summary>Chart title.</summary>
public class TitleConfig : IHasAdditionalProperties
{
    /// <summary>The display text of the chart title.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Whether the title is displayed.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Title style (font family, size, color, etc.).</summary>
    public StyleConfig? Style { get; set; }

    /// <summary>Additional properties not covered by the typed API.</summary>
    public Dictionary<string, object>? AdditionalProperties { get; set; }

    Dictionary<string, object>? IHasAdditionalProperties.GetAdditionalProperties() => AdditionalProperties;
}
