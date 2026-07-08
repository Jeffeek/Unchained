namespace Unchained.Xlsx.Extensions.Highcharts.Models;

/// <summary>Scrollbar configuration (for zoomable charts).</summary>
public class ScrollbarConfig
{
    /// <summary>Whether the scrollbar is enabled.</summary>
    public bool? Enabled { get; set; }

    /// <summary>Scrollbar width in pixels (horizontal) or height (vertical).</summary>
    public double? Width { get; set; }

    /// <summary>Scrollbar height in pixels.</summary>
    public double? Height { get; set; }

    /// <summary>Scrollbar background color.</summary>
    public string? BackgroundColor { get; set; }

    /// <summary>Scrollbar track background color.</summary>
    public string? TrackBackgroundColor { get; set; }

    /// <summary>Scrollbar track border color.</summary>
    public string? TrackBorderColor { get; set; }

    /// <summary>Scrollbar track border radius.</summary>
    public double? TrackBorderRadius { get; set; }

    /// <summary>Scrollbar track border width.</summary>
    public double? TrackBorderWidth { get; set; }

    /// <summary>Scrollbar bar (thumb) background color.</summary>
    public string? BarBackgroundColor { get; set; }

    /// <summary>Scrollbar bar (thumb) border color.</summary>
    public string? BarBorderColor { get; set; }

    /// <summary>Scrollbar bar (thumb) border radius.</summary>
    public double? BarBorderRadius { get; set; }

    /// <summary>Scrollbar bar (thumb) border width.</summary>
    public double? BarBorderWidth { get; set; }

    /// <summary>Scrollbar rifle color (current position indicator).</summary>
    public string? RifleColor { get; set; }

    /// <summary>Minimum scrollbar length in pixels.</summary>
    public double? MinLength { get; set; }
}
