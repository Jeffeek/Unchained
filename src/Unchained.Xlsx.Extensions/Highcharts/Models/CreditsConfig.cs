namespace Unchained.Xlsx.Extensions.Highcharts.Models;

/// <summary>Credits configuration (bottom-right attribution link).</summary>
public class CreditsConfig
{
    /// <summary>Whether the credits label is displayed.</summary>
    public bool? Enabled { get; set; }

    /// <summary>Credits text content.</summary>
    public string? Text { get; set; }

    /// <summary>Hyperlink URL.</summary>
    public string? Href { get; set; }

    /// <summary>Credits position configuration.</summary>
    public CreditsPositionConfig? Position { get; set; }

    /// <summary>Credits style (CSS object).</summary>
    public object? Style { get; set; }

    /// <summary>Whether to render as HTML.</summary>
    public bool? UseHtml { get; set; }
}
